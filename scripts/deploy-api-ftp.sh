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

echo "Upload preview:"
if [ ! -d "$PUBLISH_DIR" ]; then
  echo "Publish directory does not exist after dotnet publish: $PUBLISH_DIR" >&2
  exit 1
fi

find "$PUBLISH_DIR" -maxdepth 2 -type f | sed "s#^$PUBLISH_DIR/##" | sort

echo "Mirroring publish output to $FTP_HOST:$REMOTE_DIR..."
lftp -u "$FTP_USER","$FTP_PASS" "$FTP_HOST" <<LFTP_COMMANDS
set ftp:ssl-allow no
lcd "$PUBLISH_DIR"
mirror -R --only-newer --no-perms --verbose \
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
bye
LFTP_COMMANDS

echo "Done."
