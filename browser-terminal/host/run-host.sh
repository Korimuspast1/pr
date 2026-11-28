#!/usr/bin/env bash
# Обёртка-хост: браузер требует абсолютный путь до исполняемого файла,
# поэтому в манифесте хоста указывается путь до этого скрипта.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if command -v python3 >/dev/null 2>&1; then
  PY=python3
else
  PY=python
fi
exec "$PY" "$DIR/host.py" "$@"
