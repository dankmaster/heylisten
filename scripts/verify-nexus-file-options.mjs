import fs from "node:fs/promises";
import http from "node:http";
import { spawn } from "node:child_process";
import readline from "node:readline/promises";
import { stdin as input, stdout as output } from "node:process";

const chromePath = readEnv("NEXUS_BROWSER_PATH");
const browserProfile = readEnv("NEXUS_BROWSER_PROFILE");
const gameDomain = readEnv("NEXUS_GAME_DOMAIN", false) || "slaythespire2";
const gameId = Number(readEnv("NEXUS_GAME_ID", false) || "8916");
const modId = Number(readEnv("NEXUS_MOD_ID"));
const releaseVersion = readEnv("NEXUS_RELEASE_VERSION");
const displayName = readEnv("NEXUS_UPLOAD_DISPLAY_NAME", false);
const expectedFileId = readEnv("NEXUS_UPLOAD_FILE_ID", false);
const expectDefaultModManagerDownload = readBool("NEXUS_EXPECT_DEFAULT_MOD_MANAGER_DOWNLOAD", true);
const remoteDebuggingPort = Number(readEnv("NEXUS_REMOTE_DEBUGGING_PORT", false) || "9222");
const editFilesUrl = readEnv("NEXUS_EDIT_FILES_URL", false) ||
  `https://www.nexusmods.com/games/${gameDomain}/mods/${modId}/edit/files`;

await fs.mkdir(browserProfile, { recursive: true });

const chrome = spawn(chromePath, [
  `--remote-debugging-port=${remoteDebuggingPort}`,
  `--user-data-dir=${browserProfile}`,
  "--no-first-run",
  "--no-default-browser-check",
  editFilesUrl,
], {
  detached: true,
  stdio: "ignore",
});
chrome.unref();

await waitForDevTools();
const target = await openTab(editFilesUrl);
const client = await connectCdp(target.webSocketDebuggerUrl);
await cdp(client, "Page.enable");
await cdp(client, "Runtime.enable");

try {
  await waitForPageReady(client);
  await ensureLoggedIn(client);
  await cdp(client, "Page.navigate", { url: editFilesUrl });
  await waitForPageReady(client);

  const files = await fetchModFiles(client);
  const file = findReleaseFile(files);
  const modManagerDownloadAllowed = Number(file.manager) === 0;
  const defaultModManagerDownload = Number(file.primary) === 1;

  console.log("Nexus file editor options:");
  console.log(`- File: ${file.name} ${file.version ? `(${file.version})` : ""}`.trim());
  console.log(`- File ID: ${file.fileId}`);
  console.log(`- Category: ${categoryName(file.categoryId)}`);
  console.log(`- Mod-manager download: ${modManagerDownloadAllowed ? "enabled" : "disabled"}`);
  console.log(`- Default mod-manager download: ${defaultModManagerDownload ? "enabled" : "disabled"}`);

  if (!modManagerDownloadAllowed) {
    throw new Error(`Nexus file ${file.fileId} is not allowed for mod-manager download in the file editor.`);
  }

  if (expectDefaultModManagerDownload && !defaultModManagerDownload) {
    throw new Error(`Nexus file ${file.fileId} is not the default mod-manager download in the file editor.`);
  }

  console.log("Verified Nexus file editor mod-manager options.");
}
finally {
  client.close();
}

function readEnv(name, required = true) {
  const value = process.env[name];
  if (required && (!value || !value.trim())) {
    throw new Error(`${name} is required.`);
  }

  return value ? value.trim() : "";
}

function readBool(name, defaultValue) {
  const value = process.env[name];
  if (!value || !value.trim()) {
    return defaultValue;
  }

  return ["1", "true", "yes", "y"].includes(value.trim().toLowerCase());
}

async function requestJson(pathname, method = "GET") {
  return new Promise((resolve, reject) => {
    const request = http.request({
      host: "127.0.0.1",
      port: remoteDebuggingPort,
      path: pathname,
      method,
    }, response => {
      const chunks = [];
      response.on("data", chunk => chunks.push(chunk));
      response.on("end", () => {
        const body = Buffer.concat(chunks).toString("utf8");
        if (response.statusCode < 200 || response.statusCode >= 300) {
          reject(new Error(`DevTools ${method} ${pathname} failed with ${response.statusCode}: ${body}`));
          return;
        }

        resolve(JSON.parse(body));
      });
    });

    request.on("error", reject);
    request.end();
  });
}

async function waitForDevTools() {
  const startedAt = Date.now();
  while (Date.now() - startedAt < 30000) {
    try {
      await requestJson("/json/version");
      return;
    }
    catch {
      await delay(250);
    }
  }

  throw new Error("Timed out waiting for Chrome DevTools.");
}

async function openTab(url) {
  return requestJson(`/json/new?${encodeURIComponent(url)}`, "PUT");
}

