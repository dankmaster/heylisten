import { open, stat } from "node:fs/promises";
import path from "node:path";

const apiBase = (process.env.NEXUSMODS_API_BASE || "https://api.nexusmods.com/v3").trim();

function readEnv(name, required = true) {
  const value = (process.env[name] || "").trim();
  if (required && !value) {
    throw new Error(`Missing required environment variable: ${name}`);
  }

  return value;
}

function readBool(name, defaultValue) {
  const value = (process.env[name] || "").trim().toLowerCase();
  if (!value) {
    return defaultValue;
  }

  if (["1", "true", "yes"].includes(value)) {
    return true;
  }

  if (["0", "false", "no"].includes(value)) {
    return false;
  }

  throw new Error(`${name} must be true or false.`);
}

async function apiFetch(apiKey, route, options = {}) {
  const response = await fetch(`${apiBase}${route}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      apikey: apiKey,
      "User-Agent": "HeyListen release uploader",
      ...(options.headers || {}),
    },
  });

  if (!response.ok) {
    throw new Error(`${route} failed: ${response.status} ${await response.text()}`);
  }

  return response;
}

function buildCompleteMultipartXml(parts) {
  const partXml = parts
    .map((part) => [
      "  <Part>",
      `    <PartNumber>${part.partNumber}</PartNumber>`,
      `    <ETag>${part.etag}</ETag>`,
      "  </Part>",
    ].join("\n"))
    .join("\n");

  return `<CompleteMultipartUpload>\n${partXml}\n</CompleteMultipartUpload>`;
}

async function uploadPart(fileHandle, url, partNumber, totalParts, partSizeBytes) {
  const buffer = Buffer.alloc(partSizeBytes);
  const offset = (partNumber - 1) * partSizeBytes;
  const { bytesRead } = await fileHandle.read(buffer, 0, partSizeBytes, offset);
  const body = bytesRead === partSizeBytes ? buffer : buffer.subarray(0, bytesRead);

  console.log(`Uploading part ${partNumber}/${totalParts} (${bytesRead} bytes)`);
  const response = await fetch(url, {
    method: "PUT",
    headers: {
      "Content-Type": "application/octet-stream",
      "Content-Length": String(bytesRead),
    },
    body,
  });

  if (!response.ok) {
    throw new Error(`Part ${partNumber} upload failed: ${response.status} ${await response.text()}`);
  }

  const etag = response.headers.get("ETag");
  if (!etag) {
    throw new Error(`Part ${partNumber} upload did not return an ETag.`);
  }

  return {
    partNumber,
    etag: etag.replaceAll('"', ""),
  };
}

async function waitForUpload(apiKey, uploadId) {
  for (let attempt = 0; attempt < 60; attempt += 1) {
    const response = await apiFetch(apiKey, `/uploads/${uploadId}`, { method: "GET" });
    const payload = await response.json();
    const state = payload?.data?.state;
    console.log(`Polling upload ${uploadId}: state = ${state}`);
    if (state === "available") {
      return payload.data;
    }

    const delayMs = Math.min(2000 * (1.5 ** attempt), 30000);
    await new Promise((resolve) => setTimeout(resolve, delayMs));
  }

  throw new Error(`Upload processing timed out: ${uploadId}`);
}

async function main() {
  const apiKey = readEnv("NEXUSMODS_API_KEY");
  const fileGroupId = readEnv("NEXUS_FILE_GROUP_ID");
  const filename = readEnv("NEXUS_UPLOAD_FILENAME");
  const version = readEnv("NEXUS_UPLOAD_VERSION");
  const displayName = readEnv("NEXUS_UPLOAD_DISPLAY_NAME", false) || path.basename(filename);
  const description = readEnv("NEXUS_UPLOAD_DESCRIPTION", false) || undefined;
  const fileCategory = readEnv("NEXUS_UPLOAD_FILE_CATEGORY", false) || "main";
  const archiveExistingFile = readBool("NEXUS_ARCHIVE_EXISTING_FILE", false);
  const primaryModManagerDownload = readBool("NEXUS_PRIMARY_MOD_MANAGER_DOWNLOAD", true);
  const allowModManagerDownload = readBool("NEXUS_ALLOW_MOD_MANAGER_DOWNLOAD", true);
  const showRequirementsPopUp = readBool("NEXUS_SHOW_REQUIREMENTS_POP_UP", false);

  const { size } = await stat(filename);
  console.log(`Requesting Nexus upload for ${path.basename(filename)} (${size} bytes)`);
  const multipartResponse = await apiFetch(apiKey, "/uploads/multipart", {
    method: "POST",
    body: JSON.stringify({
      filename: path.basename(filename),
      size_bytes: String(size),
    }),
  });
  const multipart = await multipartResponse.json();
  const uploadId = multipart.data.id;
  const partUrls = multipart.data.part_presigned_urls;
  const partSizeBytes = Number(multipart.data.part_size_bytes);
  const completeUrl = multipart.data.complete_presigned_url;

  console.log(`Created multipart upload: ${uploadId} (${partUrls.length} parts)`);
  const fileHandle = await open(filename, "r");
  const parts = [];
  try {
    for (let index = 0; index < partUrls.length; index += 1) {
      parts.push(await uploadPart(fileHandle, partUrls[index], index + 1, partUrls.length, partSizeBytes));
    }
  }
  finally {
    await fileHandle.close();
  }

  const completeResponse = await fetch(completeUrl, {
    method: "POST",
    headers: { "Content-Type": "application/xml" },
    body: buildCompleteMultipartXml(parts),
  });
  if (!completeResponse.ok) {
    throw new Error(`Completing multipart upload failed: ${completeResponse.status} ${await completeResponse.text()}`);
  }

  const finaliseResponse = await apiFetch(apiKey, `/uploads/${uploadId}/finalise`, { method: "POST" });
  const finalise = await finaliseResponse.json();
  console.log(`Finalised upload: ${finalise.data.id} (state: ${finalise.data.state})`);
  await waitForUpload(apiKey, uploadId);

  const updateResponse = await apiFetch(apiKey, `/mod-file-update-groups/${fileGroupId}/versions`, {
    method: "POST",
    body: JSON.stringify({
      upload_id: uploadId,
      name: displayName,
      description,
      version,
      file_category: fileCategory,
      archive_existing_file: archiveExistingFile,
      primary_mod_manager_download: primaryModManagerDownload,
      allow_mod_manager_download: allowModManagerDownload,
      show_requirements_pop_up: showRequirementsPopUp,
    }),
  });
  const update = await updateResponse.json();

  console.log(`File updated successfully: ${update.data.id}`);
  console.log(`file_uid=${update.data.id}`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
});
