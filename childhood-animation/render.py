"""Render a 4-minute original motion-comic to H.264 MP4.

Run make_audio.py first. --stills produces inexpensive composition previews;
without it, raw RGB frames are piped to FFmpeg, never stored as thousands of
large intermediate images.
"""
import argparse
import bisect
from functools import lru_cache
import json
import math
from pathlib import Path
import subprocess
import time

import imageio_ffmpeg
import numpy as np
from PIL import Image, ImageDraw

import art

ROOT=Path(__file__).resolve().parent
WORK=ROOT/'.work'
DOCS=ROOT.parent/'docs'/'childhood-animation'
TIMING=json.loads((WORK/'timing.json').read_text(encoding='utf-8'))
ENVELOPE=np.load(WORK/'voice_envelope.npy')
SCENES=TIMING['scenes']
CAPTIONS=TIMING['captions']
STARTS=[s['start']-0.325 if i else 0.0 for i,s in enumerate(SCENES)]
CAP_STARTS=[s['start'] for s in CAPTIONS]
W,H=art.W,art.H
FPS=TIMING['fps']
DURATION=TIMING['duration']

# Four shot designs per chapter. Backgrounds, expressions, props and camera
# framing change throughout, rather than holding on one illustration.
SHOTS=[
 [
  dict(bg='yard',cast='center',poses=('walk','walk'),tag='КАНИКУЛЫ!',fx='sparkle'),
  dict(bg='fort',cast='boys',poses=('point','cheer'),tag='СЕКРЕТНЫЙ ПЛАН',fx='sparkle'),
  dict(bg='laundry',cast='boys',poses=('scared','scared'),emotion='shocked',prop='flyblanket',tag='ВЕТЕР?!',fx='wind'),
  dict(bg='yard',cast='center',poses=('walk','walk'),tag='ЛЕТО ВО ДВОРЕ',fx='sparkle'),
 ],
 [
  dict(bg='playground',cast='boys',poses=('point','point'),prop='box',tag='НАХОДКА ВЕКА!'),
  dict(bg='fantasy',cast='boys',poses=('point','cheer'),prop='rocket',tag='В КОСМОС!',fx='sparkle'),
  dict(bg='playground',cast='boys',poses=('relax','point'),prop='icecream',tag='НОЛЬ РУБЛЕЙ',emotion='worried'),
  dict(bg='playground',cast='boys',poses=('point','cheer'),prop='box',tag='ЕСТЬ ИДЕЯ!',fx='sparkle'),
 ],
 [
  dict(bg='playground',cast='boys',poses=('point','relax'),prop='board',tag='ВАЖНЫЕ ДЕЛА'),
  dict(bg='playground',cast='boys',poses=('wave','cheer'),prop='board',tag='ДВА НАЧАЛЬНИКА'),
  dict(bg='playground',cast='boys',poses=('point','point'),prop='board',tag='ПРАВИЛА ШТАБА'),
  dict(bg='playground',cast='boys',poses=('point','wave'),prop='board',tag='ПАРОЛЬ?',fx='question'),
 ],
 [
  dict(bg='yard',cast='boys',poses=('point','relax'),prop='box',tag='НУЖНА КРЫША',emotion='worried'),
  dict(bg='laundry',cast='boys',poses=('point','cheer'),prop='hangingblanket',tag='НАХОДКА!'),
  dict(bg='laundry',cast='boys',poses=('cheer','cheer'),prop='carriedblanket',tag='ТИХОНЬКО...'),
  dict(bg='yard',cast='boys',poses=('walk','walk'),prop='blanket',tag='ОЙ...',emotion='sly'),
 ],
 [
  dict(bg='fort',cast='boys',poses=('cheer','cheer'),tag='ГОТОВО!',fx='sparkle'),
  dict(bg='fort',cast='boys',poses=('point','point'),prop='box',tag='СЕКРЕТНЫЙ ЗНАК'),
  dict(bg='fantasy',cast='boys',poses=('point','cheer'),prop='castle',tag='ЦЕЛЫЙ ДВОРЕЦ!',fx='sparkle'),
  dict(bg='fort',cast='boys',poses=('relax','wave'),tag='ОЧЕНЬ НАДЁЖНО',emotion='sly'),
 ],
 [
  dict(bg='kitchen',cast='grandma',poses=('point','wave'),tag='ПРИПАСЫ!'),
  dict(bg='kitchen',cast='grandma',poses=('cheer','point'),tag='СЕКРЕТНАЯ УГРОЗА'),
  dict(bg='kitchen',cast='grandma',poses=('point','cheer'),prop='sandwiches3',tag='ТРИ БУТЕРБРОДА'),
  dict(bg='kitchen',cast='grandma',poses=('wave','relax'),prop='sandwiches3',tag='НА ВСЯКИЙ СЛУЧАЙ'),
 ],
 [
  dict(bg='fort',cast='boys',poses=('point','point'),prop='stock3',tag='ПРИПАСЫ'),
  dict(bg='fort',cast='boys',poses=('scared','scared'),prop='stock2',tag='ОДИН ПРОПАЛ!',emotion='shocked',fx='question'),
  dict(bg='fort',cast='boys',poses=('scared','scared'),prop='stock0',tag='ШПИОН?!',emotion='shocked',fx='question'),
  dict(bg='yard',cast='boys',poses=('hide','hide'),prop='box',tag='ТИШЕ, ТИШЕ!',emotion='worried'),
 ],
 [
  dict(bg='fort',cast='boys',poses=('point','point'),prop='trap',tag='ЛОВУШКА'),
  dict(bg='fort',cast='boys',poses=('cheer','wave'),prop='trap',tag='ПЛАН ИДЕАЛЕН!'),
  dict(bg='playground',cast='hide',poses=('hide','hide'),prop='clock',tag='ЖДЁМ...'),
  dict(bg='playground',cast='hide',poses=('relax','point'),prop='clock',tag='ПОСЛЕ ОБЕДА?',emotion='worried'),
 ],
 [
  dict(bg='fort',cast='boys',poses=('scared','scared'),prop='trap',tag='ЗВЯК!',emotion='shocked',fx='ring'),
  dict(bg='fort',cast='cat',poses=('scared','point'),tag='КОТ?!',emotion='shocked',fx='question'),
  dict(bg='yard',cast='boys',poses=('scared','scared'),prop='flyblanket',tag='ВЕТЕР!',emotion='shocked',fx='wind'),
  dict(bg='laundry',cast='aunt',poses=('hide','hide'),prop='blanket',tag='ОЙ-ОЙ...',emotion='worried'),
 ],
 [
  dict(bg='laundry',cast='aunt',poses=('hide','hide'),prop='blanket',tag='ИЗВИНИТЕ...',emotion='worried'),
  dict(bg='laundry',cast='aunt',poses=('wave','cheer'),prop='blanket',tag='НОВАЯ КРЫША!',fx='sparkle'),
  dict(bg='fort',cast='group',poses=('wave','cheer'),tag='ВСЕ В ШТАБ!',fx='sparkle'),
  dict(bg='fort',cast='group',poses=('cheer','wave'),tag='СВОИ ЛЮДИ',fx='sparkle'),
 ],
]

