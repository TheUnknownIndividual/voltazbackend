#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"
META_INBOX_TARGET="${META_INBOX_TARGET:-test}"
case "$META_INBOX_TARGET" in
  test) EXPECTED_REMOTE_DIR="/testapivoltaz"; EXPECTED_CALLBACK_URL="https://test.api.volt.az/api/meta-inbox/webhook"; OAUTH_REDIRECT_URI="https://test.volt.az/admin-dashboard/whatsapp-setup"; TARGET_LABEL="test" ;;
  production) EXPECTED_REMOTE_DIR="/apivoltaz"; EXPECTED_CALLBACK_URL="https://api.volt.az/api/meta-inbox/webhook"; OAUTH_REDIRECT_URI="https://volt.az/admin-dashboard/whatsapp-setup"; TARGET_LABEL="production" ;;
  *) echo "META_INBOX_TARGET must be test or production." >&2; exit 1 ;;
esac
REMOTE_DIR="${REMOTE_DIR:-$EXPECTED_REMOTE_DIR}"
CALLBACK_URL="${CALLBACK_URL:-$EXPECTED_CALLBACK_URL}"
GRAPH_API_VERSION="${GRAPH_API_VERSION:-v26.0}"
META_APP_ID="${META_APP_ID:-2182171352343231}"

command -v lftp >/dev/null || { echo "lftp is required. On macOS: brew install lftp" >&2; exit 1; }
command -v openssl >/dev/null || { echo "openssl is required." >&2; exit 1; }
command -v node >/dev/null || { echo "node is required to create JSON safely." >&2; exit 1; }
command -v curl >/dev/null || { echo "curl is required." >&2; exit 1; }

if [ -z "${FTP_HOST:-}" ]; then read -r -p "FTP host: " FTP_HOST; fi
if [ -z "${FTP_USER:-}" ]; then read -r -p "FTP username: " FTP_USER; fi
if [ -z "${FTP_PASS:-}" ]; then read -r -s -p "FTP password: " FTP_PASS; echo; fi

read -r -s -p "Meta App Secret: " META_APP_SECRET
echo
read -r -s -p "Facebook Page Access Token (press Enter if Meta has not issued it yet): " META_PAGE_ACCESS_TOKEN
echo

[ -n "$FTP_HOST" ] || { echo "FTP host cannot be empty." >&2; exit 1; }
[ -n "$FTP_USER" ] || { echo "FTP username cannot be empty." >&2; exit 1; }
[ -n "$FTP_PASS" ] || { echo "FTP password cannot be empty." >&2; exit 1; }
[ -n "$META_APP_SECRET" ] || { echo "Meta App Secret cannot be empty." >&2; exit 1; }
[[ "$REMOTE_DIR" == "$EXPECTED_REMOTE_DIR" ]] || { echo "Refusing to configure an unexpected $TARGET_LABEL directory: $REMOTE_DIR" >&2; exit 1; }
[[ "$CALLBACK_URL" == "$EXPECTED_CALLBACK_URL" ]] || { echo "Refusing to configure an unexpected $TARGET_LABEL callback: $CALLBACK_URL" >&2; exit 1; }

TASK_TEMP_DIR="$(mktemp -d)"
cleanup() {
  unset FTP_PASS META_APP_SECRET META_PAGE_ACCESS_TOKEN META_WHATSAPP_ACCESS_TOKEN WHATSAPP_PHONE_NUMBER_ID WHATSAPP_BUSINESS_ACCOUNT_ID
  if [ -n "${TASK_TEMP_DIR:-}" ] && [ -d "$TASK_TEMP_DIR" ]; then
    rm -rf -- "$TASK_TEMP_DIR"
  fi
}
trap cleanup EXIT

CONFIG_FILE="$TASK_TEMP_DIR/meta-inbox.production.json"
OFFLINE_FILE="$TASK_TEMP_DIR/app_offline.htm"
EXISTING_FILE="$TASK_TEMP_DIR/meta-inbox.production.existing.json"
TIMESTAMP="$(date -u +%Y%m%d-%H%M%S)"

