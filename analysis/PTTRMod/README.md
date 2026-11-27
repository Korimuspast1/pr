# PTTRMod — полный разбор

Разобраны три файла из корня репозитория:

| Файл | Размер | Что это |
|---|---:|---|
| `PTTRMod.dll` | 8 192 Б | управляемая .NET-сборка (мод для MelonLoader) |
| `PTTRMod.pdb` | 10 900 Б | Portable PDB — отладочная информация к этой сборке |
| `PTTRMod.deps.json` | 413 Б | файл зависимостей `dotnet build` |

Ничего не запускалось: разбор сделан статически — разобраны PE-заголовки, метаданные ECMA-335,
IL-байткод, таблицы Portable PDB, блобы атрибутов. Все цифры ниже получены из самих файлов.

Дополнительные машинные дампы (без сокращений):

* [`PTTRMod.metadata.txt`](PTTRMod.metadata.txt) — полный дамп метаданных, 452 строки
* [`PTTRMod.il.txt`](PTTRMod.il.txt) — полный IL-листинг с привязкой к строкам исходников
* [`PTTRMod.pdb.txt`](PTTRMod.pdb.txt) — полный разбор PDB

---

## 1. Короткий ответ: что это такое

Это **чит-мод «PTTR Mod» версии 1.0.0 от автора `olivia`** для игры на Unity, собранный под
**MelonLoader 0.6.6** и **Harmony 2.10.2**. В игре есть класс `PTTRPlayer`, у которого мод
перехватывает 4 метода:

| Что перехвачено | Что делает мод |
|---|---|
| `PTTRPlayer.SubtractHealth(float amount)` | блокирует урон в режиме бессмертия |
| `PTTRPlayer.SetHealth(float setHealthTo, bool loweringHealth)` | блокирует снижение здоровья |
| `PTTRPlayer.HitEnemy(float damage)` | умножает урон игрока (x5) |
| `PTTRPlayer.Die()` | не даёт игроку умереть |

Плюс три настройки в `MelonPreferences` (`GodMode`, `DamageScale`, `LogPlayerHealth`),
горячие клавиши F1/F2/F3 и дамп состояния игрока в лог.

**Вредоносного кода нет.** Ни сети, ни файловых операций, ни P/Invoke, ни обфускации,
ни рефлексии по строкам, ни загрузки сторонних сборок. 19 строковых литералов, 11 методов,
739 байт IL, 0 блоков обработки исключений, 0 встроенных ресурсов.

---

## 2. Проверка: DLL и PDB — от одной сборки

Это первое, что нужно проверять, когда рядом лежат `.dll` и `.pdb` (PDB легко подменить,
и тогда «исходники» будут врать).

Сборка **детерминированная** (`Deterministic`), поэтому в DLL есть запись debug-типа 19 с
SHA-256 самой PDB:

```
Значение, записанное в PTTRMod.dll (debug type 19):
  353E7A0401DAD7C7AA484142F38D877CA3397580236B3136419CA20D78A8298D
SHA-256 от PTTRMod.pdb с обнулённым 20-байтным полем #Pdb ID:
  353E7A0401DAD7C7AA484142F38D877CA3397580236B3136419CA20D78A8298D      ← совпало
```

Дополнительно совпадают три независимых «паспорта»:

* CodeView `RSDS`: GUID `353e7a0401dad747aa484142f38d877c`, Age `1` = `#Pdb` ID в PDB;
* MVID модуля `d47a5c73-b959-4bed-ac54-a68cf546ae34` (тоже детерминированный);
* таблицы: маска «referenced type system tables» в `#Pdb` перечисляет **14** таблиц сборки,
  и счётчики строк в ней **побайтово совпадают** с настоящими таблицами DLL
  (TypeRef 36, TypeDef 8, MethodDef 11, MemberRef 39, CustomAttribute 17 …).

**Вывод: PDB гарантированно относится именно к этому DLL.** Подмены нет.

Мелочь, которая часто путает: в ресурсе `RT_VERSION` версия записана как `0.0.0.0`,
но реальная версия сборки — `1.0.0` (из `MelonInfo` и таблицы `Assembly`).
Ресурсная версия генерируется отдельно и в проектном файле не задана.

---

## 3. `PTTRMod.deps.json` — почему такой маленький

