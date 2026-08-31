#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

export REMOTE_DIR="/apivoltaz"
export ASPNETCORE_ENVIRONMENT="Production"

exec "$SCRIPT_DIR/deploy-api-ftp.sh"
