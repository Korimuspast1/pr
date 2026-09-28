# CustomProfileMobile v2 для Discord Android

Мобильный порт CustomProfile под Vendetta-compatible моды Discord Android:

- Revenge
- Bunny
- Vendetta / Pyoncord-совместимые загрузчики

Плагин **local-only**: изменения профиля видишь только ты на своём телефоне. Он не просит Discord token и ничего не отправляет на сервер.

## Установка на Android

Рекомендую **Revenge Manager**.

1. Установи Revenge Manager / Bunny Manager.
2. Через него установи модифицированный Discord.
3. Открой Discord → Settings → Plugins.
4. Нажми `+` / Install plugin.
5. Вставь URL плагина:

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/custom-profile
```

Если загрузчик просит прямую ссылку на manifest:

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@arena/01a0e825-pr/mobile/dist/custom-profile/manifest.json
```

Raw GitHub manifest:

```text
https://raw.githubusercontent.com/Korimuspast1/pr/arena/01a0e825-pr/mobile/dist/custom-profile/manifest.json
```

## Что добавлено в v2

- предпросмотр профиля в настройках;
- пресеты: Nitro / Staff / Anime / Dark;
- больше бейджей;
- Nitro уровни;
- Boost уровни;
- avatar decorations;
- custom decoration URL;
- profile effects / custom effect ID;
- best-effort clan tag / nameplate;
- best-effort fake Orbs Balance;
- fake connections до 3 штук;
- импорт/экспорт профиля JSON;
- 3 слота профилей;
- локальные override-ы для других пользователей;
- защита от дублей бейджей.

## Ограничения

- Другие люди это не увидят.
- Это не полноценный Vencord APK: мобильный Discord работает на React Native, а Vencord — на Desktop/Electron.
- Nameplate, Orbs Balance и Profile Effects зависят от текущей версии Discord Android и могут работать не на всех сборках.
- После обновлений Discord некоторые патчи могут перестать работать.
- Клиентские моды Discord могут нарушать ToS Discord — используй на свой риск.

## Почему не один готовый Discord APK?

`CustomProfileMobile` — это плагин для мобильного загрузчика, а не самостоятельное приложение. Полный Discord APK содержит proprietary-код Discord; его нельзя нормально собрать из этого репозитория и безопасно раздавать как готовый APK. Правильный путь — установить Discord через Revenge/Bunny Manager, а затем поставить этот плагин по URL выше.
