# CustomProfileMobile для Discord Android

Это мобильный порт CustomProfile под Vendetta-compatible моды Discord Android:

- Revenge
- Bunny
- Vendetta/Pyoncord-совместимые загрузчики

Плагин **local-only**: изменения профиля видишь только ты на своём телефоне. Он не просит Discord token и ничего не отправляет на сервер.

## Установка на Android

Рекомендую вариант через **Revenge Manager**, потому что это ближе всего к “Vencord на телефоне”.

1. Установи Revenge Manager / Bunny Manager.
2. Установи через него Discord.
3. Открой Discord → Settings → Plugins.
4. Нажми `+` / Install plugin.
5. Вставь URL плагина:

```text
https://raw.githubusercontent.com/Korimuspast1/pr/refs/heads/arena/01a0e825-pr/mobile/dist/custom-profile
```

Если загрузчик не принимает raw URL, открой ссылку на `manifest.json` ниже и установи вручную через свой менеджер:

```text
https://raw.githubusercontent.com/Korimuspast1/pr/refs/heads/arena/01a0e825-pr/mobile/dist/custom-profile/manifest.json
```

## Что умеет первая версия

- локально менять username/display name;
- локально менять bio/pronouns в профиле;
- локально подменять аватар по URL;
- локально подменять баннер по URL;
- локально задавать profile colors;
- локально симулировать Nitro там, где мобильный Discord читает `premiumType`;
- добавлять несколько фейковых бейджей только для твоего отображения.

## Ограничения

- Другие люди это не увидят.
- Это не полный порт Vencord: мобильный Discord работает на React Native, а Vencord — на Desktop/Electron.
- После обновлений Discord некоторые патчи могут перестать работать.
- Клиентские моды Discord могут нарушать ToS Discord — используй на свой риск.
