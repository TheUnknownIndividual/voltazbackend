(() => {
  const session = Volt.readHash();
  if (!session) return;

  const panel = Volt.createPanel("Volt.az → Tap.az");

  const gql = async (query, variables) => {
    const response = await fetch("/graphql", {
      method: "POST",
      credentials: "include",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ query, variables }),
    });
    if (!response.ok) throw new Error("Tap.az request failed (" + response.status + ")");
    return response.json();
  };

  const findCategory = (list, legacyId) => {
    for (const category of list || []) {
      if (String(category.legacyResourceId) === String(legacyId)) return category;
      const nested = findCategory(category.children, legacyId);
      if (nested) return nested;
    }
    return null;
  };

  const CREATE_AD = `mutation CreateAd($adParams: CreateAdAttributes!) {
    createAd(adParams: $adParams) {
      entity { id legacyResourceId path status statusMessage title }
      errors { code message path }
    }
  }`;

  const run = async () => {
    panel.log("Loading the prepared ad from Volt.az...");
    const envelope = await Volt.loadEnvelope(session, "tapaz");
    const payload = envelope.payload;
    panel.log("Ad: " + payload.productName, "ok");

    panel.log("Checking your Tap.az login...");
    const me = (await gql("{ currentUser { name email phone } }")).data?.currentUser;
    if (!me) throw new Error("You are not logged in to Tap.az. Log in and prepare the ad again.");
    panel.log("Logged in as " + (me.name || me.email || "your account") + ".", "ok");

    const photoIds = [];
    for (let i = 0; i < payload.imageUrls.length; i++) {
      const row = panel.log("Uploading photo " + (i + 1) + "/" + payload.imageUrls.length + "...");
      try {
        const image = await Volt.bg({ type: "volt:fetchImage", url: payload.imageUrls[i] });
        const form = new FormData();
        form.append("images[]", Volt.b64ToBlob(image.b64, image.mime), "volt-" + (i + 1) + ".jpg");
        const upload = await fetch("https://photos.tap.az/pond?lang=az", {
          method: "POST",
          headers: { Accept: "application/json" },
          body: form,
        });
        const json = await upload.json().catch(() => ({}));
        const id = json.photos && json.photos[0] && json.photos[0].id;
        if (!upload.ok || !id) throw new Error("upload failed (" + upload.status + ")");
        photoIds.push(String(id));
        row.textContent = "Photo " + (i + 1) + " uploaded.";
        row.className = "row ok";
      } catch (error) {
        row.textContent = "Photo " + (i + 1) + " skipped: " + error.message;
        row.className = "row warn";
      }
    }

    panel.preview(
      payload.title + "\n" +
      (payload.price ? payload.price + " AZN" : "No price set") + "\n" +
      payload.targetLabel + "\n" +
      "Photos: " + photoIds.length + "\n\n" + payload.body
    );
    for (const warning of payload.warnings || []) panel.note("Check: " + warning, "warn");
    panel.note("Tap.az has no draft step: Publish submits the ad to Tap.az moderation.", "warn");

    const buildParams = (categoryId, regionId) => ({
      title: payload.title,
      body: payload.body,
      price: payload.price,
      categoryId,
      regionId,
      photoIds,
      source: "DESKTOP",
      contactAttributes: {
        contactType: "CALLS_AND_MESSAGES",
        email: me.email || "",
        name: me.name || "",
        phones: [me.phone || ""],
      },
      propertySet: {
        collection: payload.properties.map((p) => ({ legacyId: String(p.propertyId), value: String(p.optionId) })),
        boolean: [],
        range: [],
      },
    });

    const submit = async (params) => {
      const result = (await gql(CREATE_AD, { adParams: params })).data?.createAd;
      if (!result) throw new Error("Tap.az returned no result");
      return result;
    };

    panel.button("Publish on Tap.az", "pub", async (el) => {
      el.disabled = true;
      try {
        if (!payload.price) throw new Error("Tap.az requires a price. Set one on the product first.");
        if (photoIds.length === 0) throw new Error("Tap.az requires at least one photo.");
        if (!me.phone) throw new Error("Add a phone number to your Tap.az profile first.");

        const tree = (await gql("{ categories(scope: CREATE_AD) { id legacyResourceId children { id legacyResourceId children { id legacyResourceId } } } }")).data?.categories;
        const category = findCategory(tree, payload.categoryId);
        const regions = (await gql("{ regions { id legacyResourceId } }")).data?.regions || [];
        const region = regions.find((r) => String(r.legacyResourceId) === String(payload.regionId));
        if (!category || !region) throw new Error("Could not resolve the Tap.az category or region.");

        let result = await submit(buildParams(category.id, region.id));
        const idProblem = (result.errors || []).some((e) => (e.path || []).some((p) => /categoryId|regionId/.test(p)));
        if (!result.entity && idProblem) {
          panel.log("Retrying with numeric ids...", "warn");
          result = await submit(buildParams(String(category.legacyResourceId), String(region.legacyResourceId)));
        }

        if (!result.entity) {
          const messages = (result.errors || []).map((e) => e.message).join("; ") || "unknown error";
          throw new Error("Tap.az rejected the ad: " + messages);
        }

        const entity = result.entity;
        const url = entity.path ? "https://tap.az" + (entity.path.startsWith("/") ? entity.path : "/" + entity.path) : null;
        const status = /publish|active/i.test(entity.status || "") ? "published" : "pending";
        panel.log("Submitted to Tap.az" + (entity.statusMessage ? ": " + entity.statusMessage : "."), "ok");
        await Volt.report(session, { externalId: String(entity.legacyResourceId || entity.id), url, status }, panel);
        if (url) panel.button("Open ad", "sec", () => window.open(url, "_blank"));
      } catch (error) {
        panel.log(error.message, "err");
        el.disabled = false;
      }
    });
    panel.button("Close", "sec", panel.close);
  };

  run().catch((error) => {
    panel.log(error.message || String(error), "err");
    panel.button("Close", "sec", panel.close);
  });
})();
