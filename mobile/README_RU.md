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
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@24663df2b81ad7b20481867362c4b07c11396c3b/mobile/dist/custom-profile
```

Если загрузчик просит прямую ссылку на manifest:

```text
https://cdn.jsdelivr.net/gh/Korimuspast1/pr@24663df2b81ad7b20481867362c4b07c11396c3b/mobile/dist/custom-profile/manifest.json
```

Raw GitHub manifest:

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


## Почему не один готовый Discord APK?

`CustomProfileMobile` — это плагин для мобильного загрузчика, а не самостоятельное приложение. Полный Discord APK содержит proprietary-код Discord; его нельзя нормально собрать из этого репозитория и безопасно раздавать как готовый APK. Правильный путь — установить Discord через Revenge/Bunny Manager, а затем поставить этот плагин по URL выше.
