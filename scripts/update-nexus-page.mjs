import fs from "node:fs/promises";
import http from "node:http";
import { spawn } from "node:child_process";
import readline from "node:readline/promises";
import { stdin as input, stdout as output } from "node:process";

const pageCopyPath = readEnv("NEXUS_PAGE_COPY_PATH");
const changelogPath = readEnv("NEXUS_CHANGELOG_PATH", false);
const modUrl = readEnv("NEXUS_MOD_URL");
const editUrl = readEnv("NEXUS_EDIT_URL", false);
const chromePath = readEnv("NEXUS_BROWSER_PATH");
const browserProfile = readEnv("NEXUS_BROWSER_PROFILE");
const gameDomain = readEnv("NEXUS_GAME_DOMAIN", false) || "slaythespire2";
const gameId = Number(readEnv("NEXUS_GAME_ID", false) || "8916");
const modId = Number(readEnv("NEXUS_MOD_ID", false) || "697");
const releaseVersion = readEnv("NEXUS_RELEASE_VERSION", false);
const remoteDebuggingPort = Number(readEnv("NEXUS_REMOTE_DEBUGGING_PORT", false) || "9222");
const loginOnly = readBool("NEXUS_PAGE_LOGIN_ONLY", false);
const savePage = readBool("NEXUS_PAGE_SAVE", false);
const syncChangelog = readBool("NEXUS_SYNC_CHANGELOG", true) && !loginOnly;

const resolvedEditUrl = editUrl || `https://www.nexusmods.com/games/${gameDomain}/mods/${modId}/edit/general`;
const pageCopy = await fs.readFile(pageCopyPath, "utf8");
const shortDescription = extractFencedSection(pageCopy, "Short Description", "text");
const fullDescription = extractFencedSection(pageCopy, "Full Description", "bbcode");
let changelogText = "";

if (!shortDescription || !fullDescription) {
  throw new Error(`Could not read short and full descriptions from ${pageCopyPath}`);
}

if (syncChangelog) {
  if (!releaseVersion) {
    throw new Error("NEXUS_RELEASE_VERSION is required when Nexus changelog sync is enabled.");
  }

  if (!changelogPath) {
    throw new Error("NEXUS_CHANGELOG_PATH is required when Nexus changelog sync is enabled.");
  }

  const changelog = await fs.readFile(changelogPath, "utf8");
  changelogText = extractMarkdownVersionSection(changelog, releaseVersion);
  if (!changelogText) {
    throw new Error(`Could not read CHANGELOG.md section for ${releaseVersion}.`);
  }
}

await fs.mkdir(browserProfile, { recursive: true });

const chrome = spawn(chromePath, [
  `--remote-debugging-port=${remoteDebuggingPort}`,
  `--user-data-dir=${browserProfile}`,
  "--no-first-run",
  "--no-default-browser-check",
  modUrl,
], {
  detached: true,
  stdio: "ignore",
});
chrome.unref();

await waitForDevTools();
const target = await openTab(modUrl);
const client = await connectCdp(target.webSocketDebuggerUrl);
await cdp(client, "Page.enable");
await cdp(client, "Runtime.enable");

await waitForPageReady(client);
await ensureLoggedIn(client);

if (loginOnly) {
  console.log("Nexus login profile is ready.");
  console.log(`Browser profile: ${browserProfile}`);
  process.exit(0);
}

await openEditor(client);
const preview = await getUpdatePreview(client);
printPreview(preview);

if (savePage) {
  await submitUpdates(client, preview);
  console.log("Nexus page update finished. View the public mod page once to check formatting.");
}
else {
  await fillPageCopyForReview(client);
  console.log("Filled the Nexus page editor for review and stopped before saving.");
  console.log("No public Nexus changes were submitted.");
}

client.close();

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

function extractFencedSection(markdown, heading, language) {
  const escapedHeading = heading.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const escapedLanguage = language.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const match = markdown.match(new RegExp(`##\\s+${escapedHeading}\\s+\`\`\`${escapedLanguage}\\s*([\\s\\S]*?)\\r?\\n\`\`\``, "i"));
  return match ? match[1].trim() : "";
}