function connectCdp(webSocketDebuggerUrl) {
  const socket = new WebSocket(webSocketDebuggerUrl);
  let nextId = 1;
  const pending = new Map();

  socket.addEventListener("message", event => {
    const message = JSON.parse(event.data);
    if (!message.id) {
      return;
    }

    const callbacks = pending.get(message.id);
    if (!callbacks) {
      return;
    }

    pending.delete(message.id);
    if (message.error) {
      callbacks.reject(new Error(message.error.message || JSON.stringify(message.error)));
    }
    else {
      callbacks.resolve(message.result);
    }
  });

  return new Promise((resolve, reject) => {
    socket.addEventListener("open", () => resolve({
      close() {
        socket.close();
      },
      send(method, params = {}) {
        const id = nextId++;
        socket.send(JSON.stringify({ id, method, params }));
        return new Promise((resolveMessage, rejectMessage) => {
          pending.set(id, { resolve: resolveMessage, reject: rejectMessage });
        });
      },
    }));
    socket.addEventListener("error", reject);
  });
}

function cdp(client, method, params = {}) {
  return client.send(method, params);
}

async function evaluate(client, expression, returnByValue = true) {
  const result = await cdp(client, "Runtime.evaluate", {
    expression,
    awaitPromise: true,
    returnByValue,
  });

  if (result.exceptionDetails) {
    const text = result.exceptionDetails.exception?.description || result.exceptionDetails.text;
    throw new Error(text);
  }

  return result.result.value;
}

async function waitForPageReady(client) {
  const startedAt = Date.now();
  while (Date.now() - startedAt < 30000) {
    const state = await evaluate(client, "document.readyState");
    if (state === "interactive" || state === "complete") {
      return;
    }

    await delay(250);
  }

  throw new Error("Timed out waiting for the Nexus file editor to load.");
}

async function ensureLoggedIn(client) {
  const state = await getPageState(client);
  if (!state.loginPage && !state.hasPasswordField) {
    return;
  }

  console.log("Nexus is asking for login. Complete login in the browser window, then return here.");
  const rl = readline.createInterface({ input, output });
  try {
    await rl.question("Press Enter after Nexus has redirected back to the file editor...");
  }
  finally {
    rl.close();
  }

  await cdp(client, "Page.navigate", { url: editFilesUrl });
  await waitForPageReady(client);

  const afterLogin = await getPageState(client);
  if (afterLogin.loginPage || afterLogin.hasPasswordField) {
    throw new Error("Still on the Nexus login page.");
  }
}

async function getPageState(client) {
  return evaluate(client, `(() => ({
    url: location.href,
    title: document.title,
    loginPage: location.href.includes("/auth/sign_in"),
    hasPasswordField: !!document.querySelector('input[type="password"]')
  }))()`);
}

async function fetchModFiles(client) {
  const query = `
    query ModFiles($gameId: ID!, $modId: ID!) {
      modFiles(gameId: $gameId, modId: $modId) {
        categoryId
        date
        fileId
        id
        manager
        name
        primary
        uri
        version
      }
    }
  `;
  const payload = JSON.stringify({
    query,
    variables: { gameId: String(gameId), modId: String(modId) },
    operationName: "ModFiles",
  });

  return evaluate(client, `(async () => {
    const response = await fetch("https://api-router.nexusmods.com/graphql", {
      method: "POST",
      credentials: "include",
      cache: "no-store",
      headers: {
        "content-type": "application/json",
        "x-graphql-operationname": "ModFiles"
      },
      body: ${JSON.stringify(payload)}
    });

    if (!response.ok) {
      throw new Error("Failed to fetch Nexus file metadata (" + response.status + ").");
    }

    const json = await response.json();
    if (json.errors && json.errors.length) {
      throw new Error(json.errors.map(error => error.message).join("; "));
    }

    if (!json.data || !Array.isArray(json.data.modFiles)) {
      throw new Error("Nexus file metadata response did not include mod files.");
    }

    return json.data.modFiles;
  })()`);
}

function findReleaseFile(files) {
  if (expectedFileId) {
    const match = files.find(file => String(file.fileId) === expectedFileId);
    if (!match) {
      throw new Error(`Could not find Nexus file ID ${expectedFileId} in the file editor.`);
    }

    return match;
  }

  const versionMatches = files.filter(file => normalizeVersion(file.version) === normalizeVersion(releaseVersion));
  const displayMatches = displayName
    ? versionMatches.filter(file => normalizeText(file.name) === normalizeText(displayName))
    : [];
  const candidates = (displayMatches.length > 0 ? displayMatches : versionMatches)
    .sort((left, right) => Number(right.date || 0) - Number(left.date || 0));

  const active = candidates.find(file => ![6, 7].includes(Number(file.categoryId)));
  if (active) {
    return active;
  }

  if (candidates.length > 0) {
    return candidates[0];
  }

  const seen = files
    .map(file => `${file.fileId}: ${file.name} ${file.version}`.trim())
    .join("; ");
  throw new Error(`Could not find Nexus file for ${displayName || "release"} ${releaseVersion}. Files seen: ${seen}`);
}

function normalizeVersion(value) {
  return String(value || "").trim().replace(/^v/i, "");
}

function normalizeText(value) {
  return String(value || "").trim().replace(/\s+/g, " ").toLowerCase();
}

function categoryName(categoryId) {
  const names = new Map([
    [1, "main"],
    [2, "update"],
    [3, "optional"],
    [4, "old"],
    [5, "miscellaneous"],
    [6, "deleted"],
    [7, "archived"],
  ]);

  return names.get(Number(categoryId)) || String(categoryId || "unknown");
}

function delay(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}
