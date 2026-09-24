(() => {
  const session = Volt.readHash();
  if (!session) return;

  const panel = Volt.createPanel("Volt.az → Lalafo");

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

  const processItem = async (code, { auto }) => {
    panel.log("Loading the prepared ad from Volt.az...");
    const envelope = await Volt.loadEnvelope(session, code, "lalafo");
    const payload = envelope.payload;
    panel.log("Ad: " + payload.productName, "ok");

    panel.log("Creating a draft on Lalafo...");
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
    panel.log("Draft #" + id + " created.", "ok");

    const imageIds = [];
    for (let i = 0; i < payload.imageUrls.length; i++) {
      const row = panel.log("Uploading photo " + (i + 1) + "/" + payload.imageUrls.length + "...");
      try {
        const image = await Volt.bg({ type: "volt:fetchImage", url: payload.imageUrls[i] });
        const form = new FormData();
        form.append("image_file", Volt.b64ToBlob(image.b64, image.mime), "volt-" + (i + 1) + ".jpg");
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

    panel.log("Saving the ad details...");
    const saved = await putAd(id, buildBody(id, payload, imageIds, contact, false));
    panel.log("Draft is filled.", "ok");
    await Volt.report(session, code, { externalId: String(id), url: saved.url || null, status: "draft" }, panel);

    const publish = async () => {
      const result = await putAd(id, buildBody(id, payload, imageIds, contact, true));
      if (result.rejected_reason) throw new Error("Lalafo response: " + result.rejected_reason);
      await Volt.report(session, code, { externalId: String(id), url: result.url || saved.url || null, status: "published" }, panel);
      panel.log("Publish request sent. Check your Lalafo ads to confirm it went live.", "ok");
    };

    if (auto) {
      panel.log("Publishing automatically...");
      await publish();
      return { status: "published" };
    }

    const selected = (saved.params || [])
      .map((p) => {
        const values = (p.values || []).filter((v) => v.selected).map((v) => v.value);
        return values.length ? p.name + ": " + values.join(", ") : null;
      })
      .filter(Boolean);
    panel.preview(
      (saved.title || payload.title) + "\n" +
      (payload.price ? payload.price + " " + payload.currency : "Price by agreement") + "\n" +
      payload.categoryLabel + "\n" +
      (selected.length ? selected.join("\n") + "\n" : "") +
      "Photos: " + imageIds.length + "\n\n" + payload.description
    );
    for (const warning of payload.warnings || []) panel.note("Check: " + warning, "warn");

    for (;;) {
      const choice = await panel.decide([
        { key: "publish", label: "Publish on Lalafo", cls: "pub" },
        { key: "open", label: "Open draft page", cls: "sec" },
        { key: "skip", label: "Leave as draft / next", cls: "sec" },
      ]);
      if (choice === "open") {
        window.open(saved.url || "https://lalafo.az/", "_blank");
        continue;
      }
      if (choice === "skip") return { status: "draft" };
      try {
        await publish();
        return { status: "published" };
      } catch (error) {
        panel.log(error.message, "err");
      }
    }
  };

  Volt.runBatch(session, "Lalafo", panel, processItem);
})();