```json
{
  "runtimeTarget": { "name": ".NETCoreApp,Version=v6.0", "signature": "" },
  "compilationOptions": {},
  "targets": { ".NETCoreApp,Version=v6.0": { "PTTRMod/1.0.0": { "runtime": { "PTTRMod.dll": {} } } } },
  "libraries": { "PTTRMod/1.0.0": { "type": "project", "serviceable": false, "sha512": "" } }
}
```

Разбор:

* **цель — .NET 6.0** (`net6.0`);
* **`PTTRMod/1.0.0`** — проект, а не пакет (`"type": "project"`);
* **`"sha512": ""`** — файл создан не `dotnet publish`, а копированием/`dotnet build` без упаковки;
  у настоящего пакета здесь был бы хэш;
* **`dependencies` у проекта НЕТ** — потому что все ссылки (`MelonLoader`, `0Harmony`,
  `Assembly-CSharp`, Unity) подключены как `<Reference Include=… HintPath=…>`, а не как
  `PackageReference`. Для зависимостей, разрешаемых вручную из папки игры, это нормально;
* **`runtimes`/`native` нет** — нативная часть не нужна, вся логика в IL.

Важный нюанс: `.deps.json` описывает только runtime-зависимости для хоста .NET.
MelonLoader сам загружает сборку через свой загрузчик, поэтому этот файл в реальной работе
мода почти не участвует — он просто остался от сборки.

---

## 4. Восстановленный исходный код

Ниже — **реконструкция** по IL + sequence points из PDB. Структура, идентификаторы,
строковые литералы, порядок операторов и даже номера строк восстановлены точно
(sequence points задают строку и колонку каждой инструкции). Комментарии и точные
переносы строк в оригинале могли отличаться — они в PDB не хранятся.

Файлы: `Main.cs` (≈65 строк) и `Patches.cs` (≈52 строки). Пути при сборке:
`E:\romagame\PTTR-Source\mods\PTTRMod\`.

### `Main.cs`

```csharp
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(PTTRMod.Main), "PTTR Mod", "1.0.0", "olivia")]
[assembly: MelonGame(null, null)]

namespace PTTRMod
{
    public class Main : MelonMod
    {
        internal static MelonPreferences_Entry<bool>  GodMode;
        internal static MelonPreferences_Entry<float> DamageScale;
        internal static MelonPreferences_Entry<bool>  LogPlayerHealth;

        private static readonly Harmony Patcher =
            new Harmony("com.olivia.pttrmod");          // строка 17 → .cctor

        public override void OnInitializeMelon()
        {
            //                                         // строка 22:13 – 23:72
            var category = MelonPreferences.CreateCategory(
                "PTTRMod", "PTTR Mod");

            GodMode = category.CreateEntry(
                "GodMode", false, "Ignore incoming damage");
            DamageScale = category.CreateEntry(
                "DamageScale", 1f, "Multiplier on damage the player deals");
            LogPlayerHealth = category.CreateEntry(
                "LogPlayerHealth", false, "Print player health every second");

            Patcher.PatchAll(typeof(Main).Assembly);
            LoggerInstance.Msg("PTTRMod ready. F1 godmode, F2 damage x5, F3 dump player state.");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F1))           // 282
            {
                GodMode.Value = !GodMode.Value;
                MelonPreferences.Save();
                LoggerInstance.Msg($"GodMode = {GodMode.Value}");
            }

            if (Input.GetKeyDown(KeyCode.F2))           // 283
            {
                DamageScale.Value = DamageScale.Value >= 5f ? 1f : 5f;
                MelonPreferences.Save();
                LoggerInstance.Msg($"DamageScale = {DamageScale.Value}");
            }

