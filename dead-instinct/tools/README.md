# Тесты и генераторы (в APK не входят)

```bash
npm i @napi-rs/canvas jsdom
node tools/soak.js       # логика: 5 эпизодов × 3 сида по 150 с, финалы, сохранения, навигация
node tools/uitest.js     # весь UI в jsdom: меню → старт → диалоги → рюкзак → пауза → смерть → перезапуск
node tools/gen_icons.js  # иконки лаунчера Android
```

`tools/screens.js` делает настоящие скриншоты в headless Chromium (нужен puppeteer-core и браузер,
см. комментарий в начале файла) и складывает их в `tools/shots/` (в git не попадают).
