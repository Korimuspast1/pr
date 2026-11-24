/* Procedural audio via WebAudio — no audio files needed. */
'use strict';
var ZT = window.ZT;

ZT.Audio = function(){
  this.ctx=null; this.master=null; this.musicGain=null;
  this.musicTimer=null; this.musicMode='day'; this.step=0;
  this.init();
};
ZT.Audio.prototype = {
  init(){
    try{
      const AC = window.AudioContext || window.webkitAudioContext;
      if(!AC) return;
      this.ctx=new AC();
      this.master=this.ctx.createGain(); this.master.gain.value=0.5;
      this.master.connect(this.ctx.destination);
      this.musicGain=this.ctx.createGain(); this.musicGain.gain.value=0.16;
      this.musicGain.connect(this.master);
    }catch(e){ this.ctx=null; }
  },
  resume(){ if(this.ctx && this.ctx.state==='suspended') this.ctx.resume(); },
  tone(freq, dur, type, vol, when, slideTo){
    if(!this.ctx || !ZT.settings.sound) return;
    const t=this.ctx.currentTime+(when||0);
    const o=this.ctx.createOscillator(), g=this.ctx.createGain();
    o.type=type||'sine'; o.frequency.setValueAtTime(freq,t);
    if(slideTo) o.frequency.exponentialRampToValueAtTime(Math.max(20,slideTo),t+dur);
    g.gain.setValueAtTime(vol||0.2,t);
    g.gain.exponentialRampToValueAtTime(0.001,t+dur);
    o.connect(g); g.connect(this.master);
    o.start(t); o.stop(t+dur+0.02);
  },
  noise(dur, vol, freq, when){
    if(!this.ctx || !ZT.settings.sound) return;
    const t=this.ctx.currentTime+(when||0);
    const len=Math.floor(this.ctx.sampleRate*dur);
    const buf=this.ctx.createBuffer(1,len,this.ctx.sampleRate);
    const d=buf.getChannelData(0);
    for(let i=0;i<len;i++) d[i]=(Math.random()*2-1)*(1-i/len);
    const src=this.ctx.createBufferSource(); src.buffer=buf;
    const f=this.ctx.createBiquadFilter(); f.type='lowpass'; f.frequency.value=freq||800;
    const g=this.ctx.createGain(); g.gain.value=vol||0.15;
    src.connect(f); f.connect(g); g.connect(this.master);
    src.start(t);
  },
  sfx(name){
    if(!this.ctx || !ZT.settings.sound) return;
    switch(name){
      case 'click': this.tone(700,0.06,'square',0.08); break;
      case 'place': this.tone(300,0.12,'square',0.15,0,180); this.noise(0.08,0.1,500); break;
      case 'hammer': this.noise(0.05,0.12,2000); this.tone(180,0.05,'square',0.08); break;
      case 'chop': this.noise(0.07,0.15,1200); break;
      case 'eat': this.tone(400,0.08,'sine',0.1,0,300); break;
      case 'drink': this.tone(500,0.15,'sine',0.1,0,700); break;
      case 'arrive': this.tone(523,0.12,'sine',0.15); this.tone(659,0.12,'sine',0.15,0.1); this.tone(784,0.2,'sine',0.15,0.2); break;
      case 'groan': this.tone(90+Math.random()*40,0.5,'sawtooth',0.07,0,60); break;
      case 'bite': this.noise(0.08,0.12,600); break;
      case 'hit': this.noise(0.05,0.1,900); break;
      case 'hurt': this.tone(220,0.15,'sawtooth',0.12,0,140); break;
      case 'smash': this.noise(0.15,0.2,400); this.tone(120,0.2,'square',0.12,0,60); break;
      case 'shoot': this.noise(0.06,0.14,3000); break;
      case 'alarm': for(let i=0;i<3;i++){ this.tone(660,0.25,'square',0.12,i*0.3,440); } break;
      case 'alarm-short': this.tone(660,0.15,'square',0.08,0,440); break;
      case 'scream': this.tone(900,0.4,'sawtooth',0.1,0,1400); break;
      case 'infect': this.tone(200,0.4,'sawtooth',0.12,0,80); break;
      case 'heal': this.tone(523,0.15,'sine',0.12); this.tone(784,0.2,'sine',0.12,0.1); break;
      case 'coin': this.tone(988,0.08,'square',0.1); this.tone(1319,0.12,'square',0.1,0.07); break;
      case 'build-done': this.tone(392,0.1,'square',0.12); this.tone(523,0.1,'square',0.12,0.09); this.tone(659,0.18,'square',0.12,0.18); break;
      case 'death': this.tone(330,0.5,'sine',0.15,0,110); break;
      case 'upgrade': this.tone(523,0.1,'square',0.12); this.tone(659,0.1,'square',0.12,0.09); this.tone(784,0.1,'square',0.12,0.18); this.tone(1047,0.2,'square',0.12,0.27); break;
      case 'rain': this.noise(0.4,0.05,1500); break;
      case 'bird': this.tone(1200+Math.random()*600,0.08,'sine',0.05,0,1800); break;
      case 'error': this.tone(160,0.15,'square',0.12); break;
    }
  },
  startMusic(){
    if(!this.ctx || this.musicTimer) return;
    const dayNotes=[262,294,330,392,440,392,330,294]; // C major pentatonic-ish cozy
    const nightNotes=[220,208,196,185,196,208,220,233]; // tense minor
    const play=()=>{
      if(!ZT.settings.music) return;
      const notes = this.musicMode==='day' ? dayNotes : nightNotes;
      const n=notes[this.step%notes.length];
      // soft pluck
      if(this.ctx){
        const t=this.ctx.currentTime;
        const o=this.ctx.createOscillator(), g=this.ctx.createGain();
        o.type=this.musicMode==='day'?'triangle':'sawtooth';
        o.frequency.value=n;
        g.gain.setValueAtTime(0.5,t); g.gain.exponentialRampToValueAtTime(0.001,t+0.5);
        o.connect(g); g.connect(this.musicGain); o.start(t); o.stop(t+0.55);
        // bass every 4 steps
        if(this.step%4===0){
          const o2=this.ctx.createOscillator(), g2=this.ctx.createGain();
          o2.type='sine'; o2.frequency.value=n/2;
          g2.gain.setValueAtTime(0.4,t); g2.gain.exponentialRampToValueAtTime(0.001,t+0.8);
          o2.connect(g2); g2.connect(this.musicGain); o2.start(t); o2.stop(t+0.85);
        }
      }
      this.step++;
    };
    this.musicTimer=setInterval(play, 420);
  },
  setMusicMode(mode){ this.musicMode=mode; }
};
