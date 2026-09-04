#!/usr/bin/env bash
set -euo pipefail

META_INBOX_TARGET="${META_INBOX_TARGET:-test}"
case "$META_INBOX_TARGET" in
  test) EXPECTED_REMOTE_DIR="/testapivoltaz"; EXPECTED_CALLBACK_URL="https://test.api.volt.az/api/meta-inbox/webhook"; TARGET_LABEL="test" ;;
  production) EXPECTED_REMOTE_DIR="/apivoltaz"; EXPECTED_CALLBACK_URL="https://api.volt.az/api/meta-inbox/webhook"; TARGET_LABEL="production" ;;
  *) echo "META_INBOX_TARGET must be test or production." >&2; exit 1 ;;
esac
REMOTE_DIR="${REMOTE_DIR:-$EXPECTED_REMOTE_DIR}"
CALLBACK_URL="${CALLBACK_URL:-$EXPECTED_CALLBACK_URL}"

command -v lftp >/dev/null || { echo "lftp is required. On macOS: brew install lftp" >&2; exit 1; }
command -v node >/dev/null || { echo "node is required." >&2; exit 1; }
command -v curl >/dev/null || { echo "curl is required." >&2; exit 1; }

if [ -z "${FTP_HOST:-}" ]; then read -r -p "FTP host: " FTP_HOST; fi
if [ -z "${FTP_USER:-}" ]; then read -r -p "FTP username: " FTP_USER; fi
if [ -z "${FTP_PASS:-}" ]; then read -r -s -p "FTP password: " FTP_PASS; echo; fi
read -r -s -p "WhatsApp system-user access token: " WHATSAPP_ACCESS_TOKEN
echo
read -r -p "WhatsApp Phone Number ID: " WHATSAPP_PHONE_NUMBER_ID
read -r -p "WhatsApp Business Account ID (WABA ID): " WHATSAPP_BUSINESS_ACCOUNT_ID

