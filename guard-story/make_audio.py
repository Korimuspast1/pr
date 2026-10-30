"""Build the four-minute narration, original ambient score and caption timing."""
from pathlib import Path
import json
import re
import subprocess
import wave

import imageio_ffmpeg
import numpy as np

ROOT=Path(__file__).resolve().parent
WORK=ROOT/'.work'
OUT=ROOT.parent/'docs'/'guard-story'
WORK.mkdir(exist_ok=True)
OUT.mkdir(parents=True,exist_ok=True)
FF=imageio_ffmpeg.get_ffmpeg_exe()
SR=44100
DURATION=240.0
INTRO=3.0
GAP=.45
SPEECH_TARGET=228.0


def decode(path, tempo=1.0):
    command=[FF,'-hide_banner','-loglevel','error','-i',str(path)]
    if tempo!=1:
        command += ['-af',f'atempo={tempo:.8f}']
    command += ['-f','s16le','-acodec','pcm_s16le','-ac','1','-ar',str(SR),'-']
    return np.frombuffer(subprocess.run(command,check=True,stdout=subprocess.PIPE).stdout,
                         dtype='<i2').astype(np.float32)/32768


def phrases(text):
    sentences=re.split(r'(?<=[.!?])\s+',text.strip())
    out=[]
    for sentence in sentences:
        chunks=re.split(r'(?<=,)\s+',sentence) if len(sentence)>95 else [sentence]
        current=''
        for c in chunks:
            combined=(current+' '+c).strip()
            if current and len(combined)>90:
                out.append(current); current=c
            else:
                current=combined
        if current: out.append(current)
    return out


def timecode(t):
    ms=max(0,round(t*1000));h,ms=divmod(ms,3600000);m,ms=divmod(ms,60000);s,ms=divmod(ms,1000)
    return f'{h:02}:{m:02}:{s:02},{ms:03}'


def tone(buf, at, freq, length, volume, kind='note'):
    start=round(at*SR); n=min(round(length*SR),len(buf)-start)
    if n<=0 or start<0: return
    t=np.arange(n,dtype=np.float32)/SR
    if kind=='pad':
        envelope=np.minimum(1,t/2.0)*np.minimum(1,(length-t)/2.2)
        wave=np.sin(2*np.pi*freq*t)+.18*np.sin(2*np.pi*freq*2*t)
    elif kind=='bell':
        envelope=np.minimum(1,t*80)*np.exp(-t*5)
        wave=np.sin(2*np.pi*freq*t)+.2*np.sin(2*np.pi*freq*2.01*t)
    elif kind=='knock':
        envelope=np.minimum(1,t*160)*np.exp(-t*22)
        wave=np.sin(2*np.pi*(freq*t-35*t*t))+.3*np.sin(2*np.pi*freq*2*t)
    else:
        envelope=np.minimum(1,t*45)*np.exp(-t*3.0)
        wave=np.sin(2*np.pi*freq*t)+.22*np.sin(2*np.pi*freq*2*t)
    buf[start:start+n]+=(volume*envelope*wave).astype(np.float32)


def score(scenes,captions):
    n=round(DURATION*SR)
    out=np.zeros(n,dtype=np.float32)
    # A restrained, entirely synthesized D-minor night-shift score.
    chords=[(146.83,174.61,220.00),(130.81,174.61,220.00),
            (130.81,164.81,196.00),(123.47,164.81,196.00)]
    for bar in range(21):
        start=bar*12.0; chord=chords[bar%len(chords)]
        for freq in chord: tone(out,start,freq,13.0,.005,'pad')
        for pulse in range(8):
            when=start+pulse*1.5
            tone(out,when,chord[pulse%3]/2,.75,.013)
            if pulse in (2,6): tone(out,when+.75,chord[(pulse//2)%3]*2,.55,.009,'bell')
    # Subtle non-musical cues reinforce the narrated action.
    def cue_start(fragment):
        return next((c['start'] for c in captions if fragment in c['text']),None)
    at=cue_start('датчик в коридоре')
    if at is not None: tone(out,at+2.3,920,.16,.032,'bell')
    at=cue_start('Один раз.')
    if at is not None:
        tone(out,at+.15,100,.20,.064,'knock')
        tone(out,at+1.12,90,.20,.054,'knock')
    at=cue_start('связался со старшим')
    if at is not None:
        tone(out,at+.4,640,.14,.018,'bell')
    at=cue_start('собственные шаги')
    if at is not None:
        for i in range(4): tone(out,at+1.1+i*.72,64,.18,.024,'knock')
    fade=np.minimum(1,np.arange(2*SR,dtype=np.float32)/(2*SR));out[:len(fade)]*=fade
    fade=np.minimum(1,np.arange(4*SR,dtype=np.float32)[::-1]/(4*SR));out[-len(fade):]*=fade
    return out


def main():
    story=json.loads((ROOT/'script.json').read_text(encoding='utf-8'))
    files=[ROOT/'audio'/f'{i:02}.mp3' for i in range(1,len(story)+1)]
    total=sum(len(decode(p))/SR for p in files)
    tempo=total/SPEECH_TARGET
    narration=np.zeros(round(DURATION*SR),np.float32)
    scenes=[];captions=[];cursor=INTRO
    print(f'Voice total {total:.2f}s; tempo {tempo:.4f}x',flush=True)
    for i,(item,path) in enumerate(zip(story,files),1):
        samples=decode(path,tempo);dur=len(samples)/SR
        a=round(cursor*SR);b=min(len(narration),a+len(samples));narration[a:b]+=samples[:b-a]
        scenes.append({'index':i,'title':item['title'],'start':round(cursor,4),
                       'duration':round(dur,4),'end':round(cursor+dur,4)})
        parts=phrases(item['text'])
        weights=[len(p)+6*p.count(',')+8*sum(p.count(x) for x in '.!?') for p in parts]
        t=cursor
        for text,weight in zip(parts,weights):
            end=t+dur*weight/sum(weights)
            captions.append({'scene':i,'start':round(t,4),'end':round(end,4),'text':text})
            t=end
        print(f"{i:02} {cursor:6.2f}–{cursor+dur:6.2f}",flush=True)
        cursor+=dur+GAP
    if scenes[-1]['end']>DURATION-3: raise RuntimeError('Narration does not fit')
    data={'duration':DURATION,'intro':INTRO,'tempo':tempo,'scenes':scenes,'captions':captions}
    (WORK/'timing.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8')
    srt=[]
    for i,c in enumerate(captions,1):
        srt.append(f"{i}\n{timecode(c['start'])} --> {timecode(c['end'])}\n{c['text']}\n")
    (OUT/'subtitles.srt').write_text('\n'.join(srt),encoding='utf8')
    music=score(scenes,captions)
    mixed=narration*.90+music
    peak=float(np.max(np.abs(mixed)))
    if peak>.97:mixed*=.97/peak
    pcm=(np.clip(mixed,-1,1)*32767).astype('<i2')
    with wave.open(str(WORK/'mix.wav'),'wb') as f:
        f.setnchannels(1);f.setsampwidth(2);f.setframerate(SR);f.writeframes(pcm.tobytes())
    print(f"Narration ends {scenes[-1]['end']:.2f}s; peak {peak:.3f}; {len(captions)} captions",flush=True)


if __name__=='__main__': main()
