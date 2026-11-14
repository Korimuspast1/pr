#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$HERE/docker"
[ -f .env ] || { echo "Сначала запустите ./start-server.sh"; exit 1; }
docker compose --env-file .env ps
