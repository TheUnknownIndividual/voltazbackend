#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
TARGET="${1:-}"
SSH_HOST="${SSH_HOST:-136.243.98.218}"
SSH_USER="${SSH_USER:-voltdeploy}"
SSH_KEY="${SSH_KEY:-$HOME/.ssh/volt_az_deploy}"

case "$TARGET" in
  test) REMOTE_TARGET="C:/inetpub/wwwroot/testapivoltaz" ;;
  production) REMOTE_TARGET="C:/inetpub/wwwroot/apivoltaz" ;;
  *) echo "Usage: $0 test|production" >&2; exit 1 ;;
esac

for command_name in node scp ssh; do
  command -v "$command_name" >/dev/null || { echo "$command_name is required." >&2; exit 1; }
done
if [ ! -f "$SSH_KEY" ]; then
  echo "SSH key was not found: $SSH_KEY" >&2
  exit 1
fi

read -r -s -p "Paste the NEW OpenAI project API key: " PRODUCT_AI_API_KEY
echo
read -r -p "Model [gpt-5.6]: " PRODUCT_AI_MODEL
PRODUCT_AI_MODEL="${PRODUCT_AI_MODEL:-gpt-5.6}"

if [[ "$PRODUCT_AI_API_KEY" != sk-* ]]; then
  unset PRODUCT_AI_API_KEY
  echo "The value does not look like an OpenAI project API key." >&2
  exit 1
fi
if [[ ! "$PRODUCT_AI_MODEL" =~ ^[A-Za-z0-9._-]+$ ]]; then
  unset PRODUCT_AI_API_KEY
  echo "Invalid model name." >&2
  exit 1
fi

CONFIG_DIR="$(mktemp -d "${TMPDIR:-/tmp}/volt-product-ai.XXXXXX")"
CONFIG_NAME="product-ai-config-$(date +%Y%m%d-%H%M%S).json"
CONFIG_PATH="$CONFIG_DIR/$CONFIG_NAME"
cleanup() {
  unset PRODUCT_AI_API_KEY
  rm -rf -- "$CONFIG_DIR"
}
trap cleanup EXIT
umask 077
export PRODUCT_AI_API_KEY PRODUCT_AI_MODEL CONFIG_PATH
node <<'NODE'
const fs = require('fs');
const config = {
  ProductAiImport: {
    Enabled: true,
    ApiKey: process.env.PRODUCT_AI_API_KEY,
    Model: process.env.PRODUCT_AI_MODEL,
    TrustedAssetBaseUrl: 'https://cloudfiles.volt.az/',
    MaxFiles: 10,
    MaxCombinedBytes: 52428800,
    DraftLifetimeHours: 168,
    RequestTimeoutSeconds: 180,
    MaxConcurrentJobs: 2,
    ReasoningEffort: 'low',
    MaxOutputTokens: 16000,
  },
};
fs.writeFileSync(process.env.CONFIG_PATH, `${JSON.stringify(config, null, 2)}\n`, { mode: 0o600 });
NODE

SSH_OPTIONS=(-i "$SSH_KEY" -o IdentitiesOnly=yes)
echo "Uploading protected Product AI configuration over SSH..."
scp "${SSH_OPTIONS[@]}" \
  "$CONFIG_PATH" \
  "$SCRIPT_DIR/configure-product-ai-ssh.ps1" \
  "$SSH_USER@$SSH_HOST:"

ssh "${SSH_OPTIONS[@]}" "$SSH_USER@$SSH_HOST" \
  "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%USERPROFILE%/configure-product-ai-ssh.ps1\" -SourceConfigPath \"%USERPROFILE%/$CONFIG_NAME\" -TargetPath \"$REMOTE_TARGET\""

echo "Waiting for the API to restart..."
if [ "$TARGET" = "test" ]; then
  HEALTH_URL="https://test.api.volt.az/api/Products/ai-imports/settings"
else
  HEALTH_URL="https://api.volt.az/api/Products/ai-imports/settings"
fi
echo "Configured $TARGET Product AI. Verify from the protected admin product page after the API restarts."
echo "Settings endpoint (requires admin login): $HEALTH_URL"
