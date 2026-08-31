#!/usr/bin/env bash
set -euo pipefail

META_INBOX_TARGET="${META_INBOX_TARGET:-test}"
case "$META_INBOX_TARGET" in
  test) REMOTE_DIR="/testapivoltaz"; REDIRECT_URI="https://test.volt.az/admin-dashboard/whatsapp-setup"; API_URL="https://test.api.volt.az"; TARGET_LABEL="test" ;;
  production) REMOTE_DIR="/apivoltaz"; REDIRECT_URI="https://volt.az/admin-dashboard/whatsapp-setup"; API_URL="https://api.volt.az"; TARGET_LABEL="production" ;;
  *) echo "META_INBOX_TARGET must be test or production." >&2; exit 1 ;;
esac
META_APP_ID="2182171352343231"

command -v lftp >/dev/null || { echo "lftp is required. On macOS: brew install lftp" >&2; exit 1; }
command -v node >/dev/null || { echo "node is required." >&2; exit 1; }
command -v curl >/dev/null || { echo "curl is required." >&2; exit 1; }

if [ -z "${FTP_HOST:-}" ]; then read -r -p "FTP host: " FTP_HOST; fi
if [ -z "${FTP_USER:-}" ]; then read -r -p "FTP username: " FTP_USER; fi
if [ -z "${FTP_PASS:-}" ]; then read -r -s -p "FTP password: " FTP_PASS; echo; fi
read -r -p "WhatsApp Embedded Signup Configuration ID: " CONFIGURATION_ID

[ -n "$FTP_HOST" ] || { echo "FTP host cannot be empty." >&2; exit 1; }
[ -n "$FTP_USER" ] || { echo "FTP username cannot be empty." >&2; exit 1; }
[ -n "$FTP_PASS" ] || { echo "FTP password cannot be empty." >&2; exit 1; }
[[ "$CONFIGURATION_ID" =~ ^[0-9]+$ ]] || { echo "Configuration ID must contain digits only." >&2; exit 1; }

TASK_TEMP_DIR="$(mktemp -d)"
EXISTING_FILE="$TASK_TEMP_DIR/meta-inbox.production.existing.json"
CONFIG_FILE="$TASK_TEMP_DIR/meta-inbox.production.json"
OFFLINE_FILE="$TASK_TEMP_DIR/app_offline.htm"
TIMESTAMP="$(date -u +%Y%m%d-%H%M%S)"
cleanup() {
  unset FTP_PASS CONFIGURATION_ID
  if [ -n "${TASK_TEMP_DIR:-}" ] && [ -d "$TASK_TEMP_DIR" ]; then rm -rf -- "$TASK_TEMP_DIR"; fi
}
trap cleanup EXIT
chmod 700 "$TASK_TEMP_DIR"

echo "Reading protected $TARGET_LABEL Meta Inbox configuration..."
lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" >/dev/null <<LFTP_DOWNLOAD
set ftp:ssl-allow no
get "$REMOTE_DIR/meta-inbox.production.json" -o "$EXISTING_FILE"
bye
LFTP_DOWNLOAD
chmod 600 "$EXISTING_FILE"

CONFIG_FILE="$CONFIG_FILE" EXISTING_FILE="$EXISTING_FILE" META_APP_ID="$META_APP_ID" CONFIGURATION_ID="$CONFIGURATION_ID" REDIRECT_URI="$REDIRECT_URI" node <<'NODE'
const fs = require('node:fs');
const configuration = JSON.parse(fs.readFileSync(process.env.EXISTING_FILE, 'utf8'));
if (!configuration.MetaInbox?.AppSecret || !configuration.MetaInbox?.WhatsAppAccessToken) {
  throw new Error('The existing Meta App Secret or WhatsApp system-user token is missing.');
}
configuration.MetaInbox.AppId = process.env.META_APP_ID;
configuration.MetaInbox.WhatsAppEmbeddedSignupConfigurationId = process.env.CONFIGURATION_ID;
configuration.MetaInbox.WhatsAppOAuthRedirectUri = process.env.REDIRECT_URI;
configuration.MetaInbox.GraphApiVersion = 'v26.0';
fs.writeFileSync(process.env.CONFIG_FILE, `${JSON.stringify(configuration, null, 2)}\n`, { mode: 0o600 });
NODE
chmod 600 "$CONFIG_FILE"

printf 'Volt %s API is applying WhatsApp Embedded Signup configuration. Please retry shortly.\n' "$TARGET_LABEL" > "$OFFLINE_FILE"
echo "Backing up the existing configuration and restarting only the $TARGET_LABEL API..."
lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" >/dev/null <<LFTP_UPLOAD
set ftp:ssl-allow no
put "$EXISTING_FILE" -o "$REMOTE_DIR/meta-inbox.production.json.$TIMESTAMP.bak"
put "$OFFLINE_FILE" -o "$REMOTE_DIR/app_offline.htm"
sleep 5
put "$CONFIG_FILE" -o "$REMOTE_DIR/meta-inbox.production.json"
rm "$REMOTE_DIR/app_offline.htm"
bye
LFTP_UPLOAD

echo "Waiting for the $TARGET_LABEL API..."
sleep 5
curl --fail --silent --show-error --connect-timeout 5 --max-time 15 --retry 6 --retry-delay 3 --retry-all-errors "$API_URL/api/seo/robots.txt" >/dev/null

echo
echo "WhatsApp Embedded Signup $TARGET_LABEL configuration is ready."
echo "Redirect URI: $REDIRECT_URI"
echo "Admin page: $REDIRECT_URI"
echo "App ID: $META_APP_ID"
echo "Configuration ID: $CONFIGURATION_ID"
