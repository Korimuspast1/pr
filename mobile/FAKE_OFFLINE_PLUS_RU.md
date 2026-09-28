# FakeOfflinePlus для Discord Android

Набор приватности: скрытие typing, quiet hours, panic mode и локальный spoof статуса.

## Установка

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/fake-offline-plus
```

Manifest:

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/fake-offline-plus/manifest.json
```

## Что умеет

- блокирует outgoing `typing...`;
- отдельно для ЛС и серверов;
- whitelist каналов;
- quiet hours;
- panic mode;
- счётчик заблокированных typing events;
- попытка выставить реальный Invisible статус, если текущая версия Discord отдаёт такой action;
- локальный spoof твоего статуса у тебя на устройстве.

## Важно

Самая надёжная функция — скрытие typing. Реальный invisible зависит от внутренних API Discord Android и может не сработать на конкретной сборке.
