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
      case 'thunder': this.noise(0.8,0.25,200); this.tone(60,0.7,'sine',0.2,0,40); break;
      case 'bird': this.tone(1200+Math.random()*600,0.08,'sine',0.05,0,1800); break;
      case 'error': this.tone(160,0.15,'square',0.12); break;
    }
  },
  startMusic(){
    if(!this.ctx || this.musicTimer) return;
    // cozy chord progression (C - Am - F - G) + pentatonic melody random walk
    const chordsDay=[[262,330,392],[220,262,330],[175,220,262],[196,247,294]];
    const chordsNight=[[220,262,330],[208,247,294],[196,233,294],[185,220,262]];
    const scaleDay=[262,294,330,392,440,523,587,659];
    const scaleNight=[220,233,262,294,330,349,392];
    let melIdx=3;
    const pluck=(freq,vol,dur,type)=>{
      if(!this.ctx) return;
      const t=this.ctx.currentTime;
      const o=this.ctx.createOscillator(), g=this.ctx.createGain();
      o.type=type||'triangle'; o.frequency.value=freq;
      g.gain.setValueAtTime(vol,t); g.gain.exponentialRampToValueAtTime(0.001,t+dur);
      o.connect(g); g.connect(this.musicGain); o.start(t); o.stop(t+dur+0.05);
    };
    const play=()=>{
      if(!ZT.settings.music) return;
      const day=this.musicMode==='day';
      const chords=day?chordsDay:chordsNight;
      const scale=day?scaleDay:scaleNight;
      const bar=Math.floor(this.step/8)%4;
      const chord=chords[bar];
      // pad chord at bar start
      if(this.step%8===0){ chord.forEach(f=>pluck(f,0.10,1.6,'sine')); pluck(chord[0]/2,0.14,1.8,'sine'); }
      // melody: random walk, plays on most steps
      if(Math.random()<0.75){
        melIdx=Math.max(0,Math.min(scale.length-1, melIdx+((Math.random()*3)|0)-1));
        pluck(scale[melIdx], day?0.16:0.10, 0.45, day?'triangle':'sawtooth');
      }
      this.step++;
    };
    this.musicTimer=setInterval(play, 300);
  },
  setMusicMode(mode){ this.musicMode=mode; }
};
