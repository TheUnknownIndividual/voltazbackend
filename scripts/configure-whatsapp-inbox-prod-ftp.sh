#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
export META_INBOX_TARGET="production"
exec "$SCRIPT_DIR/configure-whatsapp-inbox-ftp.sh"