# Shot changes follow the story's major ideas rather than a fixed metronome.
# The boundaries roughly coincide with the corresponding subtitle cues.
BEAT_STARTS=[
 [0,.16,.41,.64], [0,.33,.46,.67], [0,.17,.43,.77],
 [0,.20,.51,.78], [0,.27,.42,.66], [0,.16,.44,.68],
 [0,.22,.335,.58], [0,.22,.53,.80], [0,.25,.72,.91],
 [0,.285,.52,.75],
]


def ease(v):
    v=max(0,min(1,v))
    return 1-(1-v)**3


@lru_cache(maxsize=10)
def oversized_background(kind):
    # The tiny slow pan adds life without making text or characters jitter.
    return art.background(kind).resize((990,557),Image.Resampling.LANCZOS)


def background(kind,t):
    bg=oversized_background(kind)
    x=int(15+10*math.sin(t*.15))
    y=int(8+5*math.sin(t*.11+1.2))
    return bg.crop((x,y,x+W,y+H))


@lru_cache(maxsize=512)
def scaled_actor(kind,pose,emotion,mouth,blink,height):
    if kind in ('hero','friend'):
        img=art.boy(kind,pose,emotion,mouth,blink)
    elif kind in ('grandma','aunt'):
        img=art.grownup(kind,pose,emotion,mouth,blink)
    elif kind=='cat':
        img=art.cat(blink,bool(mouth))
    else:
        raise KeyError(kind)
    width=round(img.width*height/img.height)
    return img.resize((width,height),Image.Resampling.LANCZOS)


