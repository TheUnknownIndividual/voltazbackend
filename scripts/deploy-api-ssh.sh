#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"

CONFIGURATION="${CONFIGURATION:-Release}"
PROJECT_PATH="$ROOT_DIR/Volt.API/Volt.API.csproj"
SOLUTION_PATH="$ROOT_DIR/Volt.sln"
DEPLOY_TARGET="${DEPLOY_TARGET:-}"
ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-}"
DATABASE_NAME="${DATABASE_NAME:-}"

SSH_HOST="${SSH_HOST:-136.243.98.218}"
SSH_USER="${SSH_USER:-voltdeploy}"
SSH_KEY="${SSH_KEY:-$HOME/.ssh/volt_az_deploy}"

case "$DEPLOY_TARGET" in
  test)
    REMOTE_TARGET="C:/inetpub/wwwroot/testapivoltaz"
    HEALTH_URL="https://test.api.volt.az/api/seo/robots.txt"
    PACKAGE_PREFIX="testapivoltaz"
    ;;
  production)
    REMOTE_TARGET="C:/inetpub/wwwroot/apivoltaz"
    HEALTH_URL="https://api.volt.az/api/seo/robots.txt"
    PACKAGE_PREFIX="apivoltaz"
    if [ "${CONFIRM_PRODUCTION_DEPLOY:-}" != "YES" ]; then
      echo "Production deployment requires CONFIRM_PRODUCTION_DEPLOY=YES." >&2
      exit 1
    fi
    ;;
  *)
    echo "DEPLOY_TARGET must be test or production." >&2
    exit 1
    ;;
esac

if [[ ! "$ASPNETCORE_ENVIRONMENT" =~ ^(Staging|Production)$ ]]; then
  echo "ASPNETCORE_ENVIRONMENT must be Staging or Production." >&2
  exit 1
fi

if [ -n "$DATABASE_NAME" ] && [[ ! "$DATABASE_NAME" =~ ^[A-Za-z0-9_-]+$ ]]; then
  echo "Invalid DATABASE_NAME." >&2
  exit 1
fi

for command_name in dotnet perl zip scp ssh curl; do
  command -v "$command_name" >/dev/null || {
    echo "$command_name is required but was not found." >&2
    exit 1
  }
done

if [ ! -f "$SSH_KEY" ]; then
  echo "SSH key was not found: $SSH_KEY" >&2
  exit 1
fi

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
  [[ "$TELEGRAM_BOT_TOKEN" =~ ^[A-Za-z0-9_:-]+$ ]] || { echo "Invalid TELEGRAM_BOT_TOKEN format." >&2; exit 1; }
  [[ "$TELEGRAM_BOT_USERNAME" =~ ^[A-Za-z0-9_]+$ ]] || { echo "Invalid TELEGRAM_BOT_USERNAME format." >&2; exit 1; }
  [[ "$TELEGRAM_BOT_LINK_KEY" =~ ^[A-Fa-f0-9]{32,}$ ]] || { echo "TELEGRAM_BOT_LINK_KEY must be a 32+ character hexadecimal value." >&2; exit 1; }
fi

cd "$ROOT_DIR"

echo "Restoring backend packages..."
dotnet restore "$SOLUTION_PATH"

echo "Building backend ($CONFIGURATION)..."
dotnet build "$SOLUTION_PATH" \
  --configuration "$CONFIGURATION" \
  --no-restore \
  --disable-build-servers

DEPLOY_TEMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/volt-api-ssh.XXXXXX")"
PUBLISH_DIR="$DEPLOY_TEMP_DIR/publish"
PACKAGE_NAME="$PACKAGE_PREFIX-api-$(date +%Y%m%d-%H%M%S).zip"
PACKAGE_PATH="$DEPLOY_TEMP_DIR/$PACKAGE_NAME"

cleanup() {
  rm -rf -- "$DEPLOY_TEMP_DIR"
}
trap cleanup EXIT

echo "Publishing backend..."
dotnet publish "$PROJECT_PATH" \
  --configuration "$CONFIGURATION" \
  --no-restore \
  --disable-build-servers \
  --output "$PUBLISH_DIR" \
  /p:UseAppHost=false

