# pr

Два независимых проекта в одном репозитории.

## [custom-profile](custom-profile/) — плагин Elyx для exteraGram

Локальный визуальный предпросмотр оформления собственного профиля: значки,
звёзды, эмодзи-статус, цвет имени. Ничего не отправляет на серверы Telegram.
Готовый архив — [`release/custom-profile.elyx`](release/custom-profile.elyx).

## [world-render](world-render/) — процедурный мир на Rust

Остров с маяком, домиком и лесом, отрисованный CPU-рейкастером на чистом Rust:
без GPU, без внешних крейтов, без текстур и мешей. Собран как продолжение идеи
главы 1.2 книги «Язык программирования Rust» — от `println!("Привет, мир!")` до
многопоточной трассировки лучей, своего PNG-кодировщика и интерактивного
браузерного превью.

![day](world-render/renders/preview/day.jpg)

```bash
cd world-render
cargo run --release -- --preset sunset --out renders/sunset.png
cargo run --release -- --serve --port 8080   # превью в браузере
```

Подробности, все пресеты и описание алгоритмов — в
[`world-render/README.md`](world-render/README.md).