@lru_cache(maxsize=128)
def scaled_prop(name,height,angle=0):
    img=art.prop(name)
    width=round(img.width*height/img.height)
    img=img.resize((width,height),Image.Resampling.LANCZOS)
    if angle:
        img=img.rotate(angle,resample=Image.Resampling.BICUBIC,expand=True)
    return img


def paste(dst,img,cx,bottom):
    dst.paste(img,(round(cx-img.width/2),round(bottom-img.height)),img)


def place_prop(dst,name,x,bottom,height,angle=0):
    paste(dst,scaled_prop(name,round(height),round(angle/5)*5),x,bottom)


def subtitle_lines(text, f, max_width=805):
    d=ImageDraw.Draw(Image.new('RGB',(1,1)))
    lines=[]; line=''
    for word in text.split():
        trial=(line+' '+word).strip()
        if line and d.textlength(trial,font=f)>max_width:
            lines.append(line); line=word
        else:
            line=trial
    if line: lines.append(line)
    return lines


@lru_cache(maxsize=128)
def caption_card(text):
    f=art.font(24)
    lines=subtitle_lines(text,f)
    tmp=ImageDraw.Draw(Image.new('RGB',(1,1)))
    text_w=math.ceil(max((tmp.textlength(line,font=f) for line in lines),default=0))
    w=min(W-58,text_w+42); h=20+len(lines)*30
    im=Image.new('RGBA',(w+8,h+8),(0,0,0,0)); d=ImageDraw.Draw(im)
    d.rounded_rectangle((5,6,w+4,h+5),radius=12,fill=(45,55,60,70))
    d.rounded_rectangle((0,0,w,h),radius=12,fill=(35,48,65,231),outline=(255,245,220,210),width=1)
    for i,line in enumerate(lines):
        tw=d.textlength(line,font=f)
        d.text(((w-tw)/2,7+i*30),line,font=f,fill='#fffdf5',stroke_width=0)
    return im


@lru_cache(maxsize=20)
def chapter_header(number,title):
    f=art.font(19,True)
    d0=ImageDraw.Draw(Image.new('RGB',(1,1)))
    tw=math.ceil(d0.textlength(title,font=f))
    w=tw+72
    im=Image.new('RGBA',(w+8,52),(0,0,0,0)); d=ImageDraw.Draw(im)
    d.rounded_rectangle((4,6,w+4,48),radius=11,fill=(101,73,64,100))
    d.rounded_rectangle((0,2,w,44),radius=11,fill='#fff4d9',outline=art.INK,width=3)
    d.rounded_rectangle((6,8,43,38),radius=8,fill='#ef9c80',outline=art.INK,width=2)
    nf=art.font(16,True)
    d.text((13,11),f'{number:02}',font=nf,fill=art.INK)
    d.text((52,10),title,font=f,fill=art.INK)
    return im


@lru_cache(maxsize=80)
def tag_card(text):
    lines=text.split('\n')
    fs=21 if len(text)<20 else 18
    f=art.font(fs,True)
    d0=ImageDraw.Draw(Image.new('RGB',(1,1)))
    w=math.ceil(max(d0.textlength(x,font=f) for x in lines))+28
    h=len(lines)*(fs+4)+16
    im=Image.new('RGBA',(w+8,h+9),(0,0,0,0)); d=ImageDraw.Draw(im)
    d.rounded_rectangle((5,6,w+5,h+6),radius=11,fill=(95,61,54,95))
    d.rounded_rectangle((0,0,w,h),radius=11,fill='#fff8e5',outline=art.INK,width=3)
    for i,line in enumerate(lines):
        d.text((14,7+i*(fs+4)),line,font=f,fill=art.INK)
    return im


@lru_cache(maxsize=2)
def title_card():
    im=Image.new('RGBA',(760,240),(0,0,0,0)); d=ImageDraw.Draw(im)
    d.rounded_rectangle((12,16,754,233),radius=26,fill=(79,62,56,95))
    d.rounded_rectangle((4,4,746,222),radius=26,fill='#fff0cb',outline=art.INK,width=5)
    d.rounded_rectangle((18,18,732,208),radius=18,outline='#e1b884',width=2)
    f1=art.font(30,True); f2=art.font(50,True); f3=art.font(21)
    d.text((375,53),'КАК МЫ ПОСТРОИЛИ',font=f1,fill=art.INK,anchor='mm')
    d.text((375,109),'СЕКРЕТНЫЙ ШТАБ',font=f2,fill='#ef8071',stroke_width=2,stroke_fill=art.INK,anchor='mm')
    d.text((375,171),'одна история из детства',font=f3,fill='#516174',anchor='mm')
    return im