if [ -n "$DATABASE_NAME" ]; then
  BASE_SETTINGS_FILE="$PUBLISH_DIR/appsettings.json"
  ENVIRONMENT_SETTINGS_FILE="$PUBLISH_DIR/appsettings.$ASPNETCORE_ENVIRONMENT.json"

  if [ ! -f "$BASE_SETTINGS_FILE" ] || [ ! -f "$ENVIRONMENT_SETTINGS_FILE" ]; then
    echo "Required appsettings files are missing from the publish output." >&2
    exit 1
  fi

  export DATABASE_NAME BASE_SETTINGS_FILE
  perl -0pi -e '
    open my $source, "<", $ENV{BASE_SETTINGS_FILE}
      or die "Could not read base appsettings.json\n";
    local $/;
    my $base = <$source>;
    close $source;

    $base =~ /^\s*"DefaultConnection"\s*:\s*"([^"]+)"/m
      or die "Could not find the active base DefaultConnection\n";
    my $connection = $1;
    $connection =~ s/(Database=)[^;]+/$1$ENV{DATABASE_NAME}/i
      or die "Could not set the database name\n";

    s{^(\s*"DefaultConnection"\s*:\s*")[^"]*(")}
      {$1 . $connection . $2}em
      or die "Could not replace the environment DefaultConnection\n";
  ' "$ENVIRONMENT_SETTINGS_FILE"

  grep -q "Database=$DATABASE_NAME;" "$ENVIRONMENT_SETTINGS_FILE" || {
    echo "Environment database override validation failed." >&2
    exit 1
  }
fi

export ASPNETCORE_ENVIRONMENT
perl -0pi -e '
  my $variables = qq{\n        <environmentVariables>\n          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="$ENV{ASPNETCORE_ENVIRONMENT}" />\n        </environmentVariables>\n      };
  s{(<aspNetCore\b[^>]*?)\s*/>}{$1>$variables</aspNetCore>}s
    or die "Could not add ASPNETCORE_ENVIRONMENT to web.config\n";
' "$PUBLISH_DIR/web.config"

if [ "$TELEGRAM_CONFIG_COUNT" -eq 3 ]; then
  printf '{\n  "TelegramBot": {\n    "BotToken": "%s",\n    "BotUsername": "%s",\n    "LinkApiKey": "%s"\n  }\n}\n' \
    "$TELEGRAM_BOT_TOKEN" "$TELEGRAM_BOT_USERNAME" "$TELEGRAM_BOT_LINK_KEY" \
    > "$PUBLISH_DIR/telegram.production.json"
fi

if [ ! -f "$PUBLISH_DIR/Volt.API.dll" ] || [ ! -f "$PUBLISH_DIR/web.config" ]; then
  echo "Published API output is incomplete; deployment stopped." >&2
  exit 1
fi

echo "Compressing API package..."
(
  cd "$PUBLISH_DIR"
  zip -qr "$PACKAGE_PATH" . -x '*.pdb' '*.xml' '*.map' '__MACOSX/*' '.DS_Store'
)

SSH_OPTIONS=(
  -i "$SSH_KEY"
  -o IdentitiesOnly=yes
)

echo "Uploading $PACKAGE_NAME over SSH..."
scp "${SSH_OPTIONS[@]}" \
  "$PACKAGE_PATH" \
  "$SCRIPT_DIR/install-api-ssh.ps1" \
  "$SSH_USER@$SSH_HOST:"

echo "Installing API package..."
ssh "${SSH_OPTIONS[@]}" "$SSH_USER@$SSH_HOST" \
  "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%USERPROFILE%/install-api-ssh.ps1\" -PackagePath \"%USERPROFILE%/$PACKAGE_NAME\" -TargetPath \"$REMOTE_TARGET\""

ssh "${SSH_OPTIONS[@]}" "$SSH_USER@$SSH_HOST" \
  "powershell.exe -NoProfile -Command \"if (Test-Path -LiteralPath '%USERPROFILE%/$PACKAGE_NAME') { Write-Error 'API installer did not consume the uploaded package.'; exit 1 }\""

echo "Waiting for API startup and migrations..."
curl --fail --silent --show-error \
  --connect-timeout 5 \
  --max-time 15 \
  --retry 12 \
  --retry-delay 3 \
  --retry-max-time 90 \
  --retry-all-errors \
  "$HEALTH_URL" >/dev/null

echo "SSH API deployment finished successfully: $HEALTH_URL"
