#!/bin/sh
# Статическая проверка: все скрипты игры склеиваются в порядке подключения и проверяются как один файл
# (в браузере у них общая глобальная область). Ловит обращения к несуществующим именам и мёртвый код.
# Нужен eslint 8: npm i eslint@8.57.0 (каталог с node_modules передаётся в KC_TOOLS).
T=${KC_TOOLS:-$HOME/.cache/kc-tools}
D=$(dirname "$0")/../www/js
OUT=/tmp/kc_all.js
: > $OUT
for f in data map game realm ai terrain render audio ui ui2 main; do echo "/* FILE:$f */" >> $OUT; sed "1s/^'use strict';//" $D/$f.js >> $OUT; done
cat > /tmp/kc_eslint.json <<'J'
{ "root": true, "env": { "es2018": true }, "globals": { "window": "readonly", "document": "readonly", "localStorage": "readonly", "performance": "readonly", "requestAnimationFrame": "readonly", "setTimeout": "readonly", "setInterval": "readonly", "clearInterval": "readonly", "clearTimeout": "readonly", "console": "readonly", "Image": "readonly", "devicePixelRatio": "readonly", "navigator": "readonly", "btoa": "readonly", "atob": "readonly", "ImageData": "readonly" }, "parserOptions": { "ecmaVersion": 2018, "sourceType": "script" },
  "rules": { "no-undef": "error", "no-unused-vars": ["warn", { "args": "none", "caughtErrors": "none" }], "no-redeclare": "error", "no-dupe-keys": "error", "no-unreachable": "error", "no-const-assign": "error", "no-func-assign": "error", "no-self-assign": "error", "no-dupe-args": "error", "no-duplicate-case": "error", "use-isnan": "error", "valid-typeof": "error", "no-cond-assign": "error", "no-fallthrough": "warn", "no-shadow-restricted-names": "error", "no-unsafe-negation": "error", "no-sparse-arrays": "error", "no-constant-condition": ["warn", { "checkLoops": false }], "no-loss-of-precision": "error", "no-implied-eval": "error", "no-eval": "error", "no-new-func": "error" } }
J
"$T/node_modules/.bin/eslint" --no-eslintrc -c /tmp/kc_eslint.json --format unix $OUT