@lru_cache(maxsize=2)
def end_card():
    im=Image.new('RGBA',(600,145),(0,0,0,0)); d=ImageDraw.Draw(im)
    d.rounded_rectangle((5,7,594,139),radius=21,fill=(81,58,54,80))
    d.rounded_rectangle((0,0,588,132),radius=21,fill='#fff1d0',outline=art.INK,width=4)
    d.text((294,48),'ДО НОВЫХ ИСТОРИЙ!',font=art.font(30,True),fill=art.INK,anchor='mm')
    d.text((294,96),'Оригинальная история • синтез речи',font=art.font(18),fill='#6f7780',anchor='mm')
    return im


def drawn_prop(frame,name,t,phase):
    d=ImageDraw.Draw(frame)
    bob=4*math.sin(t*2.3)
    if name=='box':
        place_prop(frame,'box',731,454,156)
    elif name in ('rocket','icecream','castle','board'):
        size={'rocket':263,'icecream':225,'castle':230,'board':255}[name]
        place_prop(frame,name,733,448+bob*.35,size)
    elif name=='blanket':
        place_prop(frame,'blanket',720,402+bob,125,5*math.sin(t*2))
    elif name=='hangingblanket':
        place_prop(frame,'blanket',445,266+bob*.25,120,2*math.sin(t*2))
    elif name=='carriedblanket':
        place_prop(frame,'blanket',366,203+bob,120,4*math.sin(t*3))
    elif name=='flyblanket':
        # A proper travelling object, not a still: the sheet sweeps across the frame.
        x=365+min(1,phase)*480
        y=190+40*math.sin(phase*math.pi)+bob
        place_prop(frame,'blanket',x,y,116,20*math.sin(t*3))
    elif name=='clock':
        place_prop(frame,'clock',738,350,153)
        # Extra revolving hand for the waiting gag.
        a=t*1.7; x,y=738,274
        d.line([(x,y),(x+41*math.sin(a),y-41*math.cos(a))],fill=art.INK,width=3)
        d.ellipse((x-4,y-4,x+4,y+4),fill=art.INK)
    elif name=='trap':
        d.line([(612,303),(794,303),(794,359)],fill='#785e53',width=3)
        angle=round(12*math.sin(t*7)/5)*5
        place_prop(frame,'cup',794,397,76,angle)
        d.line([(606,301),(606,389)],fill='#785e53',width=3)
    elif name and (name.startswith('sandwiches') or name.startswith('stock')):
        count=int(name[-1])
        if name.startswith('stock'):
            place_prop(frame,'box',733,449,145)
            y=372; center=732
        else:
            # The plate is placed between the children and Grandma, in front
            # of the cast, so all three sandwiches remain visible.
            center=570
            d.ellipse((center-122,350,center+123,406),fill='#fff2d9',outline=art.INK,width=3)
            y=389
        for i in range(count):
            x=center+(i-(count-1)/2)*58
            place_prop(frame,'sandwich',x,y,61)


def draw_fx(frame,kind,t):
    d=ImageDraw.Draw(frame)
    if kind=='sparkle':
        for i,(x,y,r) in enumerate([(112,138,12),(819,155,14),(584,117,9),(96,304,8)]):
            rr=max(4,int(r*(.68+.32*math.sin(t*3+i))))
            art.sparkle(frame,x,y,rr)
    elif kind=='wind':
        for i in range(5):
            x=(int(t*115)+i*212)%1160-140
            y=155+i*41
            d.arc((x,y,x+122,y+28),180,345,fill='#fff9d8',width=4)
            d.arc((x+30,y+10,x+91,y+24),180,335,fill='#f2d795',width=2)
    elif kind=='question':
        for i,x in enumerate((188,525)):
            y=98+6*math.sin(t*3+i)
            d.text((x,y),'?',font=art.font(45,True),fill='#f4b75f',stroke_width=2,stroke_fill=art.INK)
    elif kind=='ring':
        for x in (738,850):
            d.line([(x,327),(x-8 if x<800 else x+8,315)],fill='#e97867',width=4)
            d.line([(x,340),(x-12 if x<800 else x+12,340)],fill='#e97867',width=4)


