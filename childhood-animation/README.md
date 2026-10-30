# Как мы построили секретный штаб

Оригинальная четырёхминутная анимационная история о летних каникулах, друзьях, картонной коробке и дворовом коте. Финальный файл — [`../docs/childhood-animation/sekretny-shtab.mp4`](../docs/childhood-animation/sekretny-shtab.mp4), а рядом находится простая страница для просмотра и скачивания.

**Формат:** MP4 / H.264, 1280×720, 24 кадра/с (авторская покадровая анимация рисуется с частотой 12 кадров/с), звук AAC. В ролике есть закадровая синтезированная речь, оригинальная синтезированная музыка и встроенные русские субтитры. Файл `subtitles.srt` содержит приблизительные временные метки для отдельного использования.

## Пересборка

Нужны Python 3.11+, зависимости из `requirements.txt` и файлы озвучки в `audio/`. FFmpeg поставляется пакетом `imageio-ffmpeg`.

```bash
python3 -m pip install -r childhood-animation/requirements.txt
python3 childhood-animation/make_audio.py
python3 childhood-animation/render.py --stills  # постер и проверочные кадры
python3 childhood-animation/render.py           # готовый MP4
```

Все рисунки и музыкальное сопровождение создаются кодом. Промежуточные файлы сохраняются в `childhood-animation/.work/` и не попадают в Git. Десять фрагментов синтезированной речи записаны одним выбранным голосом; сценарий находится в `script.json`.

Шрифты [Nunito](https://github.com/google/fonts/tree/main/ofl/nunito) и [Russo One](https://github.com/google/fonts/tree/main/ofl/russoone) распространяются по SIL Open Font License 1.1; тексты лицензий лежат в `assets/fonts/`.
