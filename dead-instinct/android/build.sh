#!/bin/sh
# Сборка APK без Gradle: aapt2 -> javac -> d8 -> zipalign -> apksigner.
# Нужны: JDK 11+, Android build-tools 34 и платформа android-34 (android.jar).
#   SDK=<каталог>  — где лежат bt/android-14 (build-tools) и pf/android-34 (платформа); по умолчанию ~/.cache/android-sdk
# Как скачать их без Android Studio — см. android/README.md.
# Запуск: sh android/build.sh   →   release/DeadInstinct-<версия>.apk
set -e
ROOT=$(cd "$(dirname "$0")/.." && pwd)
SDK=${SDK:-$HOME/.cache/android-sdk}
BT=${BUILD_TOOLS:-$SDK/bt/android-14}
JAR=${ANDROID_JAR:-$SDK/pf/android-34/android.jar}
[ -d /usr/lib/jvm/jdk-11/bin ] && PATH=/usr/lib/jvm/jdk-11/bin:$PATH
VERSION=$(sed -n "s/.*GAME_VERSION = '\([0-9.]*\)'.*/\1/p" "$ROOT/www/js/data.js" | head -1)
[ -n "$VERSION" ] || { echo "не найдена версия игры (GAME_VERSION в www/js/data.js)"; exit 1; }
# код версии: 1.0.0 -> 10000, 1.1.3 -> 10103; у каждого обновления он должен расти
CODE=$(echo "$VERSION" | awk -F. '{ printf "%d", $1 * 10000 + $2 * 100 + $3 }')
W=${TMPDIR:-/tmp}/di-apk
KS_DIR=$ROOT/android/keystore
KS=$KS_DIR/deadinstinct.p12
OUT=$ROOT/release/DeadInstinct-$VERSION.apk
for t in "$BT/aapt2" "$BT/d8" "$BT/zipalign" "$BT/apksigner" "$JAR"; do [ -e "$t" ] || { echo "не найдено: $t"; exit 1; }; done

rm -rf "$W"; mkdir -p "$W/gen" "$W/classes" "$W/dex" "$W/assets" "$ROOT/release"

# ключ подписи создаётся один раз (см. android/keygen.sh). Обновление ставится поверх старой
# версии только при том же ключе — берегите его!
if [ ! -f "$KS" ]; then sh "$ROOT/android/keygen.sh" || exit 1; fi

echo "1/6 ресурсы"
"$BT/aapt2" compile --dir "$ROOT/android/res" -o "$W/res.zip"
cp -R "$ROOT/www" "$W/assets/www"
find "$W/assets/www" -name '*.map' -delete
echo "2/6 упаковка"
"$BT/aapt2" link -o "$W/base.apk" -I "$JAR" --manifest "$ROOT/android/AndroidManifest.xml" -A "$W/assets" --java "$W/gen" \
  --min-sdk-version 23 --target-sdk-version 34 --version-code "$CODE" --version-name "$VERSION" "$W/res.zip"
echo "3/6 компиляция"
javac -encoding UTF-8 -nowarn -Xlint:-options -source 8 -target 8 -bootclasspath "$JAR" -classpath "$JAR" -d "$W/classes" \
  $(find "$W/gen" "$ROOT/android/java" -name '*.java')
echo "4/6 dex"
"$BT/d8" --release --min-api 23 --lib "$JAR" --output "$W/dex" $(find "$W/classes" -name '*.class')
(cd "$W/dex" && zip -q -X "$W/base.apk" classes.dex)
echo "5/6 выравнивание"
"$BT/zipalign" -f -p 4 "$W/base.apk" "$W/aligned.apk"
echo "6/6 подпись"
"$BT/apksigner" sign --ks "$KS" --ks-type PKCS12 --ks-key-alias deadinstinct --ks-pass "file:$KS_DIR/password.txt" \
  --v1-signing-enabled true --v2-signing-enabled true --v3-signing-enabled true --out "$OUT" "$W/aligned.apk"
rm -f "$OUT.idsig"
"$BT/apksigner" verify --min-sdk-version 23 "$OUT"
echo "готово: $OUT ($(wc -c < "$OUT") байт), версия $VERSION (код $CODE)"
