# VisualOwnerMode для Discord Android

Плагин делает **локальный визуальный режим владельца/админа сервера**.

Важно: это только визуально на твоём телефоне. Плагин **не выдаёт реальные права**, **не может реально выдавать роли** и **не обходит Discord permissions**. Если нажать реальную server-side кнопку без прав, Discord всё равно отклонит действие.

## Установка

Discord Android → Settings → Plugins → `+` / Install plugin:

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/visual-owner-mode
```

Manifest:

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/visual-owner-mode/manifest.json
```

## Что умеет

- визуально показывает тебя владельцем сервера;
- визуально добавляет fake owner role;
- можно задать имя и цвет fake owner role;
- можно визуально дать себе все существующие роли;
- можно визуально добавить себе выбранные role IDs;
- можно визуально выдать роль другому пользователю по User ID;
- можно визуально поменять nick другому пользователю;
- локально спуфит permission checks, чтобы часть admin/manage UI могла отображаться;
- можно ограничить только текущим сервером или target guild ID.

## Как использовать

1. Открой нужный сервер.
2. В настройках плагина включи `Only current / target server`.
3. Включи `Show me as server owner`.
4. Включи `Add fake owner role to me`.
5. Если хочешь увидеть все роли на себе — включи `Give me all existing roles visually`.
6. Для другого человека вставь его User ID и Role IDs в раздел `Visually give roles to another user`.

## Как получить Role IDs

В настройках плагина нажми:

```text
Copy role IDs in target server
```

Потом вставь куда-нибудь — там будут роли и их ID.

## Ограничения

- Всё видишь только ты.
- Реально роли не выдаются.
- Реальные действия управления сервером не сработают без настоящих прав.
- Некоторые элементы UI Discord Android могут кешироваться — если не обновилось, перезапусти Discord.
