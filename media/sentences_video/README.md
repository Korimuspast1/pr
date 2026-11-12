# Sentences video (15+ words each)

A fully code-generated MP4: 1920×1080, 30 fps, ~43 s, with a quiet synthesised
ambient pad. Every sentence on screen has **at least 15 words** (the script
refuses to render if one is shorter).

Output: [`sentences_15_words.mp4`](sentences_15_words.mp4) (~2.5 MB)

## What is inside

| Part | Content |
| --- | --- |
| Intro (3.6 s) | Title, subtitle, chip with sentence/word counts |
| 6 scenes (5.8 s each) | One sentence per scene, revealed word by word, accent colour per scene, `NN / 06` counter and a live word count |
| Outro (4.2 s) | “Generated with code”, stack, totals |
| Audio | One triad per section, cross-faded, faded in/out and peak-normalised |

The sentences (word counts: 16, 15, 15, 15, 17, 15 — 93 words total):

1. Утренний свет медленно наполнял комнату, и казалось, что весь город ещё спит, забыв о своих обещаниях.
2. Старый маяк стоял на краю скалы, и каждую ночь его луч напоминал рыбакам о доме.
3. Мы редко замечаем, как много тихих решений мы принимаем каждый день, просто чтобы остаться собой.
4. Хороший код похож на чистую комнату: в нём легко найти нужную вещь и приятно находиться.
5. Зимой в Хельсинки темнеет рано, и окна домов загораются медленно, как будто город засыпает вместе с тобой.
6. Ветер принёс запах моря, и на секунду показалось, что время можно остановить и просто дышать.

## Build

```bash
python3 -m venv .venv && . .venv/bin/activate
pip install -r media/sentences_video/requirements.txt

# render (frames are piped straight into the bundled ffmpeg)
python3 media/sentences_video/make_video.py

# custom output / frame rate
python3 media/sentences_video/make_video.py --out /tmp/clip.mp4 --fps 24
```

`imageio-ffmpeg` ships a static ffmpeg binary, so no system ffmpeg install is
needed. ffmpeg itself is only used as an encoder — every frame and every audio
sample is produced by the Python code in `make_video.py`.

## Editing the content

* Sentences: the `SENTENCES` list at the top of `make_video.py`.
* Timings: `INTRO_SEC`, `SCENE_SEC`, `OUTRO_SEC`, `REVEAL_STAGGER`, `WORD_FADE`.
* Look: `BG_TOP`/`BG_BOTTOM`, `ACCENTS`, `BODY_SIZE`, `LINE_GAP`, fonts in
  `FONT_CANDIDATES` (DejaVu Sans is used by default and covers Cyrillic).
* Audio: `CHORDS` — one triad per section, up to 8 sections are cross-faded.

Re-rendering takes about 35 seconds on a single core.

## Layout

```
media/sentences_video/
├── make_video.py            # the generator (Pillow frames + ffmpeg + stdlib audio)
├── requirements.txt
├── README.md
└── sentences_15_words.mp4   # rendered result
```