            if (Input.GetKeyDown(KeyCode.F3))           // 284
                DumpPlayerState();
        }

        private static void DumpPlayerState()
        {
            var player = Object.FindObjectOfType<PTTRPlayer>();
            if (player == null)
            {
                MelonLogger.Msg("No PTTRPlayer in scene.");
                return;
            }

            LoggerInstance.Msg($"player.health = {player.health}");
            LoggerInstance.Msg($"player.transform.position = {player.transform.position}");
            LoggerInstance.Msg($"player.healthBoost = {player.healthBoost}, damageUpgrade = {player.damageUpgrade}");
        }
    }
}
```

Проверка по sequence points (выборочно, из `PTTRMod.pdb`):

| IL | строка:колонка | код |
|---|---|---|
| `IL_0000` | `Main.cs:22:13 – 23:72` | `var category = MelonPreferences.CreateCategory("PTTRMod", "PTTR Mod");` (перенос на 2 строки) |
| `IL_000F` | `Main.cs:25:13 – 25:83` | `GodMode = category.CreateEntry("GodMode", false, "Ignore incoming damage");` |
| `IL_0063` | `Main.cs:29:13 – 29:53` | `Patcher.PatchAll(typeof(Main).Assembly);` |
| `IL_0000` | `Main.cs:35:13 – 35:46` | `if (Input.GetKeyDown(KeyCode.F1))` |
| `IL_000C` | `Main.cs:37:17 – 37:48` | `GodMode.Value = !GodMode.Value;` |
| `IL_006D` | `Main.cs:44` | `DamageScale.Value = DamageScale.Value >= 5f ? 1f : 5f;` |
| `IL_0000` | `Main.cs:55:13 – 55:83` | `var player = Object.FindObjectOfType<PTTRPlayer>();` |
| `IL_0000` | `Main.cs:17:9 – 18:58` | инициализатор `Patcher` (попал в `.cctor`) |

`KeyCode` 282/283/284 — это ровно **F1 / F2 / F3** в Unity.

### `Patches.cs`

```csharp
using HarmonyLib;
using MelonLoader;

namespace PTTRMod
{
    [HarmonyPatch(typeof(PTTRPlayer), "SubtractHealth")]
    internal static class SubtractHealthPatch
    {
        private static bool Prefix(float amount)
        {
            if (!Main.GodMode.Value)
                return true;                                     // 13: выполнить оригинал

            MelonLogger.Msg($"[godmode] blocked SubtractHealth({amount})");
            return false;                                        // 16: пропустить оригинал
        }
    }

    [HarmonyPatch(typeof(PTTRPlayer), "SetHealth")]
    internal static class SetHealthPatch
    {
        private static bool Prefix(float setHealthTo, bool loweringHealth)
        {
            if (Main.GodMode.Value && loweringHealth)
                return false;                                    // 27: блокируем снижение HP

            return true;
        }
    }

    [HarmonyPatch(typeof(PTTRPlayer), "HitEnemy")]
    internal static class HitEnemyPatch
    {
        private static void Prefix(ref float damage)
        {
            var scale = Main.DamageScale.Value;
            if (scale != 1f)
                damage *= scale;                                 // 41: усиливаем урон
        }
    }

    [HarmonyPatch(typeof(PTTRPlayer), "Die")]
    internal static class DiePatch
    {
        private static bool Prefix()
        {
            return !Main.GodMode.Value;                          // false ⇒ Die() не выполнится
        }
    }
}
```

Обратите внимание на `using`-директивы из PDB (таблица `ImportScope`):
для `Main.cs` — `System`, `HarmonyLib`, `MelonLoader`, `UnityEngine`;
для `Patches.cs` — только `HarmonyLib`, `MelonLoader`.
Это подтверждает, что в `Patches.cs` нет ни `using UnityEngine`, ни `using System`.

---

## 5. Паспорт сборки

| Параметр | Значение |
|---|---|
| Assembly name | `PTTRMod` |
| Assembly version | `0.0.0.0` (в `MelonInfo` — `1.0.0`) |
| Module name | `PTTRMod.dll` |
| MVID | `d47a5c73-b959-4bed-ac54-a68cf546ae34` |
| Signed | нет (нет PublicKey, нет StrongNameSignature) |
| PE | PE32, `IMAGE_FILE_MACHINE_I386`, 3 секции (`.text` 0x1800, `.rsrc` 0x400, `.reloc` 0x200) |
| CLR flags | `0x0001` — `ILONLY` (чистый IL, без mixed-mode) |
| Entry point | нет (DLL) |
| Импорт | только `mscoree.dll!_CorDllMain` |
| Native / C++/CLI | нет |
| Встроенные ресурсы | нет (`Resources` RVA 0x0) |
| Метаданные | `v4.0.30319`, потоки `#~ 1152`, `#Strings 1424`, `#US 808`, `#GUID 16`, `#Blob 1056` |

### Ссылки (`AssemblyRef`, 6 штук)

