# Формат `.ttydyn` (TikTok You dynamic module)

`.ttydyn` — ZIP-контейнер dynamic dex-модуля TikTok You. Стабильная часть мода
проверяет контейнер, хеш payload и ECDSA-подпись, затем вызывает точку входа
`com.windukk.mod.Main`.

## Структура контейнера

Для `payloadType: full` используется такой минимальный набор записей:

```text
manifest.json     JSON-метаданные и хеши
classes.dex       основной Dalvik DEX
signature.der     ECDSA-подпись manifest.json
```

Дополнительные `classes2.dex`, `classes3.dex` поддерживаются инструментом и должны
быть перечислены в `dexFiles` и `hashes`. Сборщик делает записи ZIP с датой
`1980-01-01 00:00:00`, чтобы результат был воспроизводимым.

Имя файла не является подписью и не влияет на содержимое. Для совместимости можно
использовать `profile-flex.ttydyn` или официальную схему
`tty-<variant>-<sha256>-<payloadType>_<id>.ttydyn`.

## `manifest.json`

Пример:

```json
{
  "payloadType": "full",
  "variant": "release",
  "dynamicVersion": 1790688535,
  "stableApiVersion": 1788704106,
  "targetDexSha256": "<64 hex>",
  "targetSize": 16476,
  "dexFiles": [
    {
      "name": "classes.dex",
      "payload": "classes.dex",
      "targetSha256": "<64 hex>",
      "targetSize": 16476
    }
  ],
  "hashes": {
    "classes.dex": "<64 hex>"
  }
}
```

| Поле | Смысл |
| --- | --- |
| `payloadType` | `full` — модуль заменяет весь dynamic dex мода |
| `variant` | канал сборки, для этого проекта `release` |
| `dynamicVersion` | целое число версии dynamic-модуля; обычно Unix time или номер сборки |
| `stableApiVersion` | минимальная версия API стабильной части APK |
| `targetDexSha256` / `targetSize` | хеш и размер первого DEX |
| `dexFiles[]` | список DEX и их индивидуальные хеши/размеры |
| `hashes` | карта `имя записи ZIP → SHA-256` |

Подпись вычисляется по **точным байтам** `manifest.json`, без форматирования или
пересортировки JSON. Поэтому нельзя менять JSON после создания `signature.der`.

## Подпись

`signature.der` — стандартный DER `SEQUENCE { INTEGER r, INTEGER s }` для ECDSA
на P-256 (`secp256r1`) с SHA-256. Для каждого INTEGER используется положительная
минимальная DER-форма; перед значением с установленным старшим битом добавляется
нулевой байт.

Проверка локального пакета:

```bash
python3 tools/ttydyn.py verify release/profile-flex.ttydyn --pub keys/dev.pub.pem
```

Инструмент проверяет ZIP, JSON, наличие DEX, все размеры и SHA-256, DER и подпись.
`inspect` дополнительно проверяет заголовок DEX и наличие
`com.windukk.mod.Main`.

## Ключи проекта

`tools/ttydyn.py keygen --out keys/dev` создаёт:

- `keys/dev.key.pem` — приватный scalar P-256, права `0600`, не добавлять в Git;
- `keys/dev.pub.pem` — 64 байта координат X+Y, публичный ключ для APK.

Перед сборкой `profile-flex/build.sh` проверяет, что оба файла являются парой. Он
не генерирует новый ключ незаметно: новая подпись с новым ключом будет отвергнута
APK, в который вшит старый публичный ключ.

## Точка входа и API

В DEX должен быть публичный класс:

```java
package com.windukk.mod;

public final class Main {
    public static void start(android.app.Application app,
                             android.content.Context context) {
        // запуск модуля
    }
}
```

Стабильная часть APK предоставляет dynamic dex следующие классы:

```java
// com.windukk.hook.HookBridge
static Set<Unhook> hookAllMethods(Class<?> clazz, String name, HookMethod cb);
static Set<Unhook> hookAllConstructors(Class<?> clazz, HookMethod cb);
static Unhook hookMethod(Member member, HookMethod cb);
static Object invokeOriginalMethod(Member member, Object thiz, Object[] args);

// com.windukk.hook.HookMethod
void beforeHookedMethod(HookMethodParam p);
void afterHookedMethod(HookMethodParam p);
// HookMethodParam: thisObject, args, method, getResult(), setResult(Object)

// com.windukk.stable.ResourceOverlay
static InputStream open(Context context, String name);
```

Заглушки этих классов находятся в `profile-flex/stubs` только для компиляции и
удаляются до запуска `d8`; в готовом DEX их реализаций быть не должно.

## Подпись официального APK

Официальная сборка TikTok You проверяет подпись по своему публичному ключу,
зашитому в APK/native library. Публичный ключ из этого проекта подходит только
для APK, собранного с ним. Восстановить официальный приватный ключ из `pubkey`
нельзя, поэтому перепаковка или переименование `.ttydyn` не может обойти ошибку
подписи.

## `payloadType: full`

`full` — не патч. Установленный модуль заменяет dynamic dex целиком. Чтобы вернуть
функции исходного мода, сохраните его оригинальный `.ttydyn`. Profile Flex также
может перед запуском своих хуков загрузить `classes.dex` из файла
`profile_flex_base.dex` в external-files каталоге приложения, если этот файл
положен пользователем вручную.
