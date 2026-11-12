#!/usr/bin/env bash
#
# bassboost — автоматический бас-буст для песен (и видео с музыкой) в Termux.
#
#   ./bassboost.sh путь/к/песне.mp3
#   ./bassboost.sh /storage/emulated/0/Music/track.mp4
#   ./bassboost.sh -s heavy track.flac
#   ./bassboost.sh --audio-only -o out.m4a track.mp4
#
# Всё определяется автоматически: есть ли видео, какой формат, куда сохранять.
# Видео (если есть) копируется без перекодирования, бустится только звук.

set -euo pipefail

VERSION="1.0.0"

# ---------- настройки обработки ----------
LOW_CUT=30        # срез саб-шума ниже этого порога, Гц
BASS_FREQ=105     # центральная частота бас-полки, Гц
BASS_WIDTH=0.7    # ширина бас-полки (0..1)
COMP_THRESH=-12dB # компрессор, чтобы бас не клипповал
COMP_RATIO=4
LOUDNORM_I=-14    # целевая громкость, LUFS
LOUDNORM_TP=-1.5  # потолок true peak, dBTP
LOUDNORM_LRA=11
LIMITER=0.95      # финальный лимитер (амплитуда)

# ---------- параметры по умолчанию ----------
INPUT=""
OUT=""
PRESET="medium"
GAIN_DB=""
LOUDNORM=1
AUDIO_ONLY=0
FORCE_VIDEO=0

c_red=$'\033[0;31m'; c_grn=$'\033[0;32m'; c_yel=$'\033[0;33m'; c_dim=$'\033[2m'; c_off=$'\033[0m'

say()  { printf '%s\n' "$*"; }
ok()   { printf '%s✔ %s%s\n' "$c_grn" "$*" "$c_off"; }
warn() { printf '%s! %s%s\n' "$c_yel" "$*" "$c_off" >&2; }
die()  { printf '%s✘ %s%s\n' "$c_red" "$*" "$c_off" >&2; exit 1; }

usage() {
  cat <<EOF
bassboost v$VERSION — бас-буст трека в один запуск (Termux / Linux)

Использование:
  $(basename "$0") [опции] <входной файл>

Примеры:
  $(basename "$0") track.mp3
  $(basename "$0") /sdcard/Music/song.mp4        # видео останется, звук станет бассистее
  $(basename "$0") -s heavy bass.flac             # пресет: light / medium / heavy
  $(basename "$0") -s 13 song.wav                 # или точный буст в дБ (-30..30)
  $(basename "$0") --audio-only song.mp4          # только аудио (m4a/mp3/...)
  $(basename "$0") -o готовый.m4a song.mp4

Опции:
  -s, --strength X   Пресет light (6 дБ), medium (10 дБ, по умолчанию),
                     heavy (15 дБ) — или число в дБ, например -s 12
      --bass-gain N  Точный буст низких частот в дБ (эквивалент -s N)
  -o, --output FILE  Куда сохранить (по умолчанию: рядом с входным,
                     с суффиксом _bassboosted)
      --audio-only   Извлечь только аудио (видео отбросить)
      --keep-video   Всегда копировать видео (по умолчанию — авто)
      --no-loudnorm  Не нормализовать громкость (по умолчанию -14 LUFS)
  -h, --help         Показать эту справку
      --version      Версия

Результат не перезаписывает существующие файлы: при совпадении имени
добавляется номер, например track_bassboosted_2.mp3.
EOF
}

# ---------- разбор аргументов ----------
while [[ $# -gt 0 ]]; do
  case "$1" in
    -h|--help)    usage; exit 0 ;;
    --version)    echo "bassboost v$VERSION"; exit 0 ;;
    -s|--strength) [[ $# -ge 2 ]] || die "У опции $1 нужен аргумент"; PRESET="$2"; shift 2 ;;
    --bass-gain)  [[ $# -ge 2 ]] || die "У опции $1 нужен аргумент"; GAIN_DB="$2"; shift 2 ;;
    -o|--output)  [[ $# -ge 2 ]] || die "У опции $1 нужен аргумент"; OUT="$2"; shift 2 ;;
    --audio-only) AUDIO_ONLY=1; shift ;;
    --keep-video) FORCE_VIDEO=1; shift ;;
    --no-loudnorm) LOUDNORM=0; shift ;;
    --) shift; [[ $# -ge 1 ]] && { [[ -z "$INPUT" ]] || die "Лишний аргумент: $1"; INPUT="$1"; shift; } ;;
    -*) die "Неизвестная опция: $1 (см. --help)" ;;
    *)  [[ -z "$INPUT" ]] || die "Лишний аргумент: $1"; INPUT="$1"; shift ;;
  esac
done

