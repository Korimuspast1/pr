# Порт CustomProfile для Vencord

Готовый порт находится здесь:

```text
vencord-userplugins/customProfile/
├─ index.tsx
└─ styles.css
```

Также подготовлен архив:

```text
customProfile-vencord.zip
```

## Что изменено относительно Nightcord-версии

Плагин портирован под обычный Vencord и больше не требует Nightcord-only API:

- удалён `@api/pluginI18n` / `autoTranslateNightcord`;
- удалён `@api/HeaderBar`;
- удалены `PluginSync` и `OAuth2` Nightcord;
- отключена облачная синхронизация профилей Nightcord;
- плагин стал **local-only**: изменения профиля видишь только ты у себя в Discord.

## Как установить в Vencord

> Важно: кастомные плагины Vencord ставятся через сборку Vencord из исходников. В обычный Discord файл напрямую не закидывается.

### 1. Скачай/клонируй Vencord

```powershell
git clone https://github.com/Vendicated/Vencord.git
cd Vencord
corepack enable
pnpm install --frozen-lockfile
```

### 2. Добавь плагин

Создай папку, если её нет:

```powershell
mkdir src\userplugins
```

Скопируй папку из этого репозитория:

```text
vencord-userplugins/customProfile
```

в:

```text
Vencord/src/userplugins/customProfile
```

Должно получиться так:

```text
Vencord/src/userplugins/customProfile/index.tsx
Vencord/src/userplugins/customProfile/styles.css
```

### 3. Собери Vencord

```powershell
pnpm build
```

### 4. Установи в Discord Desktop

```powershell
pnpm inject
```

Выбери свою версию Discord: Stable / PTB / Canary. Потом полностью перезапусти Discord.

## Как открыть настройки плагина

1. Открой Discord.
2. Settings → Vencord → Plugins.
3. Найди `CustomProfile` и включи его.
4. Открой настройки плагина и нажми **Open Custom Profile**.

Если у тебя включён Vencord Toolbox, действие также доступно как **Open Custom Profile**.

## Проверка

Порт проверен в свежем Vencord:

```text
pnpm testTsc — OK
pnpm build   — OK
```

## Ограничения

- Это визуальная локальная модификация, другие пользователи Discord её не увидят.
- Nightcord cloud sync специально удалён.
- После обновлений Discord некоторые webpack-патчи могут сломаться — тогда плагин нужно будет обновлять.
- Клиентские моды Discord могут нарушать правила Discord, используй на свой риск.
