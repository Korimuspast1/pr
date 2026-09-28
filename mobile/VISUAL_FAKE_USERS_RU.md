# VisualFakeUsers для Discord Android

Локальный визуальный менеджер fake/test аккаунтов. Аккаунты **не существуют на серверах Discord** и видны только тебе.

## Установка

Discord Android → Settings → Plugins → `+` / Install plugin:

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/visual-fake-users
```

Manifest:

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/visual-fake-users/manifest.json
```

## Что умеет

- создавать до 5 fake users;
- генерировать валидный fake snowflake ID;
- задавать username;
- задавать display name;
- avatar URL;
- banner URL;
- bio;
- pronouns;
- status online/idle/dnd/offline;
- bot tag визуально;
- role IDs;
- nick на сервере;
- fake badges;
- показывать fake users в member list, где Discord Android берёт данные из GuildMemberStore;
- fake profile при открытии;
- import/export JSON.

## Как использовать

1. Открой нужный сервер.
2. Включи плагин.
3. Выбери slot 1-5.
4. Нажми `Generate valid fake ID`.
5. Включи slot.
6. Заполни данные.
7. Перезапусти Discord или обнови member list, если fake user не появился сразу.

## Ограничения

- Всё видно только тебе.
- Эти аккаунты не существуют в Discord API.
- Они не могут писать сообщения.
- Другие пользователи их не увидят.
- Member list в Discord Android часто меняется; если fake users не появляются в твоей версии, пришли скрин/ошибку — добавим патч под твою сборку.
