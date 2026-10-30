"""Assemble a 4K cinematic motion-graphic story from timed stills and narration.

The original photo assets are credited in CREDITS.md. The low-resolution
surveillance-style photographs are intentionally treated as monitored footage;
the cat image, typography, HUD and finished video are rendered in 4K.
"""
from pathlib import Path
from functools import lru_cache
import argparse
import json
import math
import re
import shutil
import signal
import subprocess
import textwrap
import time
from uuid import uuid4

import imageio_ffmpeg
import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFont, ImageOps

ROOT=Path(__file__).resolve().parent
WORK=ROOT/'.work'
PHOTOS=ROOT/'assets'/'photos'
FONTS=ROOT/'assets'/'fonts'
OUT=ROOT.parent/'docs'/'guard-story'
W,H=3840,2160
TIMING=json.loads((WORK/'timing.json').read_text(encoding='utf-8'))
STORY=json.loads((ROOT/'script.json').read_text(encoding='utf-8'))
CAPTIONS=TIMING['captions']
SCENES=TIMING['scenes']

# Each cue starts a distinct visual shot. A small number of repeated photographs
# are used as photographic plates; the framing, overlays and camera motion vary.
PLANS=[
 [(0,'guard','film','ПОСТ ОХРАНЫ'),(1,'mall','film','ПОСЛЕ ЗАКРЫТИЯ'),(2,'control-room','film','НОЧНАЯ СМЕНА'),(3,'monitors','cctv','НЕОЖИДАННЫЙ СИГНАЛ')],
 [(0,'logbook','film','ИНСТРУКЦИЯ'),(1,'camera','cctv','ПРОВЕРКА КАМЕР'),(3,'logbook','quote','СМЕНА ПРОХОДИТ СПОКОЙНО'),(4,'control-room','film','ПЕРВАЯ НОЧЬ')],
 [(0,'passage','cctv','ДАТЧИК ДВИЖЕНИЯ'),(1,'guard','film','МОНИТОР НАБЛЮДЕНИЯ'),(2,'passage','cctv','ДВИЖЕНИЕ В КОРИДОРЕ'),(3,'monitors','cctv','ПОВТОР ЗАПИСИ'),(4,'hallway','cctv','ЗАПИСЬ НЕЯСНА')],
 [(0,'camera','cctv','СИГНАЛИЗАЦИЯ АКТИВНА'),(1,'closed-mall','film','ПУСТОЙ ЭТАЖ'),(2,'hallway','film','СТУК В КОРИДОРЕ'),(5,'control-room','film','РАЦИЯ')],
 [(0,'guard','film','СВЯЗЬ СО СТАРШИМ'),(1,'boxes','film','СЛУЖЕБНЫЙ КОРИДОР'),(2,'control-room','film','НА ПОСТУ'),(3,'passage','cctv','ПРОВЕРКА'),(4,'logbook','film','ПЕРЕД ОБХОДОМ')],
 [(0,'hallway','film','ОБХОД'),(2,'closed-mall','film','НЕЯСНОЕ ОТРАЖЕНИЕ'),(3,'passage','cctv','ШАГИ ПОЗАДИ'),(6,'hallway','film','ЭХО')],
 [(0,'boxes','film','УПАВШАЯ КОРОБКА'),(1,'boxes','cctv','ТИХИЙ ШОРОХ'),(2,'hallway','film','ФОНАРИК'),(3,'cat','reveal','НАРУШИТЕЛЬ НАЙДЕН'),(4,'cat','film','СЛУЧАЙНЫЙ ГОСТЬ'),(5,'passage','cctv','ОСТАЛСЯ ВОПРОС')],
 [(0,'cat','film','КТО СТУЧАЛ?'),(2,'boxes','film','ПРИЧИНА ТРЕВОГИ'),(4,'cat','reveal','СОВСЕМ НЕ СТРАШНЫЙ ГОСТЬ')],
 [(0,'control-room','film','НА ПОСТУ'),(1,'guard','film','ДО УТРА'),(2,'cat','film','ДЕЖУРНЫЙ НА СТУЛЕ'),(3,'logbook','quote','ПРИЧИНА ТРЕВОГИ: КОТ')],
 [(0,'mall','dawn','КОНЕЦ НОЧНОЙ СМЕНЫ'),(2,'cat','dawn','ВРЕМЯ ДОМОЙ'),(3,'logbook','quote','НАРУШИТЕЛЬ НАЙДЕН'),(6,'control-room','dawn','ТИХОЙ СМЕНЫ')],
]


