# RYU — Ryuu Notation

```
        ~^~^~^~^~^~^~^~^~^~^~^~^~^~
       <  . ~ # ? % & ! $ ' + |  >
        \___ данные как обход ___/
             \___ дерева ___/
```

**RYU** — новый текстовый формат структурированных данных. Не похож на
JSON, CSV, YAML, TOML и XML: в нём нет кавычек, скобок, запятых,
двоеточий и значимых отступов. Вместо разметки — **обход дерева курсором**:
одна строка = один узел, руна в начале строки задаёт тип.

- 📜 Полная спецификация: [SPEC.md](SPEC.md)
- ⚙️ Референс-реализация: Python-пакет [`ryuu/`](ryuu/) — ноль зависимостей
- 🧪 Тесты: [`tests/test_ryuu.py`](tests/test_ryuu.py) — 70 тестов
- 📦 Примеры: [`examples/`](examples/) — карточка игры, библиотека (замена CSV), настройки

## Как выглядит

Одно и то же дерево:

**JSON**
```json
{
  "appid": 2427700,
  "name": "Ryuu Manifest Tool",
  "tags": ["steam", "manifest", "tools"],
  "free": false,
  "updated": "2026-09-28T14:30:00Z",
  "homepage": "https://example.com",
  "depots": [
    {"depot_id": 3736911, "size": 482130944},
    {"depot_id": 3736912, "size": 12582912}
  ],
  "description": "Многострочный\nтекст без экранирования \"кавычек\""
}
```

**RYU** (`game.ryu`)
```
. appid # 2427700
. name ~ Ryuu Manifest Tool
. tags
+ ~ steam
+ ~ manifest
+ ~ tools
. free ? no
. updated % 2026-09-28T14:30:00Z
. homepage & https://example.com
. depots
+
. depot_id # 3736911
. size # 482_130_944
+
. depot_id # 3736912
. size # 12_582_912
''
. description
~~~
Многострочный
текст без экранирования "кавычек"
~~~
```

Читается сверху вниз как прогулка по дереву: «поле `appid`, число 2427700;
поле `tags`, заходим; элемент `steam`…».

## Руны

| Руна | Смысл            | Пример                        |
|------|------------------|-------------------------------|
| `.`  | поле (гнездо)    | `. name ~ Alice`              |
| `+`  | элемент списка   | `+ ~ steam`                   |
| `'`  | закрыть узел     | `'` · `''`                    |
| `~`  | строка, сырая    | `. path ~ C:/Steam/config`    |
| `#`  | число            | `. size # 482_130_944`        |
| `?`  | булево           | `. nsfw ? no`                 |
| `%`  | дата/время       | `. added % 2026-09-28`        |
| `&`  | ссылка           | `. img & https://cdn…/x.jpg`  |
| `$`  | байты (base64)   | `. checksum $ 5raSLRE=`       |
| `!`  | null             | `. note !`                    |
| `\|` | пустая форма     | `. dlc | list`                |
| `;`  | комментарий      | `; вся строка — комментарий`  |
| `~~~`| блок-строка      | многострочный текст без escape|

## Почему это удобно

- **Нет экранирования.** Строка — это просто символы до конца строки.
  Кавычки, слэши, `;` — всё пишется как есть. Многострочный текст — блок
  `~~~`.
- **Плоские карты — без подъёмов.** Атом «запечатывает» своё поле:
  ```
  . user
  . name ~ Alice
  . age # 30
  . active ? yes
  ```
  — это `user = {name, age, active}` без единого `'`.
- **`+` — разделитель записей.** Список словарей (главный сценарий CSV)
  пишется без запятых и скобок: следующий `+` сам закрывает предыдущую
  запись (см. `examples/library.ryu`).
- **Типы видны сразу.** Число, ссылка, время и байты различимы с первого
  взгляда — и парсер не гадает.
- **Отступы не значимы.** Документ нельзя сломать пробелом; диффы чистые:
  одна строка = один узел.

## Быстрый старт

```bash
# клонировать и пользоваться без установки
python3 -m ryuu check    examples/game.ryu      # проверка синтаксиса
python3 -m ryuu decode   examples/game.ryu      # RYU -> JSON (stdout)
python3 -m ryuu encode   data.json data.ryu     # JSON -> RYU

# тесты
python3 -m unittest discover -s tests
```

Или установить как пакет (CLI `ryuu`):

```bash
pip install .
ryuu encode data.json data.ryu
```

## Python API

```python
import ryuu
from ryuu import Link

tree = ryuu.loads(open("examples/game.ryu", encoding="utf-8").read())
tree["appid"]            # 2427700 (int)
tree["updated"]          # datetime.datetime(2026, 9, 28, 14, 30, tzinfo=UTC)
tree["header_image"]     # Link("https://cdn.example.com/…") — подкласс str

text = ryuu.dumps({"answer": 42, "tags": ["a", "b"]})
# . answer # 42
# . tags
# + ~ a
# + ~ b

ryuu.dump(tree, open("out.ryu", "w", encoding="utf-8"))
```

Соответствие типов: карта↔`dict`, список↔`list`, `~`↔`str`, `#`↔`int/float`,
`?`↔`bool`, `!`↔`None`, `%`↔`datetime/date/time`, `&`↔`ryuu.Link`,
`$`↔`bytes`.

## Структура репозитория

```
SPEC.md            спецификация формата v1.0
README.md          этот файл
ryuu/              реализация (parser, writer, CLI)
  parser.py        разбор: стек открытых узлов, O(n)
  writer.py        каноническая запись
  __main__.py      CLI: encode / decode / check
  values.py        тип Link
  errors.py        RyuuParseError / RyuuWriteError
tests/test_ryuu.py 70 тестов + фаззинг round-trip
examples/          game.ryu, library.ryu, settings.ryu
pyproject.toml     упаковка пакета
```

## Ограничения (честно)

- имена полей — один токен без пробелов (`release_date`, не `release date`);
- комментарии — только целыми строками;
- строка не может содержать строку `~~~` (это закрывающий маркер блока);
- пустой элемент списка выбрасывается — для явного `null` пишите `+ !`.

Полный список с обоснованиями — [SPEC.md, §11](SPEC.md).
