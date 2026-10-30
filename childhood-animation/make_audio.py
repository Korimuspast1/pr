"""Assemble the selected synthetic narration and an original musical bed.

Requires Pillow/NumPy/imageio-ffmpeg (see requirements.txt). Speech is kept in
separate numbered files so the story can be edited or re-timed without touching
video frames. This script writes only disposable intermediate files to .work/.
"""
from pathlib import Path
import json
import math
import re
import subprocess
import wave

import imageio_ffmpeg
import numpy as np

ROOT = Path(__file__).resolve().parent
WORK = ROOT / '.work'
DOCS = ROOT.parent / 'docs' / 'childhood-animation'
WORK.mkdir(exist_ok=True)
DOCS.mkdir(parents=True, exist_ok=True)
SAMPLE_RATE = 44100
DURATION = 240.0
FPS = 12
INTRO = 3.4
GAP = 0.65
TARGET_SPEECH_SECONDS = 225.0
FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()


def decode(path, tempo=1.0):
    cmd = [FFMPEG, '-hide_banner', '-loglevel', 'error', '-i', str(path)]
    if tempo != 1.0:
        cmd += ['-af', f'atempo={tempo:.8f}']
    cmd += ['-f', 's16le', '-acodec', 'pcm_s16le', '-ac', '1', '-ar', str(SAMPLE_RATE), '-']
    raw = subprocess.run(cmd, check=True, stdout=subprocess.PIPE).stdout
    return np.frombuffer(raw, dtype='<i2').astype(np.float32) / 32768.0


def split_captions(text):
    # Break at sentence boundaries, then long sentences at a convenient comma.
    sentences = re.split(r'(?<=[.!?])\s+', text.strip())
    parts = []
    for s in sentences:
        chunks = re.split(r'(?<=,)\s+', s) if len(s) > 95 else [s]
        acc = ''
        for c in chunks:
            candidate = (acc + ' ' + c).strip()
            if acc and len(candidate) > 90:
                parts.append(acc)
                acc = c
            else:
                acc = candidate
        if acc:
            parts.append(acc)
    return parts


def srt_time(t):
    ms = max(0, round(t*1000))
    h, ms = divmod(ms, 3_600_000)
    m, ms = divmod(ms, 60_000)
    s, ms = divmod(ms, 1000)
    return f'{h:02}:{m:02}:{s:02},{ms:03}'


def note(out, start, freq, length, volume, kind='pluck'):
    start_i = int(start*SAMPLE_RATE)
    n = min(int(length*SAMPLE_RATE), len(out)-start_i)
    if n <= 0 or start_i < 0:
        return
    t = np.arange(n, dtype=np.float32) / SAMPLE_RATE
    if kind == 'pluck':
        env = np.minimum(1, t*70) * np.exp(-t*5.3)
        tone = (np.sin(2*np.pi*freq*t) + .27*np.sin(2*np.pi*freq*2*t)
                + .10*np.sin(2*np.pi*freq*3*t))
    elif kind == 'bass':
        env = np.minimum(1, t*35) * np.exp(-t*3.8)
        tone = np.sin(2*np.pi*freq*t) + .2*np.sin(2*np.pi*freq*2*t)
    elif kind == 'bell':
        env = np.minimum(1, t*100) * np.exp(-t*3.5)
        tone = np.sin(2*np.pi*freq*t) + .32*np.sin(2*np.pi*freq*2.01*t)
    else:
        env = np.minimum(1, t*70) * np.exp(-t*12)
        tone = np.sin(2*np.pi*(freq*t - freq*.33*t*t))
    out[start_i:start_i+n] += (volume * env * tone).astype(np.float32)


