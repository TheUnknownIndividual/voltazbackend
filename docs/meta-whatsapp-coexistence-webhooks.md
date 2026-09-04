# Meta WhatsApp Coexistence webhook checklist

Use the same callback configured for the Volt Meta Inbox:

- Production: `https://api.volt.az/api/meta-inbox/webhook`
- Test: `https://test.api.volt.az/api/meta-inbox/webhook`

In the Meta App Dashboard, configure the `whatsapp_business_account` object and subscribe all four fields:

- `messages` — new Cloud API messages and message-status updates.
- `history` — chunks from the one-time WhatsApp Business app history import.
- `smb_message_echoes` — messages sent from the WhatsApp Business mobile app while Coexistence is active.
- `smb_app_state_sync` — WhatsApp Business app contacts and state synchronization.

The WABA-level app subscription (`/{WABA_ID}/subscribed_apps`) and the app-level webhook-field subscription are separate. A successful WABA subscription does not prove that the four fields above are enabled.

After changing the Meta configuration, run the protected production diagnostic:

```bash
cd /Users/user/Desktop/voltdev/Voltbackend
export FTP_HOST="136.243.98.218"
export FTP_USER="ftp_code"
./scripts/diagnose-whatsapp-inbox-prod-ftp.sh
```

The diagnostic reads the protected configuration into a temporary owner-only file, never prints access tokens or the App Secret, and reports a separate PASS/FAIL result for every required field.

The history import has additional Meta constraints: the business must approve history sharing, and the one-time request must be made within Meta's onboarding window. Field subscription alone does not start the import.
