# ChatHeatmap для Discord Android

Статистика активности чатов: активность по часам, дням, топ пользователей, топ слов, топ emoji, медиа.

## Установка

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/chat-heatmap
```

Manifest:

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/chat-heatmap/manifest.json
```

## Что умеет

- heatmap по часам;
- heatmap по дням недели;
- топ пользователей;
- топ каналов;
- топ слов;
- топ emoji;
- счётчик медиа/вложений;
- экспорт статистики JSON;
- публикация статистики в Discord-канал через webhook;
- auto-publish каждые N сообщений.

## Как сделать не локально

1. На своём сервере Discord создай Webhook в нужном канале.
2. Скопируй Webhook URL.
3. В настройках ChatHeatmap вставь URL в `Publish / non-local sharing`.
4. Нажми `Publish now` или включи `Auto-publish`.

Так статистика будет поститься в канал и её увидят другие. Discord token не нужен.

## Важно

Считает только сообщения, которые пришли на телефон **после включения плагина**. Сырые сообщения никуда не отправляются; webhook публикует только агрегированную статистику.