printf 'Volt %s API is applying protected Meta Inbox configuration. Please retry shortly.\n' "$TARGET_LABEL" > "$OFFLINE_FILE"

echo "Checking the deployed $TARGET_LABEL API..."
lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" <<LFTP_CHECK
set ftp:ssl-allow no
cls "$REMOTE_DIR/Volt.API.dll"
bye
LFTP_CHECK

# Download the current remote configuration if present. A missing first-time
# configuration is expected, so this one read is allowed to fail.
lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" >/dev/null 2>&1 <<LFTP_BACKUP || true
set ftp:ssl-allow no
get "$REMOTE_DIR/meta-inbox.production.json" -o "$EXISTING_FILE"
bye
LFTP_BACKUP

VERIFY_TOKEN="$(openssl rand -hex 32)"
META_WHATSAPP_ACCESS_TOKEN=""
WHATSAPP_PHONE_NUMBER_ID=""
WHATSAPP_BUSINESS_ACCOUNT_ID=""
if [ -s "$EXISTING_FILE" ]; then
  EXISTING_FILE="$EXISTING_FILE" node <<'NODE' > "$TASK_TEMP_DIR/existing-values.txt"
const fs = require("node:fs");
try {
  const configuration = JSON.parse(fs.readFileSync(process.env.EXISTING_FILE, "utf8"));
  const section = configuration?.MetaInbox ?? {};
  process.stdout.write(`${section.VerifyToken ?? ""}\n${section.PageAccessToken ?? ""}\n${section.WhatsAppAccessToken ?? ""}\n${section.WhatsAppPhoneNumberId ?? ""}\n${section.WhatsAppBusinessAccountId ?? ""}\n${section.WhatsAppEmbeddedSignupConfigurationId ?? ""}\n`);
} catch {
  process.stdout.write("\n\n\n\n\n\n");
}
NODE
  EXISTING_VERIFY_TOKEN="$(sed -n '1p' "$TASK_TEMP_DIR/existing-values.txt")"
  EXISTING_PAGE_ACCESS_TOKEN="$(sed -n '2p' "$TASK_TEMP_DIR/existing-values.txt")"
  META_WHATSAPP_ACCESS_TOKEN="$(sed -n '3p' "$TASK_TEMP_DIR/existing-values.txt")"
  WHATSAPP_PHONE_NUMBER_ID="$(sed -n '4p' "$TASK_TEMP_DIR/existing-values.txt")"
  WHATSAPP_BUSINESS_ACCOUNT_ID="$(sed -n '5p' "$TASK_TEMP_DIR/existing-values.txt")"
  WHATSAPP_EMBEDDED_SIGNUP_CONFIGURATION_ID="$(sed -n '6p' "$TASK_TEMP_DIR/existing-values.txt")"
  if [ -n "$EXISTING_VERIFY_TOKEN" ]; then VERIFY_TOKEN="$EXISTING_VERIFY_TOKEN"; fi
  if [ -z "$META_PAGE_ACCESS_TOKEN" ] && [ -n "$EXISTING_PAGE_ACCESS_TOKEN" ]; then
    META_PAGE_ACCESS_TOKEN="$EXISTING_PAGE_ACCESS_TOKEN"
  fi
fi

WHATSAPP_EMBEDDED_SIGNUP_CONFIGURATION_ID="${WHATSAPP_EMBEDDED_SIGNUP_CONFIGURATION_ID:-}"

