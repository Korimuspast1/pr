# как достать cookies для ютуба

## зачем

YouTube блокирует запросы с датацентровых IP («Sign in to confirm you're not a
bot»). GitHub Actions крутится именно на таких IP. Единственный надёжный способ
— отдать боту cookies залогиненного (или хотя бы открытого) youtube из браузера.

без cookies работает: **tiktok, instagram reels, vk видео** — они так не блокируют.

## вариант 1: расширение для браузера (проще всего)

1. поставь расширение **Get cookies.txt LOCALLY**
   ([chrome](https://chromewebstore.google.com/detail/get-cookiestxt-locally/cclelndahbckbenkjhflpdbgdldlbecc)
   / [firefox](https://addons.mozilla.org/firefox/addon/get-cookiestxt-locally/))
2. открой `https://www.youtube.com` и убедись, что ты вошёл в аккаунт
3. нажми иконку расширения → **Export** → сохранится `youtube.com_cookies.txt`
4. перекодируй файл в base64 одной строкой, чтобы удобно вставить в секрет:

```bash
base64 -w0 youtube.com_cookies.txt > cookies.b64
```

(на macOS: `base64 -i youtube.com_cookies.txt | tr -d '\n' > cookies.b64`)

5. скопируй содержимое `cookies.b64` в секрет `YTDL_COOKIES`

> бот принимает и **сырой** текст cookies (netscape-формат с табами), и base64.
> base64 надёжнее: секреты GitHub плохо переносят переводы строк.

## вариант 2: без расширения, из devtools

1. F12 → Network → обнови страницу youtube
2. найди любой запрос к `youtube.com`, посмотри заголовок `cookie`
3. но это не netscape-формат — проще всё-таки вариант 1

## вариант 3: yt-dlp локально

```bash
yt-dlp --cookies-from-browser chrome --cookies cookies.txt --skip-download URL
base64 -w0 cookies.txt
```

## сколько живёт

Cookies живут неделями, но если бот начнёт отвечать «нужны cookies» — просто
экспортируй заново. Признак: в логе Actions `Sign in to confirm you're not a bot`.

## предупреждение по безопасности

Cookies — это фактически доступ к твоему аккаунту. Поэтому:

* клади их **только в секрет** GitHub, никогда в код и не в чат
* если cookies утекли — в google-аккаунте есть «выход со всех устройств»,
  после него старые cookies перестают работать
* лучше завести **отдельный google-аккаунт** для бота, а не использовать основной

## прокси (если не хочется светить аккаунтом)

секрет `YTDL_PROXY`, форматы:

```
1.2.3.4:8080
1.2.3.4:8080:логин:пароль
http://логин:пароль@1.2.3.4:8080
socks5://1.2.3.4:1080
```

резидентные прокси обычно проходят без cookies, но стоят денег.
датацентровые прокси ютуб режет так же, как IP github — толку мало.
