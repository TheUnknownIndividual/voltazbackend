#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"

CONFIGURATION="${CONFIGURATION:-Release}"
PROJECT_PATH="$ROOT_DIR/Volt.API/Volt.API.csproj"
SOLUTION_PATH="$ROOT_DIR/Volt.sln"
DEFAULT_PUBLISH_DIR="$ROOT_DIR/publish/api"
PUBLISH_DIR="${PUBLISH_DIR:-$DEFAULT_PUBLISH_DIR}"
REMOTE_DIR="${REMOTE_DIR:-/apivoltaz}"

: "${FTP_USER:?Set FTP_USER before running this script.}"
: "${FTP_PASS:?Set FTP_PASS before running this script.}"
: "${FTP_HOST:?Set FTP_HOST before running this script.}"

# Optional production-only Telegram configuration. These values are written to
# telegram.production.json in the API root, never appsettings.json or the
# frontend bundle. The remote file remains in place during later normal FTP
# deploys, because mirror does not delete files absent from a new publish.
TELEGRAM_CONFIG_COUNT=0
for name in TELEGRAM_BOT_TOKEN TELEGRAM_BOT_USERNAME TELEGRAM_BOT_LINK_KEY; do
  if [ -n "${!name:-}" ]; then
    TELEGRAM_CONFIG_COUNT=$((TELEGRAM_CONFIG_COUNT + 1))
  fi
done

if [ "$TELEGRAM_CONFIG_COUNT" -ne 0 ] && [ "$TELEGRAM_CONFIG_COUNT" -ne 3 ]; then
  echo "Set TELEGRAM_BOT_TOKEN, TELEGRAM_BOT_USERNAME, and TELEGRAM_BOT_LINK_KEY together." >&2
  exit 1
fi

if [ "$TELEGRAM_CONFIG_COUNT" -eq 3 ]; then
  # Restrict values to the character sets used by BotFather tokens, Telegram
  # usernames, and a hex shared key before writing JSON without interpolation.
  [[ "$TELEGRAM_BOT_TOKEN" =~ ^[A-Za-z0-9_:-]+$ ]] || { echo "Invalid TELEGRAM_BOT_TOKEN format." >&2; exit 1; }
  [[ "$TELEGRAM_BOT_USERNAME" =~ ^[A-Za-z0-9_]+$ ]] || { echo "Invalid TELEGRAM_BOT_USERNAME format." >&2; exit 1; }
  [[ "$TELEGRAM_BOT_LINK_KEY" =~ ^[A-Fa-f0-9]{32,}$ ]] || { echo "TELEGRAM_BOT_LINK_KEY must be a 32+ character hexadecimal value." >&2; exit 1; }
fi

if [ -n "${ASPNETCORE_ENVIRONMENT:-}" ] && [[ ! "$ASPNETCORE_ENVIRONMENT" =~ ^(Development|Staging|Production)$ ]]; then
  echo "Invalid ASPNETCORE_ENVIRONMENT." >&2
  exit 1
fi

command -v dotnet >/dev/null || {
  echo "dotnet is required but was not found in PATH." >&2
  exit 1
}

command -v lftp >/dev/null || {
  echo "lftp is required but was not found in PATH." >&2
  exit 1
}

if [ -z "$PUBLISH_DIR" ]; then
  echo "PUBLISH_DIR resolved to an empty path. Using $DEFAULT_PUBLISH_DIR instead." >&2
  PUBLISH_DIR="$DEFAULT_PUBLISH_DIR"
fi

case "$PUBLISH_DIR" in
  "$ROOT_DIR"/publish/*) ;;
  *)
    echo "Refusing to clean PUBLISH_DIR outside $ROOT_DIR/publish: $PUBLISH_DIR" >&2
    exit 1
    ;;
esac

echo "Publish directory: $PUBLISH_DIR"

echo "Restoring packages..."
dotnet restore "$SOLUTION_PATH"

echo "Building $SOLUTION_PATH ($CONFIGURATION)..."
dotnet build "$SOLUTION_PATH" --configuration "$CONFIGURATION" --no-restore

echo "Publishing $PROJECT_PATH to $PUBLISH_DIR..."
rm -rf "$PUBLISH_DIR"
dotnet publish "$PROJECT_PATH" \
  --configuration "$CONFIGURATION" \
  --no-restore \
  --output "$PUBLISH_DIR" \
  /p:UseAppHost=false

if [ -n "${ASPNETCORE_ENVIRONMENT:-}" ]; then
  export ASPNETCORE_ENVIRONMENT
  perl -0pi -e '
    my $variables = qq{\n        <environmentVariables>\n          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="$ENV{ASPNETCORE_ENVIRONMENT}" />\n        </environmentVariables>\n      };
    s{(<aspNetCore\b[^>]*?)\s*/>}{$1>$variables</aspNetCore>}s
      or die "Could not add ASPNETCORE_ENVIRONMENT to web.config\n";
  ' "$PUBLISH_DIR/web.config"
fi

if [ "$TELEGRAM_CONFIG_COUNT" -eq 3 ]; then
  echo "Adding Telegram production configuration to the publish output..."
  printf '{\n  "TelegramBot": {\n    "BotToken": "%s",\n    "BotUsername": "%s",\n    "LinkApiKey": "%s"\n  }\n}\n' \
    "$TELEGRAM_BOT_TOKEN" "$TELEGRAM_BOT_USERNAME" "$TELEGRAM_BOT_LINK_KEY" \
    > "$PUBLISH_DIR/telegram.production.json"
fi

echo "Upload preview:"
if [ ! -d "$PUBLISH_DIR" ]; then
  echo "Publish directory does not exist after dotnet publish: $PUBLISH_DIR" >&2
  exit 1
fi

find "$PUBLISH_DIR" -maxdepth 2 -type f | sed "s#^$PUBLISH_DIR/##" | sort

APP_OFFLINE_FILE="$PUBLISH_DIR/app_offline.htm"
printf '%s\n' 'Volt API is updating. Please retry in a moment.' > "$APP_OFFLINE_FILE"

echo "Mirroring publish output to $FTP_HOST:$REMOTE_DIR..."
lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" <<LFTP_COMMANDS
set ftp:ssl-allow no
put "$APP_OFFLINE_FILE" -o "$REMOTE_DIR/app_offline.htm"
sleep 10
lcd "$PUBLISH_DIR"
mirror -R --upload-older --no-perms --verbose \
  --exclude-glob .git \
  --exclude-glob .git/** \
  --exclude-glob .github \
  --exclude-glob .github/** \
  --exclude-glob .vscode \
  --exclude-glob .vscode/** \
  --exclude-glob bin \
  --exclude-glob bin/** \
  --exclude-glob obj \
  --exclude-glob obj/** \
  --exclude-glob publish \
  --exclude-glob publish/** \
  --exclude-glob '*.cs' \
  --exclude-glob '*.csproj' \
  --exclude-glob '*.sln' \
  --exclude-glob '*.user' \
  --exclude-glob '*.pubxml.user' \
  --exclude-glob '*.md' \
  --exclude-glob '*.http' \
  --exclude-glob '*.log' \
  . "$REMOTE_DIR"
rm "$REMOTE_DIR/app_offline.htm"
bye
LFTP_COMMANDS

echo "Done."