def entries():
    entries=[{'start':0.0,'end':TIMING['intro'],'asset':'control-room','kind':'title','label':''}]
    for ix,plan in enumerate(PLANS):
        scene_cues=[c for c in CAPTIONS if c['scene']==ix+1]
        for cue,asset,kind,label in plan:
            entries.append({'start':scene_cues[cue]['start'],'asset':asset,'kind':kind,
                            'label':label,'scene':ix+1})
    entries.append({'start':SCENES[-1]['end'],'asset':'cat','kind':'end','label':''})
    entries.sort(key=lambda e:e['start'])
    for i,e in enumerate(entries):
        e['end']=entries[i+1]['start'] if i+1<len(entries) else TIMING['duration']
    return entries


@lru_cache(maxsize=32)
def font(size,bold=False):
    f=ImageFont.truetype(str(FONTS/'IBM-Plex-Sans.ttf'),int(size))
    try: f.set_variation_by_name('Bold' if bold else 'Medium')
    except OSError: pass
    return f


@lru_cache(maxsize=4)
def vignette():
    # Generate a soft lens falloff at lower resolution, then sample to UHD.
    y,x=np.mgrid[0:540,0:960]
    radius=np.sqrt(((x-480)/530)**2+((y-270)/360)**2)
    a=np.clip((radius-.25)*150,0,148).astype(np.uint8)
    return Image.fromarray(a,'L').resize((W,H),Image.Resampling.BILINEAR)


@lru_cache(maxsize=12)
def dark_gradient():
    x=np.linspace(185,16,960,dtype=np.uint8)
    a=np.tile(x,(540,1))
    return Image.fromarray(a,'L').resize((W,H),Image.Resampling.BILINEAR)


@lru_cache(maxsize=40)
def plate(name,kind):
    source=Image.open(PHOTOS/(name+'.jpg')).convert('RGB')
    center=(.50,.28 if name=='cat' else .50)
    photo=ImageOps.fit(source,(W,H),method=Image.Resampling.LANCZOS,centering=center)
    if kind=='cctv':
        photo=ImageEnhance.Color(photo).enhance(.25)
        photo=ImageEnhance.Contrast(photo).enhance(1.08)
        tint=Image.new('RGB',(W,H),(14,39,47))
        photo=Image.blend(photo,tint,.28)
        photo=ImageEnhance.Brightness(photo).enhance(.74)
    elif kind=='dawn':
        photo=ImageEnhance.Color(photo).enhance(.60)
        photo=Image.blend(photo,Image.new('RGB',(W,H),(55,42,30)),.17)
        photo=ImageEnhance.Brightness(photo).enhance(.85)
    else:
        photo=ImageEnhance.Color(photo).enhance(.48)
        photo=ImageEnhance.Contrast(photo).enhance(1.08)
        photo=Image.blend(photo,Image.new('RGB',(W,H),(12,28,37)),.22)
        photo=ImageEnhance.Brightness(photo).enhance(.80)
    photo.paste((2,8,13),(0,0,W,H),vignette())
    return photo


def draw_rule(d,x,y,w,col=(208,162,91),width=3):
    d.line((x,y,x+w,y),fill=col,width=width)


def draw_corner(d,x,y,horizontal,vertical,color,width=5,length=96):
    d.line((x,y,x+horizontal*length,y),fill=color,width=width)
    d.line((x,y,x,y+vertical*length),fill=color,width=width)


