#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

export DEPLOY_TARGET="production"
export ASPNETCORE_ENVIRONMENT="Production"
unset DATABASE_NAME

exec "$SCRIPT_DIR/deploy-api-ssh.sh"
