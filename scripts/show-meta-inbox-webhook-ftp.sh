#!/usr/bin/env bash
set -euo pipefail

REMOTE_DIR="${REMOTE_DIR:-/testapivoltaz}"
CALLBACK_URL="${CALLBACK_URL:-https://test.api.volt.az/api/meta-inbox/webhook}"

command -v lftp >/dev/null || { echo "lftp is required. On macOS: brew install lftp" >&2; exit 1; }
command -v node >/dev/null || { echo "node is required." >&2; exit 1; }
command -v curl >/dev/null || { echo "curl is required." >&2; exit 1; }

if [ -z "${FTP_HOST:-}" ]; then read -r -p "FTP host: " FTP_HOST; fi
if [ -z "${FTP_USER:-}" ]; then read -r -p "FTP username: " FTP_USER; fi
if [ -z "${FTP_PASS:-}" ]; then read -r -s -p "FTP password: " FTP_PASS; echo; fi

[ -n "$FTP_HOST" ] || { echo "FTP host cannot be empty." >&2; exit 1; }
[ -n "$FTP_USER" ] || { echo "FTP username cannot be empty." >&2; exit 1; }
[ -n "$FTP_PASS" ] || { echo "FTP password cannot be empty." >&2; exit 1; }
[[ "$REMOTE_DIR" == /testapivoltaz ]] || { echo "Refusing to read an unexpected remote directory: $REMOTE_DIR" >&2; exit 1; }
[[ "$CALLBACK_URL" == https://test.api.volt.az/* ]] || { echo "Refusing to test an unexpected callback URL: $CALLBACK_URL" >&2; exit 1; }

TASK_TEMP_DIR="$(mktemp -d)"
CONFIG_FILE="$TASK_TEMP_DIR/meta-inbox.production.json"
cleanup() {
  unset FTP_PASS VERIFY_TOKEN
  if [ -n "${TASK_TEMP_DIR:-}" ] && [ -d "$TASK_TEMP_DIR" ]; then
    rm -rf -- "$TASK_TEMP_DIR"
  fi
}
trap cleanup EXIT
chmod 700 "$TASK_TEMP_DIR"

echo "Reading the protected test webhook configuration..."
lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" >/dev/null <<LFTP_DOWNLOAD
set ftp:ssl-allow no
get "$REMOTE_DIR/meta-inbox.production.json" -o "$CONFIG_FILE"
bye
LFTP_DOWNLOAD
chmod 600 "$CONFIG_FILE"

VERIFY_TOKEN="$(CONFIG_FILE="$CONFIG_FILE" node <<'NODE'
const fs = require('node:fs');
const configuration = JSON.parse(fs.readFileSync(process.env.CONFIG_FILE, 'utf8'));
const token = configuration?.MetaInbox?.VerifyToken;
if (typeof token !== 'string' || token.trim().length < 16) process.exit(2);
process.stdout.write(token.trim());
NODE
)" || { echo "The remote configuration does not contain a valid verify token." >&2; exit 1; }

CHALLENGE="volt-whatsapp-webhook-ok"
RESPONSE="$(curl --fail --silent --show-error --get "$CALLBACK_URL" \
  --connect-timeout 5 \
  --max-time 15 \
  --data-urlencode "hub.mode=subscribe" \
  --data-urlencode "hub.verify_token=$VERIFY_TOKEN" \
  --data-urlencode "hub.challenge=$CHALLENGE")"

if [ "$RESPONSE" != "$CHALLENGE" ]; then
  echo "The deployed callback did not accept the configured verify token." >&2
  exit 1
fi

echo
echo "WhatsApp webhook verification is ready."
echo "Callback URL: $CALLBACK_URL"
if [ "${COPY_VERIFY_TOKEN:-0}" = "1" ]; then
  command -v pbcopy >/dev/null || { echo "pbcopy is required to copy the token on macOS." >&2; exit 1; }
  printf '%s' "$VERIFY_TOKEN" | pbcopy
  echo "Verify token: copied to clipboard (the value is intentionally hidden)"
else
  echo "Verify token: $VERIFY_TOKEN"
fi
echo "Client certificate: Off"
echo "After Verify and Save, subscribe the WhatsApp webhook to: messages"