[[ -n "$INPUT" ]] || { usage; exit 1; }
[[ -f "$INPUT" ]] || die "Файл не найден: $INPUT"
[[ -r "$INPUT" ]] || die "Файл не читается: $INPUT"

# ---------- ffmpeg / ffprobe ----------
need_ffmpeg() {
  if command -v ffmpeg >/dev/null 2>&1; then return 0; fi
  if [[ "${PREFIX:-}" == *com.termux* ]] && command -v pkg >/dev/null 2>&1; then
    warn "ffmpeg не найден — устанавливаю через pkg..."
    pkg install -y ffmpeg || die "Не удалось установить ffmpeg. Попробуй вручную: pkg install ffmpeg"
    command -v ffmpeg >/dev/null 2>&1 || die "ffmpeg так и не появился. Попробуй: pkg install ffmpeg"
    ok "ffmpeg установлен"
  else
    die "ffmpeg не найден. В Termux: pkg install ffmpeg · Debian/Ubuntu: apt install ffmpeg"
  fi
}
need_ffmpeg

HAS_FFPROBE=0
command -v ffprobe >/dev/null 2>&1 && HAS_FFPROBE=1

# ---------- громкость буста ----------
resolve_gain() {
  local raw="${GAIN_DB:-$PRESET}"
  raw="$(printf '%s' "$raw" | tr '[:upper:]' '[:lower:]')"
  case "$raw" in
    light|soft|weak|low)   echo 6 ;;
    medium|normal|mid|default) echo 10 ;;
    heavy|hard|strong|max|extreme) echo 15 ;;
    *)
      [[ "$raw" =~ ^-?[0-9]+(\.[0-9]+)?$ ]] || die "Непонятный пресет/буст: '$raw' (light|medium|heavy или число дБ)"
      awk -v g="$raw" 'BEGIN{ exit !(g >= -30 && g <= 30) }' \
        || die "Буст должен быть от -30 до +30 дБ (получено: $raw)"
      echo "$raw" ;;
  esac
}
GAIN="$(resolve_gain)"
# что показать в сводке: пресет словом или «вручную», если ввели число
if [[ "${GAIN_DB:-$PRESET}" =~ ^[-0-9.]+$ ]]; then PRESET_LABEL="вручную"; else PRESET_LABEL="$PRESET"; fi

# ---------- определение потоков ----------
# Печатаем семантические строки: audio / video (реальное видео) / cover (обложка альбома).
# mp3 с обложкой — это НЕ видео: иначе музыка молча превращается в mp4.
probe_streams() {
  if [[ $HAS_FFPROBE == 1 ]]; then
    local types atts
    types="$(LC_ALL=C ffprobe -v error -show_entries stream=codec_type -of csv=p=0 -- "$INPUT" 2>/dev/null)" \
      || die "Не удалось прочитать файл (битый?): $INPUT"
    grep -qx 'audio' <<<"$types" && echo audio || true
    atts="$(LC_ALL=C ffprobe -v error -select_streams v -show_entries stream_disposition=attached_pic -of csv=p=0 -- "$INPUT" 2>/dev/null || printf '')"
    if grep -qx 'video' <<<"$types"; then
      if [[ -z "$atts" ]]; then
        echo video   # ffprobe не сказал про обложку — считаем видео настоящим
      else
        if grep -qx '1' <<<"$atts"; then echo cover; fi
        if grep -qvx '1' <<<"$atts"; then echo video; fi
      fi
    fi
  else
    local probe vlines
    probe="$(LC_ALL=C ffmpeg -hide_banner -i "$INPUT" 2>&1 || true)"
    if grep -q 'Stream .*Audio:' <<<"$probe"; then echo audio; fi
    vlines="$(grep 'Stream .*Video:' <<<"$probe" || true)"
    if [[ -n "$vlines" ]]; then
      if grep -vq '(attached pic)' <<<"$vlines"; then echo video; fi
      if grep -q  '(attached pic)' <<<"$vlines"; then echo cover; fi
    fi
  fi
  return 0
}
probe_duration() { # HH:MM:SS.xx или пусто
  if [[ $HAS_FFPROBE == 1 ]]; then
    local d
    d=$(LC_ALL=C ffprobe -v error -show_entries format=duration -of default=nw=1:nk=1 -- "$INPUT" 2>/dev/null || true)
    [[ "$d" =~ ^[0-9]+(\.[0-9]+)?$ ]] && awk -v s="$d" 'BEGIN{printf "%02d:%02d:%05.2f", s/3600, (s%3600)/60, s%60}'
  else
    LC_ALL=C ffmpeg -hide_banner -i "$INPUT" 2>&1 | grep -oE 'Duration: [0-9:.]+' | head -n1 | awk '{print $2}' || true
  fi
}
probe_rate() { # частота дискретизации первой аудиодорожки, Гц (или пусто)
  if [[ $HAS_FFPROBE == 1 ]]; then
    LC_ALL=C ffprobe -v error -select_streams a:0 -show_entries stream=sample_rate -of csv=p=0 -- "$INPUT" 2>/dev/null | head -n1 || true
  else
    LC_ALL=C ffmpeg -hide_banner -i "$INPUT" 2>&1 | grep 'Audio:' | head -n1 | grep -oE '[0-9]+ Hz' | head -n1 | awk '{print $1}' || true
  fi
}

