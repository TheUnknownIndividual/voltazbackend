(() => {
  const hash = new URLSearchParams(location.hash.replace(/^#/, ""));
  const code = hash.get("volt");
  const apiBase = hash.get("api");
  if (!code || !apiBase) return;
  history.replaceState(null, "", location.pathname + location.search);

  const host = document.createElement("div");
  host.style.cssText = "position:fixed;top:16px;right:16px;z-index:2147483647;";
  const root = host.attachShadow({ mode: "open" });
  root.innerHTML = `
    <style>
      .panel{width:380px;max-height:86vh;overflow:auto;background:#fff;color:#0f172a;font:13px/1.45 system-ui,sans-serif;border-radius:14px;box-shadow:0 10px 40px rgba(0,0,0,.25);border:1px solid #e2e8f0;padding:14px}
      h1{font-size:14px;margin:0 0 8px;color:#166534}
      .row{margin:2px 0;color:#334155}.ok{color:#15803d}.err{color:#b91c1c;font-weight:600}.warn{color:#b45309}
      .box{background:#f8fafc;border-radius:8px;padding:8px;margin:8px 0;white-space:pre-wrap;word-break:break-word;max-height:170px;overflow:auto}
      button{border:0;border-radius:8px;padding:8px 12px;font-weight:700;cursor:pointer;margin:6px 6px 0 0}
      .pub{background:#166534;color:#fff}.sec{background:#e2e8f0;color:#0f172a}button:disabled{opacity:.5;cursor:default}
    </style>
    <div class="panel"><h1>Volt.az &rarr; Lalafo</h1><div id="log"></div><div id="out"></div></div>`;
  document.documentElement.appendChild(host);
  const logEl = root.getElementById("log");
  const outEl = root.getElementById("out");

  const log = (text, cls = "") => {
    const row = document.createElement("div");
    row.className = "row " + cls;
    row.textContent = text;
    logEl.appendChild(row);
    return row;
  };

  const cookie = (name) => {
    const match = document.cookie.match(new RegExp("(?:^|; )" + name + "=([^;]*)"));
    return match ? decodeURIComponent(match[1]) : "";
  };

  const headers = (json) => {
    const token = cookie("jwt_token_spa");
    if (!token) throw new Error("You are not logged in to Lalafo. Log in and prepare the ad again.");
    const h = {
      authorization: "Bearer " + token,
      "country-id": "13",
      device: "pc",
      language: "az_AZ",
      "device-fingerprint": cookie("device_fingerprint"),
      "user-hash": cookie("event_user_hash"),
      "request-id": "react-client-" + crypto.randomUUID(),
    };
    if (json) h["content-type"] = "application/json";
    return h;
  };

  const bg = (message) =>
    new Promise((resolve, reject) => {
      chrome.runtime.sendMessage(message, (response) => {
        if (chrome.runtime.lastError) return reject(new Error(chrome.runtime.lastError.message));
        if (!response || !response.ok) return reject(new Error(response ? response.error : "No response"));
        resolve(response.data);
      });
    });

  const b64ToBlob = (b64, mime) => {
    const bin = atob(b64);
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    return new Blob([bytes], { type: mime });
  };

  const buildBody = (id, payload, imageIds, contact, submit) => {
    const params = [];
    for (const p of payload.params || []) {
      for (const v of p.valueIds || []) params.push({ id: p.paramId, value_id: v });
    }
    const hasPrice = payload.price != null && payload.price > 0;
    return {
      id,
      title: payload.title,
      description: payload.description,
      image_order: imageIds,
      params,
      currency: payload.currency || "AZN",
      category_id: payload.categoryId,
      city_id: payload.cityId,
      type: null,
      price: hasPrice ? payload.price : null,
      username: contact.username,
      mobile: contact.mobile,
      email: contact.email,
      is_negotiable: !hasPrice,
      is_private_ad: false,
      hide_phone: false,
      hide_chat: false,
      submit_request: submit,
    };
  };

  const putAd = async (id, body) => {
    const response = await fetch("/api/catalog/v32/posting-ads/temp/" + id, {
      method: "PUT",
      credentials: "include",
      headers: headers(true),
      body: JSON.stringify(body),
    });
    const json = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error("Lalafo rejected the ad (" + response.status + ")");
    return json;
  };

  const run = async () => {
    log("Loading the prepared ad from Volt.az...");
    const payload = await bg({ type: "volt:fetchPayload", api: apiBase, code });
    log("Ad: " + payload.productName, "ok");

    log("Creating a draft on Lalafo...");
    const created = await fetch("/api/catalog/v32/posting-ads/temp", {
      method: "POST",
      credentials: "include",
      headers: headers(true),
      body: "{}",
    });
    if (!created.ok) throw new Error("Could not create a Lalafo draft (" + created.status + ")");
    const draft = await created.json();
    const id = draft.id;
    const contact = {
      username: draft.username || payload.contact.username,
      mobile: draft.mobile || payload.contact.mobile,
      email: draft.email || payload.contact.email,
    };
    log("Draft #" + id + " created.", "ok");

    const imageIds = [];
    for (let i = 0; i < payload.imageUrls.length; i++) {
      const row = log("Uploading photo " + (i + 1) + "/" + payload.imageUrls.length + "...");
      try {
        const image = await bg({ type: "volt:fetchImage", url: payload.imageUrls[i] });
        const form = new FormData();
        form.append("image_file", b64ToBlob(image.b64, image.mime), "volt-" + (i + 1) + ".jpg");
        form.append("ad_id", String(id));
        const upload = await fetch("/api/upload/swoole-upload/v3/images/upload", {
          method: "POST",
          credentials: "include",
          headers: headers(false),
          body: form,
        });
        const json = await upload.json().catch(() => ({}));
        if (!upload.ok || !json.id) throw new Error("upload failed (" + upload.status + ")");
        imageIds.push(json.id);
        row.textContent = "Photo " + (i + 1) + " uploaded.";
        row.className = "row ok";
      } catch (error) {
        row.textContent = "Photo " + (i + 1) + " skipped: " + error.message;
        row.className = "row warn";
      }
    }

    log("Saving the ad details...");
    const saved = await putAd(id, buildBody(id, payload, imageIds, contact, false));
    log("Draft is filled.", "ok");

    const title = saved.title || payload.title;
    const selected = (saved.params || [])
      .map((p) => {
        const values = (p.values || []).filter((v) => v.selected).map((v) => v.value);
        return values.length ? p.name + ": " + values.join(", ") : null;
      })
      .filter(Boolean);

    const preview = document.createElement("div");
    preview.className = "box";
    preview.textContent =
      title + "\n" +
      (payload.price ? payload.price + " " + payload.currency : "Price by agreement") + "\n" +
      payload.categoryLabel + "\n" +
      (selected.length ? selected.join("\n") + "\n" : "") +
      "Photos: " + imageIds.length + "\n\n" + payload.description;
    outEl.appendChild(preview);

    for (const warning of payload.warnings || []) {
      const row = document.createElement("div");
      row.className = "row warn";
      row.textContent = "Check: " + warning;
      outEl.appendChild(row);
    }

    const publish = document.createElement("button");
    publish.className = "pub";
    publish.textContent = "Publish on Lalafo";
    publish.onclick = async () => {
      publish.disabled = true;
      try {
        const result = await putAd(id, buildBody(id, payload, imageIds, contact, true));
        log(
          result.rejected_reason
            ? "Lalafo response: " + result.rejected_reason
            : "Publish request sent. Check your Lalafo ads to confirm it went live.",
          result.rejected_reason ? "warn" : "ok"
        );
      } catch (error) {
        log(error.message, "err");
        publish.disabled = false;
      }
    };
    const open = document.createElement("button");
    open.className = "sec";
    open.textContent = "Open draft page";
    open.onclick = () => window.open(saved.url || "https://lalafo.az/", "_blank");
    const close = document.createElement("button");
    close.className = "sec";
    close.textContent = "Close";
    close.onclick = () => host.remove();
    outEl.append(publish, open, close);
  };

  run().catch((error) => {
    log(error.message || String(error), "err");
    const close = document.createElement("button");
    close.className = "sec";
    close.textContent = "Close";
    close.onclick = () => host.remove();
    outEl.appendChild(close);
  });
})();