function extractMarkdownVersionSection(markdown, version) {
  const escapedVersion = version.trim().replace(/^v/i, "").replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const match = markdown.match(new RegExp(`^##\\s+v?${escapedVersion}\\s*\\r?\\n(?<body>[\\s\\S]*?)(?=^##\\s+|$(?![\\s\\S]))`, "m"));
  return match?.groups?.body?.trim() || "";
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

async function getRuntimeConfigValue(client, key, fallback = "") {
  return evaluate(client, `(window.__RUNTIME_CONFIG__ && window.__RUNTIME_CONFIG__[${JSON.stringify(key)}]) || ${JSON.stringify(fallback)}`);
}

function cookieMatchesHost(cookieDomain, host) {
  const normalizedDomain = String(cookieDomain || "").toLowerCase().replace(/^\./, "");
  return host === normalizedDomain || host.endsWith(`.${normalizedDomain}`);
}

async function getCookieHeader(client, targetUrl) {
  await cdp(client, "Network.enable").catch(() => {});
  const { cookies = [] } = await cdp(client, "Network.getAllCookies");
  const host = new URL(targetUrl).hostname.toLowerCase();
  return cookies
    .filter(cookie => cookieMatchesHost(cookie.domain, host) || String(cookie.domain || "").toLowerCase().replace(/^\./, "").endsWith("nexusmods.com"))
    .map(cookie => `${cookie.name}=${cookie.value}`)
    .join("; ");
}

async function postFlamework(client, endpoint, body, errorMessage) {
  const result = await evaluate(client, `(async () => {
    const endpoint = ${JSON.stringify(endpoint)};
    const body = ${JSON.stringify(body)};
    const baseUrl = window.__RUNTIME_CONFIG__?.NEXT_PUBLIC_SITE_URL || "https://next.nexusmods.com";
    const response = await fetch(baseUrl + endpoint, {
      method: "POST",
      credentials: "include",
      headers: {
        "Accept": "application/json",
        "Content-Type": "application/json",
      },
      body: JSON.stringify(body),
    });
    const text = await response.text();
    let json = null;
    try {
      json = JSON.parse(text);
    }
    catch {
      json = null;
    }

    return {
      ok: response.ok,
      status: response.status,
      json,
      text,
    };
  })()`);

  if (!result.ok || !result.json || result.json.success !== true) {
    const detail = result.json && typeof result.json === "object" && "error" in result.json
      ? String(result.json.error)
      : `status ${result.status}`;
    throw new Error(`${errorMessage} (${detail}).`);
  }

  return result.json;
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

  throw new Error("Timed out waiting for the Nexus page to load.");
}

async function ensureLoggedIn(client) {
  const state = await getPageState(client);
  if (!state.loginPage && !state.hasPasswordField) {
    return;
  }

  console.log("Nexus is asking for login. Complete login in the browser window, then return here.");
  const rl = readline.createInterface({ input, output });
  try {
    await rl.question("Press Enter after Nexus has redirected back to the mod page...");
  }
  finally {
    rl.close();
  }

  await cdp(client, "Page.navigate", { url: resolvedEditUrl });
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

async function openEditor(client) {
  await cdp(client, "Page.navigate", { url: resolvedEditUrl });
  await waitForPageReady(client);

  const startedAt = Date.now();
  let formState = await getEditorState(client);
  while (!formState.canFill && Date.now() - startedAt < 30000) {
    await delay(500);
    formState = await getEditorState(client);
  }

  if (!formState.canFill) {
    throw new Error(`Could not find editable Nexus description fields on ${formState.url}. Page title: ${formState.title}`);
  }
}

async function getEditorState(client) {
  return evaluate(client, `(() => ({
    url: location.href,
    title: document.title,
    canFill: !!document.querySelector('#short-description') &&
      Array.from(document.querySelectorAll('textarea')).some(element => element.getAttribute('placeholder') === 'Describe your mod in detail...')
  }))()`);
}

async function getUpdatePreview(client) {
  const currentMod = await fetchCurrentMod(client);
  const documentation = syncChangelog ? await fetchDocumentation(client) : null;
  const page = getPagePlan(currentMod);
  const changelog = syncChangelog ? getChangelogPlan(documentation) : null;

  return { currentMod, page, changelog };
}

function getPagePlan(currentMod) {
  const currentSummary = currentMod.summary || "";
  const currentDescription = currentMod.description || "";
  const currentVersion = String(currentMod.version || "").trim();
  const summaryChanged = normalizeText(currentSummary) !== normalizeText(shortDescription);
  const descriptionChanged = normalizeNexusDescription(currentDescription) !== normalizeNexusDescription(fullDescription);
  const versionChanged = !!releaseVersion && currentVersion !== releaseVersion;

  return {
    summaryChanged,
    descriptionChanged,
    versionChanged,
    needsUpdate: summaryChanged || descriptionChanged || versionChanged,
    currentSummaryLength: currentSummary.length,
    nextSummaryLength: shortDescription.length,
    currentDescriptionLength: currentDescription.length,
    nextDescriptionLength: fullDescription.length,
    currentVersion,
    nextVersion: releaseVersion || currentVersion,
  };
}

function getChangelogPlan(documentation) {
  const versions = documentation?.changelog || {};
  const existingEntries = Array.isArray(versions[releaseVersion]) ? versions[releaseVersion] : [];
  const existingLines = existingEntries
    .flatMap(entry => splitChangelogLines(String(entry.change || "").replaceAll(/<br\s*\/?>/gi, "\n")));
  const localLines = splitChangelogLines(changelogText);
  const existingFingerprints = new Set(existingLines.map(getChangelogLineFingerprint));
  const missingLines = localLines.filter(line => !existingFingerprints.has(getChangelogLineFingerprint(line)));
  const submitLines = existingEntries.length > 0 ? [...existingLines, ...missingLines] : localLines;
  const needsUpdate = existingEntries.length === 0 ? localLines.length > 0 : missingLines.length > 0;

  return {
    action: existingEntries.length > 0 ? "append" : "add",
    changeIds: existingEntries.map(entry => entry.id).filter(id => Number.isFinite(Number(id))),
    existingEntryCount: existingEntries.length,
    localEntryCount: localLines.length,
    missingEntryCount: missingLines.length,
    nextEntryCount: submitLines.length,
    needsUpdate,
    submitText: submitLines.join("\n"),
    version: releaseVersion,
  };
}

async function fetchCurrentMod(client) {
  const query = `
    query Mod($modId: ID!, $gameId: ID!) {
      mod(modId: $modId, gameId: $gameId) {
        author
        description
        gameId
        modId
        name
        summary
        version
        modCategory {
          categoryId
          name
        }
        tags {
          id
          name
        }
      }
    }
  `;
  const payload = JSON.stringify({ query, variables: { gameId: String(gameId), modId: String(modId) }, operationName: "Mod" });

  return evaluate(client, `(async () => {
    const response = await fetch("https://api-router.nexusmods.com/graphql", {
      method: "POST",
      credentials: "include",
      cache: "no-store",
      headers: {
        "content-type": "application/json",
        "x-graphql-operationname": "Mod"
      },
      body: ${JSON.stringify(payload)}
    });

    if (!response.ok) {
      throw new Error("Failed to fetch Nexus mod metadata (" + response.status + ").");
    }

    const json = await response.json();
    if (json.errors && json.errors.length) {
      throw new Error(json.errors.map(error => error.message).join("; "));
    }

    if (!json.data || !json.data.mod) {
      throw new Error("Nexus mod metadata response did not include mod data.");
    }

    return json.data.mod;
  })()`);
}

async function fetchDocumentation(client) {
  return evaluate(client, `(async () => {
    const baseUrl = window.__RUNTIME_CONFIG__?.NEXT_PUBLIC_SITE_URL || "https://next.nexusmods.com";
    const response = await fetch(baseUrl + "/api/flamework/mods/documentation?gameId=${gameId}&modId=${modId}", {
      credentials: "include",
      cache: "no-store"
    });

    if (!response.ok) {
      throw new Error("Failed to fetch Nexus documentation metadata (" + response.status + ").");
    }

    return await response.json();
  })()`);
}

function printPreview(preview) {
  console.log("Planned Nexus updates:");
  console.log(`- Page summary: ${preview.page.summaryChanged ? "will update" : "already current"} (${preview.page.currentSummaryLength} -> ${preview.page.nextSummaryLength} chars)`);
  console.log(`- Page description: ${preview.page.descriptionChanged ? "will update" : "already current"} (${preview.page.currentDescriptionLength} -> ${preview.page.nextDescriptionLength} chars)`);
  console.log(`- Page version: ${preview.page.versionChanged ? "will update" : "already current"} (${preview.page.currentVersion || "none"} -> ${preview.page.nextVersion || "none"})`);

  if (preview.changelog) {
    let state = "already current";
    if (preview.changelog.needsUpdate && preview.changelog.action === "append") {
      state = `will append ${preview.changelog.missingEntryCount} missing entries`;
    }
    else if (preview.changelog.needsUpdate) {
      state = "will add";
    }

    console.log(`- Nexus changelog ${preview.changelog.version}: ${state} (${preview.changelog.existingEntryCount} -> ${preview.changelog.nextEntryCount} entries; ${preview.changelog.localEntryCount} local entries checked)`);
  }
}

async function submitUpdates(client, preview) {
  if (preview.page.needsUpdate) {
    await submitPageCopy(client, preview.currentMod);
    console.log("Saved Nexus page description.");
  }
  else {
    console.log("Nexus page description is already current.");
  }

  if (!preview.changelog) {
    return;
  }

  if (preview.changelog.needsUpdate) {
    await submitChangelog(client, preview.changelog);
    console.log(`Saved Nexus changelog ${preview.changelog.version}.`);
  }
  else {
    console.log(`Nexus changelog ${preview.changelog.version} is already current.`);
  }
}

async function submitPageCopy(client, currentMod) {
  const body = {
    modId,
    gameId,
    name: currentMod.name,
    summary: shortDescription,
    description: fullDescription,
    categoryId: currentMod.modCategory?.categoryId,
    author: currentMod.author,
    version: releaseVersion || currentMod.version,
    type: "1",
    tags: Array.isArray(currentMod.tags)
      ? currentMod.tags.map(tag => ({ id: String(tag.id), selected: true }))
      : [],
  };

  return postFlamework(client, "/api/flamework/mods/save", body, "Failed to save Nexus page description");
}

async function submitChangelog(client, plan) {
  const endpoint = plan.action === "add"
    ? "/api/flamework/mods/changelogs/add"
    : "/api/flamework/mods/changelogs/edit";
  const body = {
    changelogText: plan.submitText,
    gameId,
    modId,
    version: plan.version,
    ...(plan.action === "append" ? { changeIds: plan.changeIds } : {}),
  };

  return postFlamework(client, endpoint, body, "Failed to save Nexus changelog");
}

async function fillPageCopyForReview(client) {
  const payload = JSON.stringify({ shortDescription, fullDescription });
  const result = await evaluate(client, `(() => {
    const payload = ${payload};

    function setNativeValue(element, value) {
      element.focus();
      element.value = value;
      element.dispatchEvent(new Event("input", { bubbles: true }));
      element.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function escapeHtml(value) {
      return value
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;");
    }

    function bbcodeToPreviewHtml(bbcode) {
      let html = escapeHtml(bbcode);
      html = html.replace(/\\[b\\]([\\s\\S]*?)\\[\\/b\\]/gi, "<strong>$1</strong>");
      html = html.replace(/\\[code\\]([\\s\\S]*?)\\[\\/code\\]/gi, "<code>$1</code>");
      html = html.replace(/\\[url=([^\\]]+)\\]([\\s\\S]*?)\\[\\/url\\]/gi, '<a href="$1">$2</a>');
      html = html.replace(/\\[list\\]\\s*/gi, "<ul>");
      html = html.replace(/\\s*\\[\\/list\\]/gi, "</ul>");
      html = html.replace(/\\[\\*\\]([^\\n\\r]*)/g, "<li>$1</li>");
      return html
        .split(/\\r?\\n\\r?\\n/)
        .map(block => block.trim())
        .filter(Boolean)
        .map(block => block.startsWith("<ul>") ? block : "<div>" + block.replace(/\\r?\\n/g, "<br>") + "<br></div>")
        .join("\\n");
    }

    const shortField = document.querySelector("#short-description");
    const sourceAreas = Array.from(document.querySelectorAll('textarea[placeholder="Describe your mod in detail..."]'));
    const sourceArea = sourceAreas.find(element => element.value.trim()) || sourceAreas[0] || null;
    const editorFrame = Array.from(document.querySelectorAll("iframe")).find(frame => {
      try {
        return frame.contentDocument?.body?.isContentEditable;
      }
      catch {
        return false;
      }
    });

    let shortUpdated = false;
    let sourceUpdated = false;
    let previewUpdated = false;

    if (shortField) {
      setNativeValue(shortField, payload.shortDescription);
      shortUpdated = true;
    }

    if (sourceArea) {
      setNativeValue(sourceArea, payload.fullDescription);
      sourceUpdated = true;
    }

    if (editorFrame?.contentDocument?.body) {
      const body = editorFrame.contentDocument.body;
      body.innerHTML = bbcodeToPreviewHtml(payload.fullDescription);
      body.dispatchEvent(new Event("input", { bubbles: true }));
      body.dispatchEvent(new Event("change", { bubbles: true }));
      previewUpdated = true;
    }

    return { shortUpdated, sourceUpdated, previewUpdated };
  })()`);

  if (!result.shortUpdated || !result.sourceUpdated) {
    throw new Error("Could not stage both Nexus page fields in the editor for review.");
  }
}

function normalizeText(value) {
  return String(value || "")
    .replace(/\r\n/g, "\n")
    .replace(/[ \t]+\n/g, "\n")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

function normalizeNexusDescription(value) {
  return normalizeText(value)
    .replace(/<br\s*\/?>/gi, "\n")
    .replace(/\[\/\*\]/g, "")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

function splitChangelogLines(value) {
  return String(value || "")
    .replace(/\r\n/g, "\n")
    .split("\n")
    .map(line => line.trim())
    .filter(Boolean);
}

function getChangelogLineFingerprint(line) {
  return normalizeText(line)
    .replace(/^\s*[-*]\s+/, "")
    .replace(/`/g, "")
    .toLowerCase();
}

function delay(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}
