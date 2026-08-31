#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

export DEPLOY_TARGET="test"
export ASPNETCORE_ENVIRONMENT="Staging"
export DATABASE_NAME="VoltAz-Test-DB"

exec "$SCRIPT_DIR/deploy-api-ssh.sh"