def animated_pose(base,t,offset):
    if base=='walk':
        return 'walk' if int(t*3.6+offset)%2==0 else 'walk2'
    # Gentle intermittent gestures make characters perform, not just slide.
    if base=='relax' and (t+offset)%5.1<.65:
        return 'wave'
    if base=='point' and (t+offset)%4.8<.42:
        return 'relax'
    if base=='cheer' and (t+offset)%3.9<.35:
        return 'wave'
    return base


def draw_cast(frame,cfg,t,shot_elapsed,env):
    cast=cfg['cast']; poses=cfg.get('poses',('relax','relax'))
    emotion=cfg.get('emotion','happy')
    if cast=='center':
        people=[('hero',350,449,310,poses[0]),('friend',558,449,300,poses[1])]
    elif cast=='hide':
        people=[('hero',270,457,245,poses[0]),('friend',458,457,238,poses[1])]
    elif cast=='grandma':
        people=[('hero',200,451,288,poses[0]),('friend',378,451,280,poses[1]),('grandma',735,451,320,'offer')]
    elif cast=='aunt':
        people=[('hero',200,451,285,poses[0]),('friend',382,451,278,poses[1]),('aunt',737,451,320,'offer')]
    elif cast=='cat':
        people=[('hero',220,450,305,poses[0]),('friend',409,450,295,poses[1]),('cat',741,444,126,'relax')]
    elif cast=='group':
        people=[('hero',148,447,265,poses[0]),('friend',312,447,255,poses[1]),
                ('grandma',500,447,286,'offer'),('aunt',663,447,286,'wave'),('cat',823,440,108,'relax')]
    else:
        people=[('hero',280,450,310,poses[0]),('friend',482,450,300,poses[1])]
    entrance=ease(shot_elapsed/.55)
    for i,(kind,cx,bottom,height,pose) in enumerate(people):
        phase=t*3.0+i*1.7
        bob=2.4*math.sin(phase)
        if pose=='walk':
            bob=4*abs(math.sin(phase*1.7))
        x=cx+(1-entrance)*(-28 if i%2==0 else 28)+1.7*math.sin(t*1.8+i)
        y=bottom-bob+(1-entrance)*12
        blink=((t+i*.73)%4.8)<.16
        if kind=='hero':
            mouth=2 if env>.12 else (1 if env>.024 else 0)
        elif kind=='cat':
            mouth=1 if cast in ('cat','group') else 0
        else:
            # The second child reacts occasionally; one narrator voices the whole story.
            mouth=1 if (t+i*1.3)%7.5<.22 and env>.06 else 0
        pp=animated_pose(pose,t,i*.6)
        im=scaled_actor(kind,pp,emotion,mouth,blink,height)
        paste(frame,im,x,y)


def choose_shot(t):
    ix=max(0,min(len(SCENES)-1,bisect.bisect_right(STARTS,t)-1))
    scene=SCENES[ix]
    local=max(0,min(.999999,(t-scene['start'])/scene['duration']))
    boundaries=BEAT_STARTS[ix]
    beat=max(0,min(3,bisect.bisect_right(boundaries,local)-1))
    shot_start=scene['start']+boundaries[beat]*scene['duration']
    next_fraction=boundaries[beat+1] if beat<3 else 1.0
    phase=(local-boundaries[beat])/(next_fraction-boundaries[beat])
    elapsed=max(0,t-shot_start)
    return ix,beat,elapsed,phase


def draw_intro(t):
    frame=background('yard',t)
    # Characters peek around the central title with a little bounce.
    for i,(kind,cx,pose) in enumerate([('hero',126,'wave'),('friend',834,'cheer')]):
        im=scaled_actor(kind,animated_pose(pose,t,i),'happy',0,False,270)
        paste(frame,im,cx,452-2*math.sin(t*2+i))
    card=title_card()
    slide=(1-ease(t/.7))*-40
    frame.paste(card,(100,round(92+slide+2*math.sin(t*1.6))),card)
    for i,(x,y) in enumerate([(60,115),(879,118),(65,285),(885,279)]):
        art.sparkle(frame,x,y,9+int(3*abs(math.sin(t*2+i))))
    return frame


def draw_ending(t):
    frame=background('fort',t)
    cfg=dict(cast='group',poses=('cheer','wave'),emotion='happy')
    draw_cast(frame,cfg,t,2,0)
    card=end_card()
    frame.paste(card,(186,45+round(2*math.sin(t*1.5))),card)
    draw_fx(frame,'sparkle',t)
    return frame