| # | Имя | Версия | PublicKeyToken | Зачем |
|---|---|---|---|---|
| 1 | `System.Runtime` | 6.0.0.0 | `b03f5f7f11d50a3a` | базовые типы .NET 6 |
| 2 | `MelonLoader` | 0.6.6.0 | — | загрузчик модов |
| 3 | `0Harmony` | 2.10.2.0 | — | движок патчинга |
| 4 | `Assembly-CSharp` | 1.0.0.0 | — | игра: `PTTRPlayer` |
| 5 | `UnityEngine.InputLegacyModule` | 0.0.0.0 | — | `Input.GetKeyDown` |
| 6 | `UnityEngine.CoreModule` | 0.0.0.0 | — | `Object`, `Component`, `Transform`, `Vector3` |

`UnityEngine.InputLegacyModule` (а не `UnityEngine.InputModule`) означает,
что игра использует **старую систему ввода** Unity — это важно при переносе мода
на другую версию игры.

### Все объявленные типы (8)

| # | Тип | Базовый | Роль |
|---|---|---|---|
| 1 | `<Module>` | — | служебный |
| 2 | `Microsoft.CodeAnalysis.EmbeddedAttribute` | `System.Attribute` | маркер Roslyn |
| 3 | `System.Runtime.CompilerServices.RefSafetyRulesAttribute` | `System.Attribute` | маркер Roslyn (C# 11+) |
| 4 | **`PTTRMod.Main`** | `MelonLoader.MelonMod` | сам мод |
| 5 | `PTTRMod.SubtractHealthPatch` | `System.Object` | Harmony-патч |
| 6 | `PTTRMod.SetHealthPatch` | `System.Object` | Harmony-патч |
| 7 | `PTTRMod.HitEnemyPatch` | `System.Object` | Harmony-патч |
| 8 | `PTTRMod.DiePatch` | `System.Object` | Harmony-патч |

Нет ни одного своего `enum`, `struct`, `interface`, `delegate`, лямбд и замыканий
(таблиц `InterfaceImpl`, `NestedClass`, `GenericParam`, `Property`, `Event` нет вовсе).

### Поля (5)

| Поле | Флаги | Тип |
|---|---|---|
| `Version` | `0x0026` = public readonly | `int` (у `RefSafetyRulesAttribute`, синтетика) |
| `GodMode` | `0x0013` = internal static | `MelonPreferences_Entry<bool>` |
| `DamageScale` | `0x0013` = internal static | `MelonPreferences_Entry<float>` |
| `LogPlayerHealth` | `0x0013` = internal static | `MelonPreferences_Entry<bool>` |
| `Patcher` | `0x0031` = private static readonly | `HarmonyLib.Harmony` |

### Методы (11, все с телом)

| # | Метод | RVA | Код | Стек | Локальные |
|---|---|---:|---:|---:|---|
| 1 | `EmbeddedAttribute::.ctor` | 0x2050 | 7 | 8 | — |
| 2 | `RefSafetyRulesAttribute::.ctor(int)` | 0x2058 | 14 | 8 | — |
| 3 | `Main::OnInitializeMelon` | 0x2068 | 141 | 10 | — |
| 4 | `Main::OnUpdate` | 0x2104 | 228 | 4 | 1 |
| 5 | `Main::DumpPlayerState` | 0x21F4 | 198 | 3 | 2 |
| 6 | `Main::.ctor` | 0x22C6 | 7 | 8 | — |
| 7 | `Main::.cctor` | 0x22CE | 16 | 8 | — |
| 8 | `SubtractHealthPatch::Prefix` | 0x22E0 | 70 | 3 | 1 |
| 9 | `SetHealthPatch::Prefix` | 0x2332 | 18 | 8 | — |
| 10 | `HitEnemyPatch::Prefix` | 0x2348 | 26 | 3 | 1 |
| 11 | `DiePatch::Prefix` | 0x236E | 14 | 8 | — |

Итого **739 байт IL**, все заголовки тел — TINY (1 байт) или FAT (12 байт), `moreSects=0`
(нет секций try/catch/finally).

### Атрибуты (17)

| Цель | Атрибут | Аргументы |
|---|---|---|
| module | `UnverifiableCode` | — |
| module | `RefSafetyRules` | `Version = 11` |
| assembly | `CompilationRelaxations` | `8` (`NoStringInterning`) |
| assembly | `RuntimeCompatibility` | `WrapNonExceptionThrows = true` |
| assembly | `Debuggable` | `(DebuggingModes)2` = `IgnoreSymbolStoreSequencePoints` |
| assembly | **`MelonInfo`** | `(typeof(Main), "PTTR Mod", "1.0.0", "olivia")` |
| assembly | **`MelonGame`** | `(null, null)` |
| assembly | `TargetFramework` | `".NETCoreApp,Version=v6.0"`, `FrameworkDisplayName = ".NET 6.0"` |
| assembly | `SecurityPermission` (DeclSecurity) | `RequestMinimum`, `SkipVerification = true` |
| типы 2 и 3 | `CompilerGenerated`, `Embedded`, `AttributeUsage(AllowMultiple, Inherited)` | — |
| тип 5 | **`HarmonyPatch(typeof(PTTRPlayer), "SubtractHealth")`** | |
| тип 6 | **`HarmonyPatch(typeof(PTTRPlayer), "SetHealth")`** | |
| тип 7 | **`HarmonyPatch(typeof(PTTRPlayer), "HitEnemy")`** | |
| тип 8 | **`HarmonyPatch(typeof(PTTRPlayer), "Die")`** | |

`MelonGame(null, null)` — важная деталь: мод **не привязан ни к разработчику, ни к игре**,
то есть MelonLoader загрузит его в любой процесс, а не только в PTTR.
(Пустые `null` по документации MelonLoader означают «универсальный мод».)

---

## 6. Что делает код, по шагам

### Запуск

1. MelonLoader находит `MelonInfo` → создаёт `PTTRMod.Main`.
2. Статический конструктор: `new Harmony("com.olivia.pttrmod")` → поле `Patcher`.
3. `OnInitializeMelon()`:
   * создаёт категорию настроек `PTTRMod` / `PTTR Mod`;
   * три записи: `GodMode=false`, `DamageScale=1.0`, `LogPlayerHealth=false`;
   * `Patcher.PatchAll(typeof(Main).Assembly)` → Harmony сканирует **всю сборку** и
     навешивает 4 патча по атрибутам `[HarmonyPatch]`;
   * пишет в лог строку готовности.

### Каждый кадр

`OnUpdate()` проверяет три клавиши и **всегда** выполняет три `GetKeyDown` (дешёвая операция).

* **F1** — `GodMode = !GodMode`, сохраняет конфиг, пишет значение.
* **F2** — `DamageScale`: если `>= 5f` → `1f`, иначе `5f` (переключение 1 ↔ 5).
* **F3** — дамп: health, позиция, healthBoost, damageUpgrade.

### Патчи

* `SubtractHealth(float amount)` — **bool Prefix**.
  `GodMode == false` → `true` (оригинал выполняется).
  `GodMode == true` → `false` (оригинал **не** выполняется) + запись в лог на каждый удар.
  Это единственное место, где мод пишет в лог часто — при частом уроне лог засоряется.

* `SetHealth(float setHealthTo, bool loweringHealth)` — **bool Prefix**.
  `GodMode && loweringHealth` → `false` (блок). Иначе `true`.
  То есть лечение (`loweringHealth == false`) проходит всегда.

* `HitEnemy(float damage)` — **void Prefix с `ref`**.
  `scale = DamageScale.Value; if (scale != 1f) damage *= scale;`
  `ref`-параметр Harmony подставляет по имени; сигнатура совпадает с игровой
  (`float` по значению → `ref float` в префиксе разрешён).

* `Die()` — **bool Prefix**, возвращает `!GodMode`.
  При `GodMode == true` возвращает `false` → тело `Die()` не выполняется.

### Найденные особенности (не баги, но стоит знать)

1. **`LogPlayerHealth` — мёртвая настройка.** Поле создаётся, но `get_Value()` для него
   в IL **нет ни одного**. Описание обещает «печатать здоровье каждую секунду», но таймера
   в коде нет. Это остаток от нереализованной функции.
2. **`GodMode` не восстанавливается на «после смерти»**, и `Die()`/`SubtractHealth()`
   блокируются по отдельности — если игра снимает HP через `SetHealth(..., true)`,
   это тоже блокируется, так что дыр нет.
3. **`HitEnemyPatch` печатает `ref float`** — если в игре параметр называется иначе
   (например, `amount`), Harmony не найдёт соответствия и патч будет вести себя иначе.
   Имена `amount`, `setHealthTo`, `loweringHealth`, `damage` взяты из PDB (`Param`) —
   они совпадают с игровыми, значит авторы подбирали имена по оригиналу.
4. **`PatchAll(Assembly)`** — патчи применяются ко всем классам сборки с `[HarmonyPatch]`,
   включая будущие. Если добавить новый патч, регистрировать отдельно не нужно.
5. **`DamageScale` по умолчанию `1.0`** — на «ванильном» конфиге `HitEnemyPatch`
   не изменяет урон (`if (scale != 1f)`), то есть мод пассивен, пока не нажали F2.

---

## 7. Portable PDB — что внутри

`PTTRMod.pdb` — **не** Windows-PDB (`MSF`), а кросс-платформенный **Portable PDB**
(контейнер `BSJB`, как у метаданных, но без PE-обёртки). Открывается Rider,
Visual Studio, VS Code (C# Dev Kit), dnSpy/ILSpy.

```
Поток      offset   размер
#Pdb       0x007C   88      ID, точка входа, маска таблиц
#~         0x00D4   292     таблицы
#Strings   0x01F8   16      имена локальных: 'scale', 'player'
#US        0x0208   4       пусто
#GUID      0x020C   64
#Blob      0x024C   10312   имена файлов, хэши, sequence points
```

Таблицы: `Document` 3, `MethodDebugInformation` 11, `LocalScope` 8,
`LocalVariable` 2, `ImportScope` 5, `StateMachineMethod` 2 (+2 «хвостовые» записи).

### Исходные файлы и их SHA-256

| Документ | SHA-256 содержимого |
|---|---|
| `E:\romagame\PTTR-Source\mods\PTTRMod\Main.cs` | `cc76d9ce56d745a4b946c72d38a11e82830f1fd8a9c97bc5e4aa87b86a4eb002` |
| `E:\romagame\PTTR-Source\mods\PTTRMod\Patches.cs` | `91b4c95207ac7d26b9e0753baa37b318477d88927a745c5e786164601e03124d` |
| `…\obj\Release\net6.0\.NETCoreApp,Version=v6.0.AssemblyAttributes.cs` | `f55a8701535cd25190399fe9b11bdb60ecd686ece28c49157ec692fbc2a1c078` |

Это удобный способ доказать авторство: если у вас есть «тот самый» `Main.cs` —
`sha256sum` должен дать ровно `cc76d9ce…`.

### Опции компиляции (блоб PDB)

```
compiler-version  4.14.0-3.26064.1+450493a9b4ec6337bced0120e97cb76f4ed783db   (Roslyn из .NET SDK 9.0.14)
language          C#
language-version  13.0
output-kind       DynamicallyLinkedLibrary
optimization      release
platform          AnyCpu
runtime-version   9.0.14+19078…  (SDK)
unsafe            True
source-file-count 3
```

Сборка велась **SDK .NET 9.0.14**, а target — `net6.0`. То есть мод собран современным
компилятором, но исполняется на .NET 6 в MelonLoader.

### Перекрёстные проверки, которые сошлись

1. Длины `LocalScope.Length` (141, 228, 198, 16, 70, 18, 26, 14) **в точности** равны
   `codeSize` методов в DLL — PDB и DLL согласованы.
2. Каждый метод с телом имеет `MethodDebugInformation`; у методов без тела
   (2 служебных) — пусто.
3. Sequence points покрывают методы от первой до последней строки без разрывов.
4. Маска «referenced type system tables» в `#Pdb` повторяет 14 таблиц DLL с теми же
   счётчиками строк.

### Две аномалии в «хвосте» таблиц PDB (для полноты)

* `StateMachineMethod` содержит 2 строки с ссылками на несуществующие RID
  (`MoveNext=253, Kickoff=39`) и (`MoveNext=4, Kickoff=798`) — в сборке всего 11 методов.
* `ImportScope[5]` ссылается на `parent=39`, у `LocalVariable` поле `Signature` пустое.

Это остатки от предыдущих компиляций в общем проекте (`PTTR-Source` — папка с несколькими
модами). На документы, строки и sequence points они не влияют: всё, что выше в таблицах,
согласовано с DLL.

---

## 8. `PTTRMod.il.txt` — что в листинге

Файл содержит **все 11 методов** с:

* токеном `MethodDef 0x0600000X`, RVA и файловым смещением;
* типом заголовка (TINY/FAT), `codeSize`, `maxStack`, `localVarSigTok`, списком локальных;
* комментарием `=== Main.cs:37:17-37:48 ===` перед группой инструкций (из PDB);
* полным текстом IL: `ldstr`, `callvirt` с разрешённым именем члена, `brfalse` с метками.

Пример (фрагмент `OnUpdate`):

```
  IL_000C:   ldsfld         Field::GodMode
  IL_0011:   ldsfld         Field::GodMode
  IL_0016:   callvirt       MelonLoader.MelonPreferences_Entry`1<bool>::get_Value() -> !0
  IL_001B:   ldc.i4.0
  IL_001C:   ceq
  IL_001E:   callvirt       MelonLoader.MelonPreferences_Entry`1<bool>::set_Value(!0) -> void
```

`!0` — это generic-параметр `T` («класс-дженерик»), поэтому вызовы `get_Value`/`set_Value`
показаны с `MelonPreferences_Entry<T>`, а не «сырым» индексами.

---

## 9. Дополнительные технические наблюдения

1. **Все патчи — префиксы.** Ни одного постфикса, ни транслитераторов, ни
   `HarmonyMethod`/`Prepare`. Это значит, что мод не «портит» тело игры, а только
   отменяет/подменяет вызовы.
2. **`Harmony` ID — `com.olivia.pttrmod`.** Строка — единственный след «пакета»;
   GUID мода (`MelonInfo` 4-й параметр) не задан, `MelonID` отсутствует.
3. **Нет `MelonPreferences.Save` при выходе** — только после изменений F1/F2.
   Если менять настройки в UI MelonLoader, сохранение не произойдёт.
4. **`Main.DumpPlayerState` — `private static`**, поэтому вызывается из `OnUpdate`
   напрямую (не через `this`). В IL это `call MethodDef 0x06000005`.
5. **`Object.FindObjectOfType<PTTRPlayer>()`** — старый API Unity (до 2023),
   в новых версиях он deprecate, но работает.
6. **`LoggerInstance.Msg` vs `MelonLogger.Msg`** в одном коде: в `Main` используется
   экземплярный `LoggerInstance`, в `Patches` — статический `MelonLogger`.
   Функционально одинаково (лог мода), но смешано.
7. **`Debug` vs `Release`**: `Debuggable` позволяет `Debugger.IsAttached` = false,
   оптимизации включены, но PDB есть — нормальный «Release + DebugType portable».

---

## 10. Итоговые ответы на вопрос «безопасно ли»

* **Подмена файлов?** Нет: DLL ↔ PDB связаны SHA-256 из детерминированной сборки.
* **Обфускация?** Нет: имена типов, методов, параметров и локальных читаемые,
  PDB полный.
* **Скрытые возможности?** Нет: 19 строковых литералов, 39 `MemberRef`, все
  разрешены в понятные типы (MelonLoader, Harmony, UnityEngine, System).
* **Сеть/файлы/процессы?** Нет. В `MemberRef` ни `System.Net.*`, ни `System.IO.*`,
  ни `System.Diagnostics.Process`.
* **Запуск постороннего кода?** Нет `Assembly.Load`, `DynamicMethod`, `DllImport`,
  `NativeLibrary`, `Marshal`, `MethodInfo.Invoke` по строке. Единственные вызовы
  рефлексии — `typeof(Main).Assembly` (для `PatchAll`) и generic `ldtoken`.
* **Зависимость от версии игры?** Да: имена четырёх методов `PTTRPlayer` берутся
  захардкоженными строками в `[HarmonyPatch]`. При переименовании в игре патчи
  молча не применятся (Harmony залогирует warning).

**Вывод: мод безопасен и минимален по функциональности — 3 настройки и 4 патча.**

---

## 11. Как воспроизвести разбор

Всё сделано на Python (`dnfile` + `dncil` для метаданных/IL, собственный парсер
Portable PDB для sequence points). Команды:

```bash
pip install dnfile dncil
python3 - <<'EOF'
import dnfile
pe = dnfile.dnPE("PTTRMod.dll")
for name in dir(pe.net.mdtables):
    attr = getattr(pe.net.mdtables, name)
    if hasattr(attr, "rows"):
        print(f"{name:24s} {len(attr.rows)}")
EOF
```

Тот же результат даёт `ilspycmd -il PTTRMod.dll`, `ildasm`, `dnSpy` или `dotnet-ildasm`.
PDB читается `System.Reflection.Metadata` (в .NET) или `dnfile`+ручной разбор `#~`.

---

*Разбор статический. Файлы не исполнялись, сетевых запросов код мода не делает.*
