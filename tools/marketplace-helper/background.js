const ALLOWED_API_BASES = ["https://api.volt.az/api", "https://test.api.volt.az/api"];
const ALLOWED_IMAGE_HOSTS = ["cloudfiles.volt.az"];

function toBase64(buffer) {
  const bytes = new Uint8Array(buffer);
  let binary = "";
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
  }
  return btoa(binary);
}

function checkApi(api, code) {
  if (!ALLOWED_API_BASES.includes(api)) throw new Error("API base is not allowed");
  if (!/^[a-f0-9]{32}$/.test(code)) throw new Error("Invalid code");
}

async function fetchEnvelope(api, code) {
  checkApi(api, code);
  const response = await fetch(`${api}/Marketplace/payload/${code}`);
  const json = await response.json();
  if (!response.ok || !json.success) {
    throw new Error("Prepared ad not found or expired. Prepare it again in the Volt admin.");
  }
  return json.data;
}

async function report(api, code, body) {
  checkApi(api, code);
  const response = await fetch(`${api}/Marketplace/report/${code}`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });
  const json = await response.json().catch(() => ({}));
  if (!response.ok || !json.success) throw new Error("Could not record the listing in Volt");
  return true;
}

async function fetchImage(url) {
  const parsed = new URL(url);
  if (parsed.protocol !== "https:" || !ALLOWED_IMAGE_HOSTS.includes(parsed.hostname)) {
    throw new Error("Image host is not allowed");
  }
  const response = await fetch(url);
  if (!response.ok) throw new Error(`Image download failed (${response.status})`);
  const mime = response.headers.get("content-type") || "image/jpeg";
  return { mime, b64: toBase64(await response.arrayBuffer()) };
}

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  const run = async () => {
    if (message.type === "volt:fetchEnvelope") return fetchEnvelope(message.api, message.code);
    if (message.type === "volt:fetchImage") return fetchImage(message.url);
    if (message.type === "volt:report") return report(message.api, message.code, message.body);
    throw new Error("Unknown message");
  };
  run().then(
    (data) => sendResponse({ ok: true, data }),
    (error) => sendResponse({ ok: false, error: String(error && error.message ? error.message : error) })
  );
  return true;
});
