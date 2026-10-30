# Ночная смена

Оригинальная четырёхминутная история о ночном охраннике. Это сдержанный кинематографичный фоторассказ: фотографии с движением камеры, графика экрана наблюдения, закадровая синтезированная речь, оригинальная синтезированная музыка, звуковые акценты и русские субтитры.

Готовый файл: [`../docs/guard-story/nochnaya-smena-4k.mp4`](../docs/guard-story/nochnaya-smena-4k.mp4). Кодирование — MP4, H.264/AAC, 3840 × 2160, 24 кадра/с, 04:00. Некоторые исходные фотопланы имеют низкое разрешение и увеличены до 4K; фотография кота и графика высокого разрешения. Источники и лицензии перечислены в [CREDITS.md](CREDITS.md).

## Сборка и просмотр

Нужны Python 3.11+, Pillow, NumPy и imageio-ffmpeg. Озвучка находится в `audio/`, текст — в `script.json`.

```bash
python3 -m pip install -r guard-story/requirements.txt
python3 guard-story/make_audio.py
python3 guard-story/build_video.py --prepare
python3 guard-story/build_video.py --encode
python3 guard-story/serve.py --port 4173
```

Промежуточные изображения и звук записываются в `guard-story/.work/`, исключённую из Git. Локальный сервер поддерживает byte-range запросы и перемотку.
