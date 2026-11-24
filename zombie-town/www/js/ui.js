/* UI: HUD, build menu, panels, inspector, toasts, menus. */
'use strict';
const ZT = window.ZT;

ZT.UI = (function(){
  const $ = (id)=>document.getElementById(id);
  let game=null;
  let activeCat='housing';

  function applyI18n(){
    document.querySelectorAll('[data-i18n]').forEach(el=>{
      el.textContent = ZT.t(el.getAttribute('data-i18n'));
    });
  }
  function show(id){ $(id).classList.remove('hidden'); }
  function hide(id){ $(id).classList.add('hidden'); }
  function toggle(id){ $(id).classList.toggle('hidden'); }
  function closeAllPanels(){ ['build-menu','pop-panel','zombie-panel','inspector'].forEach(hide); }

  function toast(text, kind){
    const box=$('toasts');
    const el=document.createElement('div');
    el.className='toast '+(kind||'');
    el.textContent=text;
    box.appendChild(el);
    while(box.children.length>4) box.removeChild(box.firstChild);
    setTimeout(()=>{ el.style.opacity='0'; el.style.transition='opacity .4s'; setTimeout(()=>el.remove(),450); }, 3400);
  }

  function renderResbar(){
    const bar=$('resbar');
    const caps=game.resourceCaps();
    let html='';
    for(const r of ZT.RES_ORDER){
      const v=Math.floor(game.resources[r]||0);
      const cap=caps[r];
      const warn = cap && v>=cap*0.95;
      html+='<div class="res'+(warn?' warn':'')+'">'+ZT.RES[r].icon+' '+v+(cap?'<span class="cap">/'+cap+'</span>':'')+'</div>';
    }
    // population + happiness + safety
    const pop=game.citizens.filter(c=>!c.dead).length;
    const beds=game.totalBeds();
    const hap=Math.round(game.avgHappiness());
    const hapIcon = hap>65?'😊':hap>40?'😐':hap>20?'😟':'😭';
    html+='<div class="res">👥 '+pop+'/'+beds+'</div>';
    html+='<div class="res">'+hapIcon+' '+hap+'%</div>';
    html+='<div class="res">🛡️ '+Math.round(game.safetyScore())+'%</div>';
    bar.innerHTML=html;
  }
  function renderClock(){
    $('day-label').textContent = (ZT.settings.lang==='en'?'Day ':'День ')+game.day;
    const h=Math.floor(game.timeOfDay*24), m=Math.floor((game.timeOfDay*24-h)*60);
    const icon = game.isNight()?'🌙':(h<7||h>18?'🌇':'☀️');
    $('time-label').textContent = icon+' '+String(h).padStart(2,'0')+':'+String(m).padStart(2,'0');
    const wIcon={clear:'☀️',rain:'🌧️',fog:'🌫️',storm:'⛈️'}[game.weather]||'☀️';
    $('weather-label').textContent=wIcon;
    // alerts
    const alerts=[];
    if(game.zombies.length>3) alerts.push('🧟 '+game.zombies.length);
    if(game.outbreak.active) alerts.push('☣ Вспышка!');
    if(game.citizens.some(c=>c.infected>0)) alerts.push('🦠 Заражение!');
    $('alerts').innerHTML=alerts.map(a=>'<div class="alert">'+a+'</div>').join('');
    // zombie badge
    const badge=$('zombie-count');
    if(game.zombies.length>0){ badge.textContent=game.zombies.length; badge.classList.remove('hidden'); }
    else badge.classList.add('hidden');
    // outbreak banner
    if(game.outbreak.active){ show('outbreak-banner'); $('outbreak-text').textContent=game.outbreakPhaseText(); }
    else hide('outbreak-banner');
  }

  /* ---------- Build menu ---------- */
  function renderBuildTabs(){
    const tabs=$('build-tabs');
    tabs.innerHTML=ZT.CATS.map(c=>'<button class="btab'+(c.id===activeCat?' active':'')+'" data-cat="'+c.id+'">'+c.icon+' '+ZT.L(c.name)+'</button>').join('');
    tabs.querySelectorAll('.btab').forEach(b=>b.onclick=()=>{ activeCat=b.dataset.cat; game.audio.sfx('click'); renderBuildTabs(); renderBuildGrid(); });
  }
  function renderBuildGrid(){
    const grid=$('build-grid');
    const ids=Object.keys(ZT.BUILDINGS).filter(id=>ZT.BUILDINGS[id].cat===activeCat);
    grid.innerHTML='';
    ids.forEach(id=>{
      const def=ZT.BUILDINGS[id];
      const afford=game.canAfford(def);
      const card=document.createElement('button');
      card.className='bcard'+(afford?'':' cant');
      card.innerHTML='<div class="ic">'+def.icon+'</div><div class="nm">'+ZT.L(def.name)+'</div><div class="ds">'+ZT.L(def.desc)+'</div><div class="cost">'+
        Object.keys(def.cost).map(r=>ZT.RES[r].icon+' '+def.cost[r]).join(' ')+
        (def.capacity?' · 🛏 '+def.capacity:'')+(def.hp?' · ❤️ '+def.hp:'')+'</div>';
      card.onclick=()=>{ game.startPlacement(id); closeAllPanels(); };
      grid.appendChild(card);
    });
  }

  /* ---------- Population panel ---------- */
  function renderPop(){
    const list=$('pop-list');
    const alive=game.citizens.filter(c=>!c.dead);
    $('pop-summary').textContent='👥 '+alive.length+' · 🏠 '+game.totalBeds()+' 🛏';
    list.innerHTML='';
    alive.slice(0,80).forEach(c=>{
      const row=document.createElement('div');
      row.className='citizen-row';
      const moodIcon=c.happiness>65?'😊':c.happiness>40?'😐':'😟';
      row.innerHTML='<div class="face">'+(c.infected>0?'🧟':(c.child?'🧒':'🧑'))+'</div>'+
        '<div class="info"><div class="nm">'+c.name+' · '+c.age+' · '+ZT.JOBS[c.job].icon+' '+ZT.L(ZT.JOBS[c.job].name)+'</div>'+
        '<div class="sub">❤️ '+Math.round(c.hp)+' · 🍞 '+Math.round(c.hunger)+' · 💧 '+Math.round(c.thirst)+' · 😴 '+Math.round(c.rest)+' · '+moodIcon+' '+Math.round(c.happiness)+'%'+(c.infected>0?' · 🦠':'')+'</div>'+
        '<div class="bar hp"><i style="width:'+c.hp+'%"></i></div>'+
        '<div class="bar mood"><i style="width:'+c.happiness+'%"></i></div></div>'+
        '<button class="mini-btn" data-follow="'+c.id+'">📍</button>';
      const fb=row.querySelector('[data-follow]'); if(fb) fb.onclick=()=>{ game.followCitizen(c); };
      list.appendChild(row);
    });
    if(!alive.length) list.innerHTML='<p class="hint">Пока никого нет. Жди переселенцев!</p>';
  }

  /* ---------- Inspector ---------- */
  function showInspectorBuilding(b){
    closeAllPanels(); show('inspector');
    $('insp-title').textContent=b.def.icon+' '+ZT.L(b.def.name);
    const body=$('insp-body');
    let html='';
    html+='<div class="row"><span>❤️ Прочность</span><b>'+Math.round(b.hp)+' / '+b.maxHp+'</b></div>';
    if(!b.constructed) html+='<div class="row"><span>🏗 Строительство</span><b>'+Math.round(b.progress/b.def.buildTime*100)+'%</b></div>';
    if(b.isHousing()) html+='<div class="row"><span>🛏 Жильё</span><b>'+b.residents.filter(c=>!c.dead).length+' / '+b.def.capacity+'</b></div>';
    if(b.def.workers) html+='<div class="row"><span>👷 Рабочие</span><b>'+b.workers.filter(c=>!c.dead).length+' / '+b.def.workers+'</b></div>';
    if(b.def.produces) html+='<div class="row"><span>⚙ Производство</span><b>'+Object.keys(b.def.produces).map(r=>ZT.RES[r].icon+' +'+b.def.produces[r]).join(' ')+'</b></div>';
    if(b.ruin) html+='<div class="row"><span>💥 Состояние</span><b>Руины</b></div>';
    if(b.burning>0) html+='<div class="row"><span>🔥 Горит!</span><b>'+Math.round(b.burning)+'с</b></div>';
    html+='<div class="ibtns">';
    if(b.ruin) html+='<button class="good" id="insp-rebuild">🏗 Перестроить</button>';
    else if(b.hp<b.maxHp) html+='<button class="good" id="insp-repair">🔧 Ремонт ('+repairCostText(b)+')</button>';
    if(b.isHousing() && b.def.upgradesTo && b.constructed && !b.ruin) html+='<button class="good" id="insp-upgrade">⬆ Улучшить до '+ZT.L(ZT.BUILDINGS[b.def.upgradesTo].name)+'</button>';
    if(!b.ruin && b.id!=='center') html+='<button class="danger" id="insp-demolish">🧨 Снести</button>';
    html+='</div>';
    body.innerHTML=html;
    const rb=$('insp-rebuild'); if(rb) rb.onclick=()=>{ game.rebuildBuilding(b); hide('inspector'); };
    const rp=$('insp-repair'); if(rp) rp.onclick=()=>{ game.repairBuilding(b); showInspectorBuilding(b); };
    const up=$('insp-upgrade'); if(up) up.onclick=()=>{ game.upgradeBuilding(b); hide('inspector'); };
    const dm=$('insp-demolish'); if(dm) dm.onclick=()=>{ game.demolishBuilding(b); hide('inspector'); };
  }
  function repairCostText(b){ const c=b.repairCost(); return Object.keys(c).map(r=>ZT.RES[r].icon+' '+c[r]).join(' '); }
  function showInspectorCitizen(c){
    closeAllPanels(); show('inspector');
    $('insp-title').textContent=(c.infected>0?'🧟 ':'🧑 ')+c.name;
    const body=$('insp-body');
    let html='';
    html+='<div class="row"><span>🎂 Возраст</span><b>'+c.age+'</b></div>';
    html+='<div class="row"><span>❤️ Здоровье</span><b>'+Math.round(c.hp)+'</b></div>';
    html+='<div class="row"><span>🍞 Еда</span><b>'+Math.round(c.hunger)+'</b></div>';
    html+='<div class="row"><span>💧 Вода</span><b>'+Math.round(c.thirst)+'</b></div>';
    html+='<div class="row"><span>😴 Отдых</span><b>'+Math.round(c.rest)+'</b></div>';
    html+='<div class="row"><span>😊 Счастье</span><b>'+Math.round(c.happiness)+'%</b></div>';
    html+='<div class="row"><span>🏠 Дом</span><b>'+(c.home?ZT.L(c.home.def.name):'—')+'</b></div>';
    if(c.infected>0) html+='<div class="row"><span>🦠 Заражение</span><b>'+Math.round(c.infectedT)+'с до превращения!</b></div>';
    html+='<div class="row"><span>💼 Работа</span><select id="insp-job">'+
      Object.keys(ZT.JOBS).map(j=>'<option value="'+j+'"'+(j===c.job?' selected':'')+'>'+ZT.JOBS[j].icon+' '+ZT.L(ZT.JOBS[j].name)+'</option>').join('')+'</select></div>';
    html+='<div class="ibtns">';
    html+='<button id="insp-follow">📍 '+ (c.follow?'Не следить':'Следить') +'</button>';
    if(c.infected>0) html+='<button class="good" id="insp-treat">💊 Лечить (1 💊)</button>';
    html+='</div>';
    body.innerHTML=html;
    $('insp-job').onchange=(e)=>{ game.assignJob(c, e.target.value); };
    $('insp-follow').onclick=()=>{ c.follow=!c.follow; showInspectorCitizen(c); };
    const tr=$('insp-treat'); if(tr) tr.onclick=()=>{ game.treatCitizen(c); showInspectorCitizen(c); };
  }

  /* ---------- Zombie panel ---------- */
  function renderZombieStats(){
    $('z-stats').innerHTML='🧟 Активно: <b>'+game.zombies.length+'</b> · ☠️ Убито: <b>'+game.stats.zombiesKilled+'</b> · 🏠 Разрушено зданий: <b>'+game.stats.buildingsLost+'</b> · Фаза: <b>'+game.outbreakPhaseText()+'</b>';
  }

  /* ---------- Tutorial ---------- */
  function showTutorial(text){ const t=$('tutorial'); t.textContent=text; t.classList.remove('hidden'); }
  function hideTutorial(){ $('tutorial').classList.add('hidden'); }

  /* ---------- Howto ---------- */
  function renderHowto(){
    const ru = ZT.settings.lang==='ru';
    $('howto-text').innerHTML = ru ? `
      <h3>🏘 Город живёт сам</h3>
      <ul><li>При старте мира люди сами заселяются: приходят с края карты, строят домики, а потом многоэтажки и небоскрёбы.</li>
      <li>Город растёт, если есть жильё, еда, вода и счастье. Строители сами возводят дома!</li></ul>
      <h3>🧟 Зомби — когда захочешь</h3>
      <ul><li>Кнопка 🧟 внизу — спавн 1 зомби, 5, орды или полная вспышка в любой момент.</li>
      <li>Зомби идут к людям и зданиям, ломают стены, заражают жителей.</li></ul>
      <h3>🛡 Защита</h3>
      <ul><li>Строй стены, ворота, башни и турели. Охранники патрулируют и стреляют.</li>
      <li>Кнопка 🚨 — тревога: все бегут в убежище.</li></ul>
      <h3>🎮 Управление</h3>
      <ul><li>Перетаскивание — панорама, щипок — масштаб, тап — выбрать, долгое нажатие — инфо.</li>
      <li>Стройка: выбери здание → поставь на зелёную клетку.</li></ul>
      <h3>🔧 После атаки</h3>
      <ul><li>Тапни по зданию → ремонт / перестройка. Лечи заражённых в клинике.</li></ul>`
    : `
      <h3>🏘 The town lives by itself</h3>
      <ul><li>At world start people settle on their own: they walk in from the map edge, build cottages, then apartments and skyscrapers.</li>
      <li>The town grows if there is housing, food, water and happiness. Builders construct houses themselves!</li></ul>
      <h3>🧟 Zombies — whenever you want</h3>
      <ul><li>The 🧟 button at the bottom — spawn 1, 5, a horde or a full outbreak anytime.</li>
      <li>Zombies march to citizens and buildings, break walls, infect people.</li></ul>
      <h3>🛡 Defense</h3>
      <ul><li>Build walls, gates, towers and turrets. Guards patrol and shoot.</li>
      <li>The 🚨 button — alarm: everyone runs to the safehouse.</li></ul>
      <h3>🎮 Controls</h3>
      <ul><li>Drag — pan, pinch — zoom, tap — select, long-press — info.</li>
      <li>Build: pick a building → place it on a green tile.</li></ul>
      <h3>🔧 After an attack</h3>
      <ul><li>Tap a building → repair / rebuild. Heal the infected at the clinic.</li></ul>`;
  }

  /* ---------- Save slots ---------- */
  function renderSlots(){
    const box=$('save-slots');
    box.innerHTML='';
    for(let s=0;s<3;s++){
      const info=ZT.Save.slotInfo(s);
      const div=document.createElement('div');
      div.className='slot';
      div.innerHTML='<span>Слот '+(s+1)+(info?'<br><small>'+info.date+' · День '+info.day+' · 👥 '+info.pop+'</small>':'<br><small>пусто</small>')+'</span>';
      const btn=document.createElement('button');
      btn.textContent='💾';
      btn.onclick=()=>{ if(game.saveTo(s)){ toast('💾 Сохранено в слот '+(s+1),'good'); renderSlots(); } };
      div.appendChild(btn);
      box.appendChild(div);
    }
  }

  function bind(gameRef){
    game=gameRef;
    applyI18n();
    renderHowto();
    // action bar
    $('btn-build').onclick=()=>{ game.audio.sfx('click'); closeAllPanels(); renderBuildTabs(); renderBuildGrid(); show('build-menu'); };
    $('btn-pop').onclick=()=>{ game.audio.sfx('click'); closeAllPanels(); renderPop(); show('pop-panel'); };
    $('btn-zombie').onclick=()=>{ game.audio.sfx('click'); closeAllPanels(); renderZombieStats(); show('zombie-panel'); };
    $('btn-alarm').onclick=()=>{ game.triggerAlarm(); };
    $('btn-menu').onclick=()=>{ game.audio.sfx('click'); game.setPaused(true); renderSlots(); show('pause-menu'); };
    // placement
    $('btn-place-ok').onclick=()=>game.confirmPlacement();
    $('btn-place-cancel').onclick=()=>game.cancelPlacement();
    // zombie panel
    $('z-spawn-1').onclick=()=>game.spawnZombies(1);
    $('z-spawn-5').onclick=()=>game.spawnZombies(5);
    $('z-spawn-20').onclick=()=>game.spawnZombies(20);
    $('z-outbreak').onclick=()=>game.startOutbreak();
    $('z-stop').onclick=()=>game.stopOutbreak();
    $('z-intensity').oninput=(e)=>{ $('z-intensity-val').textContent=e.target.value; };
    // speed
    document.querySelectorAll('.sbtn.spd').forEach(b=>{
      b.onclick=()=>{ game.setSpeed(parseInt(b.dataset.speed)); document.querySelectorAll('.sbtn.spd').forEach(x=>x.classList.toggle('active',x===b)); };
    });
    $('btn-pause').onclick=()=>game.setPaused(!game.paused);
    // menus
    $('btn-new').onclick=()=>{ try{ game.audio.resume(); game.newGame(); }catch(e){ console.error(e); if(window.__ztError) window.__ztError('Ошибка старта: '+(e&&e.message?e.message:e)); } };
    $('btn-continue').onclick=()=>{ try{ game.audio.resume(); if(!game.loadFrom(0)){ toast('Нет сохранения','bad'); } }catch(e){ console.error(e); if(window.__ztError) window.__ztError('Ошибка загрузки: '+(e&&e.message?e.message:e)); } };
    $('btn-howto').onclick=()=>{ renderHowto(); show('howto'); };
    $('btn-settings').onclick=()=>{ syncSettingsUI(); show('settings'); };
    $('btn-resume').onclick=()=>{ hide('pause-menu'); game.setPaused(false); };
    $('btn-save').onclick=()=>{ if(game.saveTo(0)) toast('💾 Сохранено!','good'); };
    $('btn-load').onclick=()=>{ hide('pause-menu'); game.loadFrom(0); };
    $('btn-quit').onclick=()=>{ game.toMenu(); };
    document.querySelectorAll('[data-close]').forEach(b=>{
      b.onclick=()=>{ hide(b.dataset.close); game.audio.sfx('click'); };
    });
    // settings
    $('set-sound').onchange=(e)=>{ ZT.settings.sound=e.target.checked; };
    $('set-music').onchange=(e)=>{ ZT.settings.music=e.target.checked; };
    $('set-vibrate').onchange=(e)=>{ ZT.settings.vibrate=e.target.checked; };
    $('set-particles').onchange=(e)=>{ ZT.settings.particles=e.target.checked; };
    $('set-quality').onchange=(e)=>{ ZT.settings.quality=e.target.value; };
    $('set-lang').onchange=(e)=>{ ZT.settings.lang=e.target.value; applyI18n(); renderHowto(); };
    $('set-difficulty').onchange=(e)=>{ ZT.settings.difficulty=e.target.value; };
    // auto-refresh pop panel
    setInterval(()=>{ if(!$('pop-panel').classList.contains('hidden')) renderPop(); if(!$('zombie-panel').classList.contains('hidden')) renderZombieStats(); }, 1500);
  }
  function syncSettingsUI(){
    $('set-sound').checked=ZT.settings.sound;
    $('set-music').checked=ZT.settings.music;
    $('set-vibrate').checked=ZT.settings.vibrate;
    $('set-particles').checked=ZT.settings.particles;
    $('set-quality').value=ZT.settings.quality;
    $('set-lang').value=ZT.settings.lang;
    $('set-difficulty').value=ZT.settings.difficulty;
  }
  function enterGame(){
    hide('main-menu'); hide('pause-menu');
    show('hud'); show('actionbar');
  }
  function toMenu(){
    hide('hud'); hide('actionbar'); hide('pause-menu'); closeAllPanels();
    show('main-menu');
  }

  return { bind, applyI18n, show, hide, closeAllPanels, toast, renderResbar, renderClock,
           renderBuildTabs, renderBuildGrid, renderPop, renderZombieStats,
           showInspectorBuilding, showInspectorCitizen, showTutorial, hideTutorial,
           renderHowto, renderSlots, syncSettingsUI, enterGame, toMenu };
})();