def render_frame(n):
    t=n/FPS
    if t<TIMING['intro']:
        return draw_intro(t)
    if t>=SCENES[-1]['end']:
        return draw_ending(t)
    ix,beat,elapsed,phase=choose_shot(t)
    cfg=SHOTS[ix][beat]
    frame=background(cfg['bg'],t)
    foreground_prop=cfg.get('prop','').startswith('sandwiches')
    if cfg.get('prop') and not foreground_prop:
        drawn_prop(frame,cfg['prop'],t,phase)
    env=float(ENVELOPE[min(n,len(ENVELOPE)-1)])
    draw_cast(frame,cfg,t,elapsed,env)
    if foreground_prop:
        drawn_prop(frame,cfg['prop'],t,phase)
    if cfg.get('fx'):
        draw_fx(frame,cfg['fx'],t)
    header=chapter_header(ix+1,SCENES[ix]['title'])
    frame.paste(header,(22,14),header)
    if cfg.get('tag'):
        tag=tag_card(cfg['tag'])
        # Keep callouts clear of chapter labels on narrow frames.
        x=W-tag.width-24
        y=72+round(2*math.sin(t*2))
        frame.paste(tag,(x,y),tag)
    ci=bisect.bisect_right(CAP_STARTS,t)-1
    if ci>=0 and CAPTIONS[ci]['start']<=t<CAPTIONS[ci]['end']:
        card=caption_card(CAPTIONS[ci]['text'])
        frame.paste(card,((W-card.width)//2,H-card.height-18),card)
    return frame


def stills():
    folder=WORK/'stills'; folder.mkdir(parents=True,exist_ok=True)
    times=[.8,8,31,41,61,87,107,126,148,173,195,201,215,227,237]
    for t in times:
        im=render_frame(int(t*FPS))
        im.save(folder/f'{t:05.1f}.jpg',quality=92)
    DOCS.mkdir(parents=True,exist_ok=True)
    draw_intro(1.0).resize((1280,720),Image.Resampling.LANCZOS).save(DOCS/'poster.jpg',quality=90,optimize=True)
    print(f'Wrote {len(times)} previews and the poster',flush=True)


def render():
    DOCS.mkdir(parents=True,exist_ok=True)
    output=DOCS/'sekretny-shtab.mp4'
    temporary_output=WORK/'rendering.mp4'
    ff=imageio_ffmpeg.get_ffmpeg_exe()
    cmd=[ff,'-hide_banner','-loglevel','error','-y',
         '-f','rawvideo','-vcodec','rawvideo','-pix_fmt','rgb24',
         '-s',f'{W}x{H}','-r',str(FPS),'-i','pipe:0',
         '-i',str(WORK/'mix.wav'),'-map','0:v:0','-map','1:a:0',
         '-vf','scale=1280:720:flags=lanczos,fps=24',
         '-c:v','libx264','-preset','veryfast','-crf','22','-pix_fmt','yuv420p',
         '-c:a','aac','-b:a','128k','-ar','44100',
         '-t',str(DURATION),'-threads','2','-movflags','+faststart',str(temporary_output)]
    total=round(DURATION*FPS); start=time.monotonic()
    print(f'Rendering {total} animation frames to {output.name}',flush=True)
    with (WORK/'ffmpeg.log').open('wb') as log:
        proc=subprocess.Popen(cmd,stdin=subprocess.PIPE,stderr=log)
        try:
            for n in range(total):
                proc.stdin.write(render_frame(n).tobytes())
                if n and n%(FPS*20)==0:
                    elapsed=time.monotonic()-start
                    print(f'  {n/FPS:5.0f}/{DURATION:.0f}s  ({100*n/total:4.1f}%; {n/elapsed:.1f} rendered frames/s)',flush=True)
            proc.stdin.close()
            rc=proc.wait()
        except BaseException:
            if proc.stdin and not proc.stdin.closed:
                proc.stdin.close()
            proc.terminate(); proc.wait()
            raise
    if rc:
        raise RuntimeError(f'FFmpeg failed ({rc}); see {WORK/"ffmpeg.log"}')
    # Do not expose a half-written file to viewers during a re-render.
    temporary_output.replace(output)
    print(f'Finished in {time.monotonic()-start:.1f}s; {output.stat().st_size/1024/1024:.1f} MiB',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--stills',action='store_true',help='write preview frames and poster instead of the video')
    args=parser.parse_args()
    if args.stills: stills()
    else: render()