[ -n "$FTP_HOST" ] || { echo "FTP host cannot be empty." >&2; exit 1; }
[ -n "$FTP_USER" ] || { echo "FTP username cannot be empty." >&2; exit 1; }
[ -n "$FTP_PASS" ] || { echo "FTP password cannot be empty." >&2; exit 1; }
[ ${#WHATSAPP_ACCESS_TOKEN} -ge 20 ] || { echo "The WhatsApp access token is too short." >&2; exit 1; }
[[ "$WHATSAPP_PHONE_NUMBER_ID" =~ ^[0-9]+$ ]] || { echo "Phone Number ID must contain digits only." >&2; exit 1; }
[[ "$WHATSAPP_BUSINESS_ACCOUNT_ID" =~ ^[0-9]+$ ]] || { echo "WABA ID must contain digits only." >&2; exit 1; }
[[ "$REMOTE_DIR" == "$EXPECTED_REMOTE_DIR" ]] || { echo "Refusing to configure an unexpected $TARGET_LABEL directory: $REMOTE_DIR" >&2; exit 1; }
[[ "$CALLBACK_URL" == "$EXPECTED_CALLBACK_URL" ]] || { echo "Refusing to configure an unexpected $TARGET_LABEL callback: $CALLBACK_URL" >&2; exit 1; }

TASK_TEMP_DIR="$(mktemp -d)"
EXISTING_FILE="$TASK_TEMP_DIR/meta-inbox.production.existing.json"
CONFIG_FILE="$TASK_TEMP_DIR/meta-inbox.production.json"
OFFLINE_FILE="$TASK_TEMP_DIR/app_offline.htm"
CURL_CONFIG="$TASK_TEMP_DIR/curl-auth.conf"
TIMESTAMP="$(date -u +%Y%m%d-%H%M%S)"
cleanup() {
  unset FTP_PASS WHATSAPP_ACCESS_TOKEN VERIFY_TOKEN
  if [ -n "${TASK_TEMP_DIR:-}" ] && [ -d "$TASK_TEMP_DIR" ]; then
    rm -rf -- "$TASK_TEMP_DIR"
  fi
}
trap cleanup EXIT
chmod 700 "$TASK_TEMP_DIR"

echo "Reading the existing protected Meta Inbox configuration..."
lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" >/dev/null <<LFTP_DOWNLOAD
set ftp:ssl-allow no
get "$REMOTE_DIR/meta-inbox.production.json" -o "$EXISTING_FILE"
bye
LFTP_DOWNLOAD
chmod 600 "$EXISTING_FILE"

CONFIG_FILE="$CONFIG_FILE" \
EXISTING_FILE="$EXISTING_FILE" \
WHATSAPP_ACCESS_TOKEN="$WHATSAPP_ACCESS_TOKEN" \
WHATSAPP_PHONE_NUMBER_ID="$WHATSAPP_PHONE_NUMBER_ID" \
WHATSAPP_BUSINESS_ACCOUNT_ID="$WHATSAPP_BUSINESS_ACCOUNT_ID" \
node <<'NODE'
const fs = require('node:fs');
const existing = JSON.parse(fs.readFileSync(process.env.EXISTING_FILE, 'utf8'));
if (!existing.MetaInbox || typeof existing.MetaInbox !== 'object') {
  throw new Error('The remote MetaInbox configuration section is missing.');
}
if (!existing.MetaInbox.AppSecret || !existing.MetaInbox.VerifyToken) {
  throw new Error('The existing App Secret or Verify Token is missing.');
}
existing.MetaInbox.WhatsAppAccessToken = process.env.WHATSAPP_ACCESS_TOKEN;
existing.MetaInbox.WhatsAppPhoneNumberId = process.env.WHATSAPP_PHONE_NUMBER_ID;
existing.MetaInbox.WhatsAppBusinessAccountId = process.env.WHATSAPP_BUSINESS_ACCOUNT_ID;
fs.writeFileSync(process.env.CONFIG_FILE, `${JSON.stringify(existing, null, 2)}\n`, { mode: 0o600 });
NODE
chmod 600 "$CONFIG_FILE"

CONFIG_FILE="$CONFIG_FILE" node <<'NODE' > "$TASK_TEMP_DIR/meta-values.txt"
const fs = require('node:fs');
const section = JSON.parse(fs.readFileSync(process.env.CONFIG_FILE, 'utf8')).MetaInbox;
process.stdout.write(`${section.VerifyToken}\n${section.GraphApiVersion || 'v26.0'}\n`);
NODE
VERIFY_TOKEN="$(sed -n '1p' "$TASK_TEMP_DIR/meta-values.txt")"
GRAPH_API_VERSION="$(sed -n '2p' "$TASK_TEMP_DIR/meta-values.txt")"

printf 'header = "Authorization: Bearer %s"\n' "$WHATSAPP_ACCESS_TOKEN" > "$CURL_CONFIG"
chmod 600 "$CURL_CONFIG"
echo "Validating the WhatsApp token and Phone Number ID with Meta..."
PHONE_RESPONSE="$(curl --fail-with-body --silent --show-error \
  --connect-timeout 5 --max-time 20 \
  --config "$CURL_CONFIG" \
  "https://graph.facebook.com/${GRAPH_API_VERSION}/${WHATSAPP_PHONE_NUMBER_ID}?fields=id,display_phone_number,verified_name")"
PHONE_RESPONSE="$PHONE_RESPONSE" EXPECTED_ID="$WHATSAPP_PHONE_NUMBER_ID" node <<'NODE'
const response = JSON.parse(process.env.PHONE_RESPONSE);
if (response.error) throw new Error(response.error.message || 'Meta rejected the WhatsApp credentials.');
if (String(response.id || '') !== process.env.EXPECTED_ID) throw new Error('Meta returned a different Phone Number ID.');
process.stdout.write(`Meta phone verified: ${response.display_phone_number || response.id} (${response.verified_name || 'name pending'})\n`);
NODE

printf 'Volt %s API is applying protected WhatsApp Inbox configuration. Please retry shortly.\n' "$TARGET_LABEL" > "$OFFLINE_FILE"
echo "Backing up the existing remote configuration..."
lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" >/dev/null <<LFTP_BACKUP
set ftp:ssl-allow no
put "$EXISTING_FILE" -o "$REMOTE_DIR/meta-inbox.production.json.$TIMESTAMP.bak"
bye
LFTP_BACKUP

echo "Uploading WhatsApp configuration and restarting only the $TARGET_LABEL API..."
lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" >/dev/null <<LFTP_UPLOAD
set ftp:ssl-allow no
put "$OFFLINE_FILE" -o "$REMOTE_DIR/app_offline.htm"
sleep 5
put "$CONFIG_FILE" -o "$REMOTE_DIR/meta-inbox.production.json"
rm "$REMOTE_DIR/app_offline.htm"
bye
LFTP_UPLOAD

echo "Waiting for the $TARGET_LABEL API to start..."
sleep 5
CHALLENGE="volt-whatsapp-config-ok"
RESPONSE="$(curl --fail --silent --show-error --get "$CALLBACK_URL" \
  --connect-timeout 5 --max-time 15 \
  --retry 6 --retry-delay 3 --retry-max-time 45 --retry-all-errors \
  --data-urlencode "hub.mode=subscribe" \
  --data-urlencode "hub.verify_token=$VERIFY_TOKEN" \
  --data-urlencode "hub.challenge=$CHALLENGE")"
[ "$RESPONSE" = "$CHALLENGE" ] || { echo "The webhook callback returned an unexpected response." >&2; exit 1; }

echo "Subscribing the Meta app to the WhatsApp Business Account..."
SUBSCRIPTION_RESPONSE="$(curl --fail-with-body --silent --show-error \
  --request POST \
  --connect-timeout 5 --max-time 20 \
  --config "$CURL_CONFIG" \
  "https://graph.facebook.com/${GRAPH_API_VERSION}/${WHATSAPP_BUSINESS_ACCOUNT_ID}/subscribed_apps")"
SUBSCRIPTION_RESPONSE="$SUBSCRIPTION_RESPONSE" node <<'NODE'
const response = JSON.parse(process.env.SUBSCRIPTION_RESPONSE);
if (response.error) throw new Error(response.error.message || 'Meta rejected the WABA subscription.');
if (response.success !== true) throw new Error('Meta did not confirm the WABA subscription.');
NODE

echo
echo "WhatsApp Inbox $TARGET_LABEL configuration is ready."
echo "Callback URL: $CALLBACK_URL"
echo "Phone Number ID: $WHATSAPP_PHONE_NUMBER_ID"
echo "WABA ID: $WHATSAPP_BUSINESS_ACCOUNT_ID"
echo "WABA app subscription: active"
echo "Next in Meta, subscribe the WhatsApp Business Account object to all Coexistence fields:"
echo "  messages"
echo "  history"
echo "  smb_message_echoes"
echo "  smb_app_state_sync"
echo "The active WABA app subscription above does not replace these webhook-field subscriptions."