PROBE_OUT="$(probe_streams)" || exit 1   # die внутри уже напечатал причину
if [[ -n "$PROBE_OUT" ]]; then
  mapfile -t STREAM_TYPES <<<"$PROBE_OUT"
else
  STREAM_TYPES=()
fi
HAS_AUDIO=0; HAS_VIDEO=0; HAS_COVER=0
for t in "${STREAM_TYPES[@]:-}"; do
  [[ "$t" == "audio" ]] && HAS_AUDIO=1
  [[ "$t" == "video" ]] && HAS_VIDEO=1
  [[ "$t" == "cover" ]] && HAS_COVER=1
done
[[ $HAS_AUDIO == 1 ]] || die "В файле нет аудиодорожки — бустить нечего: $INPUT"
DURATION="$(probe_duration)"
IN_RATE="$(probe_rate)"
[[ "$IN_RATE" =~ ^[0-9]+$ ]] || IN_RATE=""   # loudnorm внутри считает в 192 кГц — вернём исходную

# ---------- формат результата ----------
ext_of() { local f="${1##*/}"; [[ "$f" == *.* ]] && printf '%s' "${f##*.}" | tr '[:upper:]' '[:lower:]' || printf ''; }

AUDIO_EXTS=" mp3 m4a aac flac wav ogg opus wma "
is_audio_ext() { [[ "$AUDIO_EXTS" == *" $1 "* ]]; }

# режим: audio или video
IN_EXT="$(ext_of "$INPUT")"
MODE="audio"
if [[ $AUDIO_ONLY == 0 ]]; then
  if [[ $FORCE_VIDEO == 1 && $HAS_VIDEO == 1 ]]; then MODE="video"
  elif [[ $FORCE_VIDEO == 0 && $HAS_VIDEO == 1 && -z "$OUT" ]]; then MODE="video"
  elif [[ -n "$OUT" ]] && [[ "$(ext_of "$OUT")" =~ ^(mp4|mkv|mov|webm|m4v|avi)$ ]]; then MODE="video"
  fi
fi
# явный --keep-video без видео в файле бессмысленен — откатываемся в аудио
if [[ $MODE == "video" && $HAS_VIDEO == 0 ]]; then
  [[ $FORCE_VIDEO == 1 ]] && warn "В файле нет видео — сохраняю как аудио"
  MODE="audio"
fi

# расширение результата
if [[ -n "$OUT" ]]; then
  OUT_EXT="$(ext_of "$OUT")"
  [[ -n "$OUT_EXT" ]] || die "У выходного файла нет расширения: $OUT"
else
  if [[ $MODE == "video" ]]; then
    case "$IN_EXT" in mp4|mkv|mov|webm|m4v|avi) OUT_EXT="$IN_EXT" ;; *) OUT_EXT="mp4" ;; esac
  else
    if is_audio_ext "$IN_EXT"; then OUT_EXT="$IN_EXT"; else OUT_EXT="mp3"; fi
  fi
  base="${INPUT%.*}"; [[ "$base" == "$INPUT" ]] && base="$INPUT"   # файл без расширения
  OUT="${base}_bassboosted.${OUT_EXT}"
fi
[[ "$OUT" == "$INPUT" ]] && die "Выходной файл совпадает с входным — так нельзя"

# не перезаписываем: при коллизии добавляем _2, _3, ...
if [[ -e "$OUT" ]]; then
  stem="${OUT%.*}"; ext="$(ext_of "$OUT")"; n=2
  while [[ -e "${stem}_${n}.${ext}" ]]; do n=$((n+1)); done
  OUT="${stem}_${n}.${ext}"
  warn "Файл уже существует — сохраняю как: $OUT"
fi

# ---------- кодек аудио по расширению ----------
ENCODERS_CACHE=""
has_encoder() { # has_encoder libmp3lame
  if [[ -z "$ENCODERS_CACHE" ]]; then
    ENCODERS_CACHE="$(LC_ALL=C ffmpeg -hide_banner -encoders 2>/dev/null || true)"
  fi
  grep -qw "$1" <<<"$ENCODERS_CACHE"
}

