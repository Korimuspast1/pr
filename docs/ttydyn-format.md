# Формат `.ttydyn` (TikTok You dynamic module)

Разбор сделан по реальному файлу
`tty-release-076794860d55-full_a3d2bf65139f4637bd64.ttydyn`
(493 529 байт, репозиторий [Korimuspast1/Ttydyn](https://github.com/Korimuspast1/Ttydyn)).
Публичной документации по формату нет — всё ниже получено из самого файла.

## Что это такое

`.ttydyn` — контейнер **OTA-обновления кода мода TikTok You** («OTA-обновления компонентов
мода» из ченджлога v4.2). APK мода содержит только «стабильную» часть (загрузчик + хук-движок
`exteraHook`), а вся логика мода живёт в динамическом модуле, который можно обновлять
и импортировать вручную (в моде есть пункт **«Установить .ttydyn» / «Install .ttydyn»**).

## Контейнер

Обычный ZIP (deflate), 3 записи, детерминированная сборка — все даты `1980-01-01 00:00:00`:

```
manifest.json     459 B    метаданные и хеши
classes.dex       876 048  весь код мода (Dalvik, DEX 038)
signature.der     72 B     ECDSA-подпись (DER SEQUENCE{r,s}, кривая P-256)
```

Имя файла: `tty-<variant>-<первые 12 hex sha256 декса>-<payloadType>_<16 hex id сборки>.ttydyn`.

## manifest.json

```json
{
  "payloadType": "full",
  "variant": "release",
  "dynamicVersion": 1789062391,
  "stableApiVersion": 1788704106,
  "targetDexSha256": "076794860d55662d995982301a23709fec50388132f0abea60893a66ff826c77",
  "targetSize": 876048,
  "dexFiles": [
    {"name": "classes.dex", "payload": "classes.dex",
     "targetSha256": "0767...c77", "targetSize": 876048}
  ],
  "hashes": {"classes.dex": "0767...c77"}
}
```

| Поле | Смысл |
| --- | --- |
| `payloadType` | `full` — модуль полностью заменяет динамический код мода |
| `variant` | канал сборки (`release`) |
| `dynamicVersion` | версия модуля, unix-время (1789062391 → 2026-09-10 17:46:31 UTC) |
| `stableApiVersion` | минимальная версия API стабильной части APK (2026-09-06 14:15:06 UTC); стабильная часть отдаёт её через `DynamicUpdates.stableApiVersion()` |
| `targetDexSha256` / `targetSize` | контроль целостности основного декса |
| `dexFiles[]` | список дексов: имя класса-контейнера, имя записи в ZIP, хеш, размер |
| `hashes` | плоская карта «запись ZIP → sha256» |

Хеши проверены: sha256(`classes.dex`) совпадает с `targetDexSha256`, размеры сходятся.

## API стабильной части (то, на что дексу можно опираться)

Ссылки на эти классы есть в `classes.dex`, реализация — в APK мода:

```java
// com.windukk.hook.HookBridge — движок exteraHook
static Set<Unhook>  hookAllMethods(Class<?> clazz, String name, HookMethod cb);
static Set<Unhook>  hookAllConstructors(Class<?> clazz, HookMethod cb);
static Unhook       hookMethod(Member m, HookMethod cb);
static Object       invokeOriginalMethod(Member m, Object thiz, Object[] args);
static boolean      deoptimizeMethod(Member m);
static boolean      disableHiddenApiRestrictions();
static boolean      pauseNativeLoggingForSnapshot();
static void         resumeNativeLoggingAfterSnapshot();

// com.windukk.hook.HookMethod
void beforeHookedMethod(HookMethodParam p);
void afterHookedMethod(HookMethodParam p);
// HookMethodParam: поля thisObject, args, method; методы getResult()/setResult(Object)

// com.windukk.hook.ReplaceMethod extends HookMethod
static ReplaceMethod returnConstant(Object value);
static final ReplaceMethod DO_NOTHING;

// com.windukk.stable.DynamicUpdates — сам механизм OTA
static DynamicStatus getStatus(Context c);
static long   workingDynamicVersion(Context c);
static long   highestAcceptedVersion(Context c);
static int    stableApiVersion();
static void   importFile(Context c, Uri uri);      // импорт .ttydyn из проводника
static void   importPackage(Context c, File file);

// com.windukk.stable.ResourceOverlay
static InputStream open(Context c, String name);
```

## Точка входа

В дексе определён класс **`com.windukk.mod.Main`** со статическим методом

```java
public static void start(Android.app.Application app, android.content.Context ctx)
```

— его и вызывает стабильная часть после проверки подписи и загрузки декса.
Остальные 753 класса модуля обфусцированы в пакет `Z`, Kotlin/kotlinx/OkHttp
перепакованы в `ni.shikatu.*` (shading), что подтверждает Kotlin-происхождение мода.

Из строк декса видно, какие классы TikTok мод хукает, в частности
`com.ss.android.ugc.aweme.profile.model.User` (методы `getUid`, `getNickname`),
`com.ss.android.ugc.aweme.feed.model.Aweme`, `AwemeStatistics`, `FeedItemList`,
`com.ss.android.ugc.aweme.comment.model.Comment`, `UserProfileInfo`, `ProfileHeaderBaseComponent`.

## Подпись

`signature.der` — 70–72 байта, `30 46 02 21 00 …` = DER `SEQUENCE { INTEGER r, INTEGER s }`,
размер r/s по 32 байта ⇒ **ECDSA на P-256 (secp256r1)**, обычно с SHA-256.
В стабильной части есть строки `Проверка подписи и подготовка файла…`,
`dynamic_import_signed`, `verifySignatureNative` — подпись проверяется нативно,
публичный ключ зашит в APK/`.so`.

**Практическое следствие:** самосборный `.ttydyn` не пройдёт проверку подписи официальной
сборки мода — приватного ключа авторов у нас нет. Модуль из этого репозитория
подписывается вашим собственным ключом и рассчитан на:

- отладочные/собственные сборки мода, где проверка ключа отключена или ключ заменён на ваш;
- изучение формата и разработку своих модулей.

## Важное предупреждение

`payloadType: "full"` означает «этот декс — весь динамический код мода». Установив свой
модуль, вы **замените** функциональность TikTok You (регион, скачивание, фильтры и т.д.)
на то, что реализовано в вашем дексе. Храните оригинальный `.ttydyn`, чтобы вернуть его назад.