def render_still(spec):
    kind=spec['kind']; base_kind='film' if kind in ('title','end','quote','reveal') else kind
    im=plate(spec['asset'],base_kind).copy()
    d=ImageDraw.Draw(im)
    # Native-resolution cinematic bars and restrained, legible typography.
    d.rectangle((0,0,W,145),fill=(8,14,19))
    d.rectangle((0,1930,W,H),fill=(8,14,19))
    d.line((0,144,W,144),fill=(74,91,96),width=2)
    d.line((0,1930,W,1930),fill=(74,91,96),width=2)
    d.text((180,48),'НОЧНАЯ СМЕНА',font=font(42,True),fill=(221,224,220))
    d.text((1860,51),'ИСТОРИЯ ОХРАННИКА',font=font(36),fill=(140,157,158))
    if kind=='title':
        im.paste((4,12,17),(0,0,W,H),dark_gradient())
        d=ImageDraw.Draw(im)
        d.text((220,410),'ОДНА ИСТОРИЯ / 00:17',font=font(54,True),fill=(203,175,126))
        draw_rule(d,220,510,340,(203,175,126),5)
        d.text((210,610),'НОЧНАЯ',font=font(270,True),fill=(238,239,230))
        d.text((210,915),'СМЕНА',font=font(270,True),fill=(238,239,230))
        d.text((222,1320),'Когда тишина вдруг закончилась.',font=font(72),fill=(209,216,211))
    elif kind=='end':
        im.paste((5,10,13),(0,0,W,H),dark_gradient())
        d=ImageDraw.Draw(im)
        d.text((220,710),'КОНЕЦ СМЕНЫ',font=font(155,True),fill=(238,239,230))
        draw_rule(d,230,960,440,(203,175,126),5)
        d.text((230,1030),'Оригинальная история • синтезированная озвучка',font=font(55),fill=(207,215,210))
    else:
        # Small editorial label, well away from subtitle region.
        d.rectangle((170,248,184,360),fill=(207,163,96))
        d.text((225,242),spec['label'],font=font(58,True),fill=(237,237,223),
               stroke_width=1,stroke_fill=(10,18,23))
        scene=spec.get('scene',1)
        d.text((W-555,50),f'ЗАПИСЬ  {scene:02} / 10',font=font(36),fill=(170,186,182))
        if kind=='quote':
            # The quote is native 4K text layered over the actual notebook photo.
            panel=Image.new('RGBA',(W,H),(0,0,0,0));pd=ImageDraw.Draw(panel)
            pd.rounded_rectangle((225,620,2510,1060),radius=14,fill=(6,13,18,190))
            im=Image.alpha_composite(im.convert('RGBA'),panel).convert('RGB');d=ImageDraw.Draw(im)
            txt=spec['label']
            f=font(110,True)
            d.text((305,765),txt,font=f,fill=(239,228,198))
            draw_rule(d,305,952,260,(203,175,126),4)
        if kind=='cctv':
            edge=(167,196,192)
            for x,y,hs,vs in [(190,210,1,1),(3650,210,-1,1),(190,1850,1,-1),(3650,1850,-1,-1)]:
                draw_corner(d,x,y,hs,vs,edge,4,105)
            for y in range(180,1910,38):
                d.line((0,y,W,y),fill=(39,61,62),width=1)
            d.text((220,1775),'НАБЛЮДЕНИЕ  /  КАМЕРА 03',font=font(42),fill=(177,202,198))
    return im


def ass_time(t):
    centis=max(0,round(t*100));h,centis=divmod(centis,360000);m,centis=divmod(centis,6000);s,cs=divmod(centis,100)
    return f'{h}:{m:02}:{s:02}.{cs:02}'


def clean_ass(s):
    return s.replace('\\','\\\\').replace('{','(').replace('}',')').replace('\n',r'\N')


def caption_wrap(text):
    # 4K font, kept to at most roughly two readable lines.
    return r'\N'.join(textwrap.wrap(text,width=70,break_long_words=False,break_on_hyphens=False))