export CONFIG_FILE GRAPH_API_VERSION VERIFY_TOKEN META_APP_ID META_APP_SECRET META_PAGE_ACCESS_TOKEN META_WHATSAPP_ACCESS_TOKEN WHATSAPP_PHONE_NUMBER_ID WHATSAPP_BUSINESS_ACCOUNT_ID WHATSAPP_EMBEDDED_SIGNUP_CONFIGURATION_ID OAUTH_REDIRECT_URI
node <<'NODE'
const fs = require("node:fs");
const configuration = {
  MetaInbox: {
    Enabled: true,
    AppId: process.env.META_APP_ID,
    AppSecret: process.env.META_APP_SECRET,
    VerifyToken: process.env.VERIFY_TOKEN,
    PageAccessToken: process.env.META_PAGE_ACCESS_TOKEN,
    WhatsAppAccessToken: process.env.META_WHATSAPP_ACCESS_TOKEN,
    WhatsAppPhoneNumberId: process.env.WHATSAPP_PHONE_NUMBER_ID,
    WhatsAppBusinessAccountId: process.env.WHATSAPP_BUSINESS_ACCOUNT_ID,
    WhatsAppEmbeddedSignupConfigurationId: process.env.WHATSAPP_EMBEDDED_SIGNUP_CONFIGURATION_ID,
    WhatsAppOAuthRedirectUri: process.env.OAUTH_REDIRECT_URI,
    GraphApiVersion: process.env.GRAPH_API_VERSION,
  },
};
fs.writeFileSync(process.env.CONFIG_FILE, `${JSON.stringify(configuration, null, 2)}\n`, { mode: 0o600 });
JSON.parse(fs.readFileSync(process.env.CONFIG_FILE, "utf8"));
NODE

if [ -s "$EXISTING_FILE" ]; then
  echo "Backing up the existing remote configuration..."
  lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" <<LFTP_UPLOAD_BACKUP
set ftp:ssl-allow no
put "$EXISTING_FILE" -o "$REMOTE_DIR/meta-inbox.production.json.$TIMESTAMP.bak"
bye
LFTP_UPLOAD_BACKUP
fi

echo "Uploading configuration and restarting only the $TARGET_LABEL API..."
lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" <<LFTP_UPLOAD
set ftp:ssl-allow no
put "$OFFLINE_FILE" -o "$REMOTE_DIR/app_offline.htm"
sleep 5
put "$CONFIG_FILE" -o "$REMOTE_DIR/meta-inbox.production.json"
rm "$REMOTE_DIR/app_offline.htm"
bye
LFTP_UPLOAD

echo "Waiting for the $TARGET_LABEL API to start..."
sleep 5
CHALLENGE="volt-meta-webhook-ok"
echo "Testing callback: $CALLBACK_URL"
echo "Verify token: $VERIFY_TOKEN"
RESPONSE="$(curl --fail --silent --show-error --get "$CALLBACK_URL" \
  --connect-timeout 5 \
  --max-time 15 \
  --retry 6 \
  --retry-delay 3 \
  --retry-max-time 45 \
  --retry-all-errors \
  --data-urlencode "hub.mode=subscribe" \
  --data-urlencode "hub.verify_token=$VERIFY_TOKEN" \
  --data-urlencode "hub.challenge=$CHALLENGE")"

if [ "$RESPONSE" != "$CHALLENGE" ]; then
  echo "Webhook verification returned an unexpected response: $RESPONSE" >&2
  exit 1
fi

echo
echo "Meta Inbox webhook configuration is ready."
echo "Callback URL: $CALLBACK_URL"
echo "Verify token: $VERIFY_TOKEN"
echo "Subscribe the Page object to: messages, message_deliveries, message_reads"
echo "Subscribe the WhatsApp Business Account object to all Coexistence fields:"
echo "  messages"
echo "  history"
echo "  smb_message_echoes"
echo "  smb_app_state_sync"
echo "The WABA app subscription and these webhook-field subscriptions are separate settings in Meta."
if [ -z "$META_PAGE_ACCESS_TOKEN" ]; then
  echo "Page Access Token is still pending. Incoming webhooks can be configured now; rerun this installer after Meta issues the Page token to enable replies."
fi