pick_audio_codec() { # печатает: кодек + аргументы, и реальное расширение через второе значение в stdout
  local ext="$1"
  case "$ext" in
    mp3)          if has_encoder libmp3lame; then echo "libmp3lame -q:a 2|mp3"; else echo "aac -b:a 320k|m4a"; fi ;;
    m4a|aac|mp4|mov|m4v) echo "aac -b:a 320k|$ext" ;;
    flac)         echo "flac|flac" ;;
    wav)          echo "pcm_s16le|wav" ;;
    ogg|opus|webm) if has_encoder libopus; then echo "libopus -b:a 192k|$ext"; else echo "aac -b:a 320k|m4a"; fi ;;
    mkv|avi)      echo "aac -b:a 320k|$ext" ;;
    wma)          if has_encoder wmav2; then echo "wmav2 -b:a 192k|wma"; else echo "aac -b:a 320k|m4a"; fi ;;
    *)            echo "aac -b:a 320k|m4a" ;;
  esac
}

CODEC_LINE="$(pick_audio_codec "$OUT_EXT")"
ACODEC_ARGS="${CODEC_LINE%%|*}"
MAPPED_EXT="${CODEC_LINE##*|}"
# если пришлось сменить кодек (например, нет libmp3lame) — поправим и расширение
if [[ "$MAPPED_EXT" != "$OUT_EXT" ]]; then
  OUT_EXT="$MAPPED_EXT"
  base="${OUT%.*}"; OUT="${base}.${OUT_EXT}"
  if [[ -e "$OUT" ]]; then
    stem="${OUT%.*}"; n=2
    while [[ -e "${stem}_${n}.${OUT_EXT}" ]]; do n=$((n+1)); done
    OUT="${stem}_${n}.${OUT_EXT}"
  fi
  warn "Желаемый кодек недоступен в этом ffmpeg — сохраняю как .${OUT_EXT}"
fi
read -r -a ACODEC <<<"$ACODEC_ARGS"

# ---------- фильтр бас-буста ----------
FILTER="highpass=f=${LOW_CUT},bass=g=${GAIN}:f=${BASS_FREQ}:w=${BASS_WIDTH},acompressor=threshold=${COMP_THRESH}:ratio=${COMP_RATIO}:attack=6:release=120:makeup=2dB"
[[ $LOUDNORM == 1 ]] && FILTER="${FILTER},loudnorm=I=${LOUDNORM_I}:TP=${LOUDNORM_TP}:LRA=${LOUDNORM_LRA},aresample=${IN_RATE:-48000}"
FILTER="${FILTER},alimiter=limit=${LIMITER}"
# loudnorm выдаёт гигантские кадры (>65535 сэмплов) — flac это не ест, режем на 4096
FILTER="${FILTER},asetnsamples=n=4096"

# ---------- запуск ----------
say ""
say "  Вход:      $INPUT"
if [[ $MODE == "video" ]]; then
  say "  Видео:     да (будет скопировано без перекодирования)"
elif [[ $HAS_VIDEO == 1 ]]; then
  say "  Видео:     есть, будет отброшено (аудио-режим)"
else
  say "  Видео:     нет"
fi
[[ $MODE == "audio" && $HAS_COVER == 1 && $HAS_VIDEO == 0 && " mp3 m4a mp4 mov mkv " == *" $OUT_EXT "* ]] && say "  Обложка:   да (сохранится)"
[[ -n "$DURATION" ]] && say "  Длительность: $DURATION"
say "  Бас-буст:  $(awk -v g="$GAIN" 'BEGIN{printf "%+g", g}') дБ @ ${BASS_FREQ} Гц (${PRESET_LABEL})"
say "  Громкость: $([[ $LOUDNORM == 1 ]] && echo "нормализация -14 LUFS" || echo "без нормализации")"
say "  Выход:     $OUT"
say ""

CMD=(ffmpeg -hide_banner -nostdin -y -i "$INPUT")
if [[ $MODE == "video" ]]; then
  CMD+=(-map 0:v -map 0:a:0 -c:v copy)
else
  CMD+=(-map 0:a:0)
  # обложку альбома сохраняем, если контейнер это умеет и настоящего видео в файле нет
  if [[ $HAS_COVER == 1 && $HAS_VIDEO == 0 && " mp3 m4a mp4 mov mkv " == *" $OUT_EXT "* ]]; then
    CMD+=(-map 0:v:0 -c:v copy -disposition:v attached_pic)
  else
    CMD+=(-vn)
  fi
fi
CMD+=(-c:a "${ACODEC[@]}" -af "$FILTER" -sn -map_metadata 0 -map_chapters 0 "$OUT")

"${CMD[@]}" || die "ffmpeg завершился с ошибкой"

# ---------- итог ----------
SIZE="$(du -h "$OUT" 2>/dev/null | awk '{print $1}')"
say ""
ok "Готово! Файл сохранён:"
say "  $OUT${SIZE:+  ($SIZE)}"
