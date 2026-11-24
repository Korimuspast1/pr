#!/bin/sh
# Создаёт ключ подписи для APK: android/keystore/deadinstinct.p12 + password.txt рядом.
# Ключ создаётся ОДИН раз; при обновлении игры используйте тот же ключ, иначе игрокам
# придётся удалять приложение (сохранения пропадут). В git ключ не попадает (.gitignore).
#   BUILD_SECRET — если задан (например, секрет репозитория), пароль берётся из него,
#                  иначе генерируется случайный.
set -e
ROOT=$(cd "$(dirname "$0")/.." && pwd)
KS_DIR=$ROOT/android/keystore
KS=$KS_DIR/deadinstinct.p12
mkdir -p "$KS_DIR"
if [ -f "$KS" ]; then echo "ключ уже есть: $KS"; exit 0; fi
PASS=${BUILD_SECRET:-$(od -An -N15 -tx1 /dev/urandom | tr -d ' \n')}
T=${TMPDIR:-/tmp}/di-keygen
rm -rf "$T"; mkdir -p "$T"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out "$T/key.pem" 2>/dev/null
openssl req -new -x509 -key "$T/key.pem" -subj "/CN=Dead Instinct" -days 18250 -sha256 -out "$T/cert.pem" 2>/dev/null
openssl pkcs12 -export -inkey "$T/key.pem" -in "$T/cert.pem" -out "$KS" -name deadinstinct -passout "pass:$PASS"
rm -rf "$T"
printf '%s' "$PASS" > "$KS_DIR/password.txt"
echo "создан ключ подписи: $KS"
