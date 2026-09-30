#!/usr/bin/env bash
# Запуск локального MyTelegram test server.
# Usage: ./start-server.sh 192.168.1.25
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_DIR="$HERE/docker"
TEMPLATE="$COMPOSE_DIR/.env.example"
ENV_FILE="$COMPOSE_DIR/.env"

SERVER_IP="${1:-${MYTELEGRAM_IP:-}}"
if [ -z "$SERVER_IP" ]; then
  SERVER_IP="$(hostname -I 2>/dev/null | awk '{print $1}')"
fi
SERVER_IP="${SERVER_IP:-127.0.0.1}"

if [[ "$SERVER_IP" == *[[:space:]/\\]* ]]; then
  echo "ERROR: invalid server IP: $SERVER_IP" >&2
  exit 1
fi
if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR: Docker не найден. Нужен Docker Engine и docker compose." >&2
  echo "На обычном Android/Termux Docker обычно не работает; используйте Linux ПК/VPS." >&2
  exit 1
fi
if ! docker compose version >/dev/null 2>&1; then
  echo "ERROR: нужен Docker Compose v2 (команда: docker compose)" >&2
  exit 1
fi

mkdir -p "$HERE/data"
# .env создаётся локально и игнорируется Git.
sed "s/__MYTELEGRAM_IP__/$SERVER_IP/g" "$TEMPLATE" > "$ENV_FILE"

# Test-only mode: no SMS provider and a fixed local verification code.
if ! grep -q '^App__FixedVerifyCode=22222$' "$ENV_FILE"; then
  echo "ERROR: template lost App__FixedVerifyCode=22222" >&2
  exit 1
fi

cd "$COMPOSE_DIR"
echo "==> MyTelegram test server: $SERVER_IP"
echo "==> local verification code: 22222"
echo "==> ports: 20443, 20543, 20643, 20644, 30443, 30444"
echo "==> first startup downloads Docker images and may take several minutes"
docker compose --env-file "$ENV_FILE" up -d

echo
docker compose --env-file "$ENV_FILE" ps
