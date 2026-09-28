# FakeOfflinePlus для Discord Android

Набор приватности: скрытие typing, quiet hours, panic mode и server-side status lock.

## Установка

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/fake-offline-plus
```

Manifest:

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/fake-offline-plus/manifest.json
```

## Что умеет не локально

- блокирует outgoing `typing...` — другие реально не видят, что ты печатаешь;
- quiet hours — скрывает typing по расписанию;
- panic mode — включает приватность;
- пытается выставить реальный Discord status через внутренние API Discord Android;
- status lock — повторно применяет invisible/idle/dnd/online каждые N минут;
- whitelist каналов для typing.

## Что остаётся best-effort

Реальный статус зависит от текущей версии Discord Android. Если Discord поменял внутренний action статуса, кнопка `Apply real status now` покажет, что action не найден. Скрытие typing работает надёжнее, потому что блокирует отправку typing events.

## Важно

Плагин не хранит и не просит Discord token. Он использует только внутренние actions текущего клиента, если они доступны.