def build_ass(shots):
    head='''[Script Info]
ScriptType: v4.00+
PlayResX: 3840
PlayResY: 2160
WrapStyle: 2
ScaledBorderAndShadow: yes
YCbCr Matrix: TV.709

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Subtitle,IBM Plex Sans,90,&H00F4F6F4,&H000000FF,&HCC101820,&H7D101820,-1,0,0,0,100,100,0,0,3,3,0,2,200,200,124,1
Style: Hud,DejaVu Sans Mono,47,&H00D6E9E7,&H000000FF,&H9911191B,&H9911191B,0,0,0,0,100,100,1,0,1,2,0,7,0,0,0,1
Style: Alert,DejaVu Sans Mono,50,&H00647DEB,&H000000FF,&H9911191B,&H9911191B,-1,0,0,0,100,100,0,0,1,2,0,7,0,0,0,1
Style: Vector,DejaVu Sans,40,&H00DBE5E1,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,0,0,7,0,0,0,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
'''
    rows=[head]
    for c in CAPTIONS:
        text=caption_wrap(clean_ass(c['text']))
        rows.append(f"Dialogue: 8,{ass_time(c['start'])},{ass_time(c['end'])},Subtitle,,0,0,0,,{{\\fad(90,90)}}{text}\n")
    for shot in shots:
        if shot['kind']!='cctv':continue
        start,end=shot['start'],shot['end']
        rows.append(f"Dialogue: 3,{ass_time(start)},{ass_time(end)},Hud,,0,0,0,,{{\\pos(2860,280)\\fad(150,150)}}CAM 03 / LIVE\n")
        second=math.floor(start)
        while second<end:
            a=max(start,second);b=min(end,second+1)
            if b>a:
                clock=17+max(0,round(second-45))//60
                stamp=f'00:{clock:02}:{max(0,round(second-45))%60:02}'
                rows.append(f"Dialogue: 3,{ass_time(a)},{ass_time(b)},Hud,,0,0,0,,{{\\pos(2860,350)}}{stamp}\n")
                if second%2==0:
                    rows.append(f"Dialogue: 4,{ass_time(a)},{ass_time(b)},Alert,,0,0,0,,{{\\pos(3450,280)}}● REC\n")
            second+=1
        # An animated, faint scan line travels across the surveillance plate.
        st=start
        while st<end:
            finish=min(end,st+4.5);ms=round((finish-st)*1000)
            shape=r'm 0 0 l 3330 0 3330 3 0 3'
            rows.append(f"Dialogue: 2,{ass_time(st)},{ass_time(finish)},Vector,,0,0,0,,{{\\an7\\move(250,220,250,1830,0,{ms})\\alpha&HCC&\\p1}}{shape}{{\\p0}}\n")
            st=finish
    # A distant shape crosses the monitored passage; it is deliberately small
    # and indistinct, like an ambiguous security-camera image.
    for shot in shots:
        if shot['label']=='ДВИЖЕНИЕ В КОРИДОРЕ':
            start=shot['start']+.55; end=min(shot['end'],start+2.0)
            shape=('m 62 8 b 89 8 103 32 103 55 b 103 78 88 98 72 99 '
                   'l 106 205 98 250 87 250 77 190 63 247 46 247 47 185 '
                   'l 34 245 20 243 30 203 45 100 b 26 95 18 75 20 57 '
                   'b 20 27 38 8 62 8')
            rows.append(f"Dialogue: 2,{ass_time(start)},{ass_time(end)},Vector,,0,0,0,,{{\\an7\\move(1860,1120,2110,1120)\\c&H05080A&\\alpha&H80&\\blur7\\p1}}{shape}{{\\p0}}\n")
        elif shot['label']=='ФОНАРИК':
            start=shot['start']+.15; end=shot['end']; ms=round((end-start)*1000)
            shape='m 0 0 l 1280 200 1280 680 0 170'
            rows.append(f"Dialogue: 2,{ass_time(start)},{ass_time(end)},Vector,,0,0,0,,{{\\an7\\move(470,860,1850,760,0,{ms})\\c&HBDD7E0&\\alpha&HEE&\\blur30\\p1}}{shape}{{\\p0}}\n")
    # Progress marker moves under the lower letterbox without obscuring captions.
    rows.append('Dialogue: 1,0:00:00.00,0:04:00.00,Vector,,0,0,0,,{\\an7\\move(0,2150,3840,2150)\\c&H80B7C9&\\p1}m -36 0 l 0 0 0 10 -36 10{\\p0}\n')
    path=WORK/'overlay.ass';path.write_text(''.join(rows),encoding='utf8');return path


def build_stills(preview_only=False):
    shots=entries();folder=WORK/'shots';folder.mkdir(parents=True,exist_ok=True)
    print(f'Preparing {len(shots)} 4K photographic compositions',flush=True)
    for i,shot in enumerate(shots):
        im=render_still(shot)
        if preview_only:
            if i in (0,1,8,15,22,29,34,len(shots)-1):
                p=WORK/f'preview-{i:02}.jpg'
                im.resize((1280,720),Image.Resampling.LANCZOS).save(p,quality=90)
        else:
            p=folder/f'{i:03}.jpg'
            im.save(p,quality=86,subsampling=0,optimize=True)
        if i==0:
            OUT.mkdir(parents=True,exist_ok=True)
            im.resize((1920,1080),Image.Resampling.LANCZOS).save(OUT/'poster.jpg',quality=90,optimize=True)
        if not preview_only and (i+1)%10==0:print(f'  {i+1}/{len(shots)} compositions',flush=True)
    if preview_only:return
    lines=['ffconcat version 1.0']
    for i,shot in enumerate(shots):
        lines += [f"file '{(folder/f'{i:03}.jpg').as_posix()}'",f"duration {shot['end']-shot['start']:.6f}"]
    # concat demuxer needs the last still repeated for the last duration to apply.
    lines.append(f"file '{(folder/f'{len(shots)-1:03}.jpg').as_posix()}'")
    (WORK/'storyboard.ffconcat').write_text('\n'.join(lines)+'\n',encoding='utf8')
    build_ass(shots)
    (WORK/'shot-timing.json').write_text(json.dumps(shots,ensure_ascii=False,indent=2),encoding='utf8')
    print('Compositions, subtitles and shot timing ready',flush=True)