def make_music(schedule):
    """A lightly plucked, entirely synthesized 100 BPM instrumental score."""
    n=int(DURATION*SAMPLE_RATE)
    out=np.zeros(n,dtype=np.float32)
    rng=np.random.default_rng(2927)
    beat=0.6
    # C, Am, F, G. A tiny variation every four bars keeps the bed moving.
    chords=[(261.626,329.628,391.995,523.251),
            (220.0,261.626,329.628,440.0),
            (174.614,220.0,261.626,349.228),
            (195.998,246.942,293.665,391.995)]
    bass=[130.813,110.0,87.307,97.999]
    pattern=[0,2,1,3,0,2,1,2]
    for k in range(int(DURATION/(beat/2))+1):
        when=k*(beat/2)
        chord_index=(k//8)%4
        chord=chords[chord_index]
        v=.030 if k%2==0 else .019
        if (k//32)%2 and k%8==7:
            pitch=chord[3]
        else:
            pitch=chord[pattern[k%8]]
        note(out,when,pitch,.75,v)
        if k%4==0:
            note(out,when,bass[chord_index],.7,.025,'bass')
        if k%4==2:
            note(out,when,165,.12,.009,'click')
        if k%2==1:
            start=int(when*SAMPLE_RATE)
            nn=min(int(.055*SAMPLE_RATE),n-start)
            if nn>0:
                env=np.exp(-np.arange(nn,dtype=np.float32)/(SAMPLE_RATE*.013))
                out[start:start+nn]+=rng.normal(0,.003,nn).astype(np.float32)*env
    # Small musical punctuation, timed to shots rather than an unrelated loop.
    note(out,3.1,784,.4,.036,'bell')
    for s in schedule[1:]:
        note(out,s['start']-.16,659.26,.35,.018,'bell')
    for scene,part in [(2,.28),(4,.35),(7,.5),(8,.53),(9,.05),(9,.65),(10,.72)]:
        s=schedule[scene-1]
        at=s['start']+part*s['duration']
        if scene==9 and part==.05:
            for off,f in [(0,1047),(.1,831),(.2,1175)]:
                note(out,at+off,f,.25,.028,'bell')
        else:
            note(out,at,523.25,.25,.022,'bell')
    fade_in=np.minimum(1,np.arange(int(1.2*SAMPLE_RATE),dtype=np.float32)/(1.2*SAMPLE_RATE))
    out[:len(fade_in)]*=fade_in
    fade_out=np.minimum(1,np.arange(int(3*SAMPLE_RATE),dtype=np.float32)[::-1]/(3*SAMPLE_RATE))
    out[-len(fade_out):]*=fade_out
    return out


def main():
    story=json.loads((ROOT/'script.json').read_text(encoding='utf-8'))
    files=[ROOT/'audio'/f'{i:02}.mp3' for i in range(1,len(story)+1)]
    lengths=[len(decode(p))/SAMPLE_RATE for p in files]
    tempo=sum(lengths)/TARGET_SPEECH_SECONDS
    print(f'Original voice: {sum(lengths):.2f}s; tempo: {tempo:.4f}x',flush=True)
    narration=np.zeros(int(DURATION*SAMPLE_RATE),dtype=np.float32)
    schedule=[]
    captions=[]
    cursor=INTRO
    for i,(item,path) in enumerate(zip(story,files),1):
        voice=decode(path,tempo)
        dur=len(voice)/SAMPLE_RATE
        a=round(cursor*SAMPLE_RATE); b=min(a+len(voice),len(narration))
        narration[a:b] += voice[:b-a]
        schedule.append({'index':i,'title':item['title'],'start':round(cursor,4),
                         'duration':round(dur,4),'end':round(cursor+dur,4)})
        phrases=split_captions(item['text'])
        # Characters plus punctuation approximate each phrase's spoken duration.
        weights=[len(s)+6*s.count(',')+9*sum(s.count(c) for c in '.!?') for s in phrases]
        total=sum(weights)
        pos=cursor
        for phrase,weight in zip(phrases,weights):
            end=pos+dur*weight/total
            captions.append({'start':round(pos,4),'end':round(end,4),'text':phrase})
            pos=end
        print(f'  {i:02}: {cursor:6.2f}–{cursor+dur:6.2f} ({dur:4.1f}s)',flush=True)
        cursor+=dur+GAP
    speech_end=schedule[-1]['end']
    if speech_end> DURATION-3:
        raise RuntimeError(f'Narration ended too late ({speech_end:.2f}s)')
    print(f'Voice ends at {speech_end:.2f}s; closing card {DURATION-speech_end:.2f}s',flush=True)
    (WORK/'timing.json').write_text(json.dumps({'duration':DURATION,'fps':FPS,'intro':INTRO,
        'tempo':tempo,'scenes':schedule,'captions':captions},ensure_ascii=False,indent=2),encoding='utf-8')
    # RMS on each animation frame provides gently synchronized mouth shapes.
    envelope=[]
    step=SAMPLE_RATE//FPS
    for i in range(int(DURATION*FPS)):
        segment=narration[i*step:(i+1)*step]
        envelope.append(float(np.sqrt(np.mean(segment*segment))) if len(segment) else 0.0)
    np.save(WORK/'voice_envelope.npy',np.array(envelope,dtype=np.float32))
    music=make_music(schedule)
    # Keep the original speech comfortably above the score without hard clipping.
    mix=narration*.88+music
    peak=float(np.max(np.abs(mix)))
    if peak>.97:
        mix*=.97/peak
    pcm=(np.clip(mix,-1,1)*32767).astype('<i2')
    with wave.open(str(WORK/'mix.wav'),'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SAMPLE_RATE)
        w.writeframes(pcm.tobytes())
    srt=[]
    for i,c in enumerate(captions,1):
        srt.append(f"{i}\n{srt_time(c['start'])} --> {srt_time(c['end'])}\n{c['text']}\n")
    (DOCS/'subtitles.srt').write_text('\n'.join(srt),encoding='utf-8')
    print(f"Wrote {WORK/'mix.wav'}; peak {peak:.3f}; {len(captions)} caption cues",flush=True)


if __name__=='__main__':
    main()
