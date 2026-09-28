# WhoDeleted для Discord Android

Локальный помощник, который показывает, кто **скорее всего** удалил сообщение: ты, автор, модератор/бот или bulk delete.

## Установка

Discord Android → Settings → Plugins → `+` / Install plugin:

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/who-deleted
```

Manifest:

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/who-deleted/manifest.json
```

## Что умеет

- ловит удаления сообщений после установки;
- показывает toast при удалении;
- ведёт локальный лог удалений;
- пытается определить: удалил ты / автор / модератор или бот / bulk delete;
- работает в ЛС и на серверах;
- можно игнорировать ботов, себя и свои ручные удаления;
- можно копировать запись лога.

## Важно

Discord обычно **не отправляет точный ID удалившего**. Поэтому плагин честно помечает точность: `exact`, `likely`, `unknown`.
