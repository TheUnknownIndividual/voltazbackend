const Volt = (() => {
  // #codes=<code1,code2,...>&api=<api base>&auto=1  (the older single #volt=<code> form still works)
  const readHash = () => {
    const hash = new URLSearchParams(location.hash.replace(/^#/, ""));
    const codes = (hash.get("codes") || hash.get("volt") || "")
      .split(",")
      .map((c) => c.trim())
      .filter((c) => /^[a-f0-9]{32}$/.test(c));
    const api = hash.get("api");
    if (codes.length === 0 || !api) return null;
    history.replaceState(null, "", location.pathname + location.search);
    return { codes, api, auto: hash.get("auto") === "1" };
  };

  const bg = (message) =>
    new Promise((resolve, reject) => {
      chrome.runtime.sendMessage(message, (response) => {
        if (chrome.runtime.lastError) return reject(new Error(chrome.runtime.lastError.message));
        if (!response || !response.ok) return reject(new Error(response ? response.error : "No response"));
        resolve(response.data);
      });
    });

  const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

  const b64ToBlob = (b64, mime) => {
    const bin = atob(b64);
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    return new Blob([bytes], { type: mime });
  };

  const createPanel = (title) => {
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
      <div class="panel"><h1></h1><div id="log"></div><div id="out"></div></div>`;
    const heading = root.querySelector("h1");
    heading.textContent = title;
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
    const close = () => host.remove();
    const button = (label, cls, onClick) => {
      const el = document.createElement("button");
      el.className = cls;
      el.textContent = label;
      el.onclick = () => onClick(el);
      outEl.appendChild(el);
      return el;
    };
    const preview = (text) => {
      const box = document.createElement("div");
      box.className = "box";
      box.textContent = text;
      outEl.appendChild(box);
    };
    const note = (text, cls) => {
      const row = document.createElement("div");
      row.className = "row " + cls;
      row.textContent = text;
      outEl.appendChild(row);
    };
    const reset = (newTitle) => {
      logEl.textContent = "";
      outEl.textContent = "";
      if (newTitle) heading.textContent = newTitle;
    };
    // Shows buttons and resolves with the key of the one pressed; the buttons are removed afterwards.
    const decide = (choices) =>
      new Promise((resolve) => {
        const made = choices.map((c) =>
          button(c.label, c.cls, () => {
            made.forEach((b) => b.remove());
            resolve(c.key);
          })
        );
      });
    return { log, close, button, preview, note, reset, decide };
  };

  // Best-effort: recording the listing in Volt must never break the posting itself.
  const report = async (session, code, body, panel) => {
    try {
      await bg({ type: "volt:report", api: session.api, code, body });
    } catch (error) {
      panel.log("Could not record this listing in Volt: " + error.message, "warn");
    }
  };

  const loadEnvelope = (session, code, marketplace) =>
    bg({ type: "volt:fetchEnvelope", api: session.api, code }).then((envelope) => {
      if (envelope.marketplace !== marketplace) {
        throw new Error("This prepared ad is for " + envelope.marketplace + ", not " + marketplace + ".");
      }
      return envelope;
    });

  // Processes the prepared ads one after another in this tab, pausing between them so the marketplace
  // never sees a burst. processItem returns { status: published|pending|draft|skipped }.
  const runBatch = async (session, marketplaceLabel, panel, processItem) => {
    const total = session.codes.length;
    const tally = { published: 0, pending: 0, draft: 0, skipped: 0, failed: 0 };
    for (let i = 0; i < total; i++) {
      panel.reset(`Volt.az → ${marketplaceLabel}  (${i + 1}/${total})`);
      try {
        const result = await processItem(session.codes[i], { auto: session.auto, panel, session });
        tally[result.status] = (tally[result.status] || 0) + 1;
      } catch (error) {
        tally.failed++;
        panel.log(error.message || String(error), "err");
        if (!session.auto) await panel.decide([{ key: "next", label: i + 1 < total ? "Next ad" : "Finish", cls: "sec" }]);
      }
      if (i + 1 < total && session.auto) await sleep(4000 + Math.floor(Math.random() * 2000));
    }
    panel.reset(`Volt.az → ${marketplaceLabel}  done`);
    panel.log(
      `Published: ${tally.published}, in moderation: ${tally.pending}, drafts: ${tally.draft}, skipped: ${tally.skipped}, failed: ${tally.failed}`,
      tally.failed ? "warn" : "ok"
    );
    panel.button("Close", "sec", panel.close);
  };

  return { readHash, bg, sleep, b64ToBlob, createPanel, report, loadEnvelope, runBatch };
})();
