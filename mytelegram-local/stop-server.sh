#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$HERE/docker"
[ -f .env ] || { echo "Сервер ещё не запускался"; exit 0; }
docker compose --env-file .env down