def run_guarded(cmd):
    """Stop the encoder child as well if the build is interrupted."""
    proc=subprocess.Popen(cmd)
    previous=signal.getsignal(signal.SIGTERM)
    def interrupted(signum, frame):
        raise SystemExit(128+signum)
    signal.signal(signal.SIGTERM,interrupted)
    try:
        status=proc.wait()
    finally:
        if proc.poll() is None:
            proc.kill()
            proc.wait()
        signal.signal(signal.SIGTERM,previous)
    if status:
        raise RuntimeError(f'FFmpeg failed with exit code {status}')


def encode(limit=None):
    ff=imageio_ffmpeg.get_ffmpeg_exe()
    # Unique paths prevent overlapping or interrupted builds corrupting one another.
    temporary=WORK/f'rendering-4k-{uuid4().hex}.mp4'
    final=OUT/'nochnaya-smena-4k.mp4'
    # libass scans every file in fontsdir, so keep the text license elsewhere.
    staged_fonts=WORK/'fonts'
    staged_fonts.mkdir(exist_ok=True)
    shutil.copy2(FONTS/'IBM-Plex-Sans.ttf',staged_fonts/'IBM-Plex-Sans.ttf')
    # Zoom and pan move the camera through the photographic plates; ASS
    # typography is applied afterward so captions stay crisp at 4K.
    filters=("fps=24,zoompan=z='1.055+0.025*sin(on/140)':"
             "x='(iw-iw/zoom)/2+40*sin(on/70)':"
             "y='(ih-ih/zoom)/2+20*cos(on/90)':d=1:s=3840x2160:fps=24,"
             f"ass=filename='{(WORK/'overlay.ass').as_posix()}':fontsdir='{staged_fonts.as_posix()}',format=yuv420p")
    length=limit or TIMING['duration']
    cmd=[ff,'-hide_banner','-y','-loglevel','error','-stats_period','5','-progress','pipe:1','-nostats',
         '-f','concat','-safe','0','-i',str(WORK/'storyboard.ffconcat'),
         '-i',str(WORK/'mix.wav'),'-map','0:v:0','-map','1:a:0',
         '-vf',filters,'-c:v','libx264','-preset','superfast','-crf','22',
         '-maxrate','2600k','-bufsize','5200k','-pix_fmt','yuv420p',
         '-c:a','aac','-b:a','128k','-ar','44100','-t',str(length),
         '-threads','2','-movflags','+faststart',str(temporary)]
    print(f'Encoding {length}s in 3840×2160 / 24 fps',flush=True)
    now=time.monotonic()
    try:
        run_guarded(cmd)
        size=temporary.stat().st_size
        print(f'Encoded {size/1024/1024:.1f} MiB in {time.monotonic()-now:.1f}s',flush=True)
        if not limit and size>=100_000_000:
            raise RuntimeError('The MP4 exceeds GitHub’s 100 MB file limit')
        # A zero encoder exit code alone does not guarantee a decodable MP4.
        print('Verifying every video frame and audio packet',flush=True)
        run_guarded([ff,'-v','error','-xerror','-i',str(temporary),
                     '-map','0:v:0','-map','0:a:0','-f','null','-'])
        if limit:
            temporary.replace(WORK/'test-4k.mp4')
        else:
            temporary.replace(final)
            print(f'Finished: {final}',flush=True)
    finally:
        temporary.unlink(missing_ok=True)


if __name__=='__main__':
    p=argparse.ArgumentParser()
    p.add_argument('--previews',action='store_true',help='render some still previews only')
    p.add_argument('--prepare',action='store_true',help='prepare all 4K stills and subtitles')
    p.add_argument('--encode',action='store_true',help='encode already-prepared video')
    p.add_argument('--test',type=float,help='encode this many seconds for a technical check')
    args=p.parse_args()
    if args.previews:build_stills(preview_only=True)
    elif args.prepare:build_stills()
    elif args.encode or args.test:encode(args.test)
    else:build_stills();encode()
