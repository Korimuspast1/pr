'use strict';
/* =====================================================================
   DEAD INSTINCT — процедурная пиксель-графика: персонажи, зомби, портреты
   Всё рисуется кодом в offscreen-канвасы: никаких чужих ассетов.
   ===================================================================== */
const SPR = { cache: new Map(), ready: false };

function makeCanvas(w, h) {
  const c = document.createElement('canvas');
  c.width = w; c.height = h;
  const g = c.getContext('2d');
  g.imageSmoothingEnabled = false;
  return { c, g };
}

/* ---------- палитра тел ---------- */
const PAL = {
  player: { skin: '#c79a72', skinDark: '#a87a56', shirt: '#3f4a3c', shirt2: '#333c31', pants: '#3a3f46', pants2: '#2f333a', hair: '#2a2119', boot: '#24211d', blood: '#5a1414' },
  mara: { skin: '#cfa382', skinDark: '#ab7f5f', shirt: '#7a5a4a', shirt2: '#6a4a3c', pants: '#4a4038', pants2: '#3c332c', hair: '#6b3a22', boot: '#2a231e' },
  yan: { skin: '#bd9068', skinDark: '#9a704c', shirt: '#4a5a40', shirt2: '#3c4a34', pants: '#33383d', pants2: '#2a2e33', hair: '#1f1b18', boot: '#211e1a' },
  vera: { skin: '#d2a682', skinDark: '#ae8262', shirt: '#5a5a6a', shirt2: '#4a4a58', pants: '#3e4148', pants2: '#33363c', hair: '#8c6a3a', boot: '#26241f' },
  dym: { skin: '#8e6a4c', skinDark: '#6f503a', shirt: '#2f3a3a', shirt2: '#26302f', pants: '#2b2f2e', pants2: '#232726', hair: '#15120f', boot: '#1c1a17', hood: 1 },
  stas: { skin: '#c09672', skinDark: '#9c7453', shirt: '#6a342a', shirt2: '#57291f', pants: '#3a3a3e', pants2: '#2f2f33', hair: '#3b2a1a', boot: '#24201b' },
  luda: { skin: '#d7b08a', skinDark: '#b28c68', shirt: '#6a5a7a', shirt2: '#584a66', pants: '#3a3a46', pants2: '#2f2f39', hair: '#6b4a2a', boot: '#26231f' },
  zWalker: { skin: '#7c8a63', skinDark: '#5f6b4b', shirt: '#4a4a3e', shirt2: '#3c3c31', pants: '#3a3a35', pants2: '#2f2f2b', hair: '#3a3a2a', boot: '#242420', blood: '#4a1414' },
  zRunner: { skin: '#9e7f56', skinDark: '#7d6344', shirt: '#5a4034', shirt2: '#48332a', pants: '#3a302a', pants2: '#2f2620', hair: '#2a1f18', boot: '#221c18', blood: '#5a1414' },
  zCrawler: { skin: '#6f735e', skinDark: '#565a48', shirt: '#3f4238', shirt2: '#33352d', pants: '#33352d', pants2: '#292b25', hair: '#2a2c24', boot: '#1f211c', blood: '#4a1414' },
  zBloater: { skin: '#8c9a4e', skinDark: '#6d7840', shirt: '#54603a', shirt2: '#454f2f', pants: '#3a3a2a', pants2: '#2f2f22', hair: '#2a2c1a', boot: '#232318', blood: '#5a5a14' },
  zScreamer: { skin: '#a95f54', skinDark: '#86483f', shirt: '#5a3a3a', shirt2: '#48302f', pants: '#3a2c2c', pants2: '#2f2424', hair: '#2a1a1a', boot: '#231a1a', blood: '#5a1414' },
  zBrute: { skin: '#6a7a4a', skinDark: '#525f39', shirt: '#3a4030', shirt2: '#2f3427', pants: '#332f27', pants2: '#29261f', hair: '#242218', boot: '#1d1b16', blood: '#4a1414' }
};

/* ---------- человечек: 4 направления, 16×24, пиксель-арт ---------- */
function drawHumanTop(g, pal, frame, opts) {
  opts = opts || {};
  const dirName = opts.dirName || 'down';         // down/up/left/right
  const walk = opts.walk ? 1 : 0;
  const swing = walk ? (frame % 2 ? 1 : -1) : 0;
  const bob = walk ? (frame % 2 ? -1 : 0) : 0;
  const skin = pal.skin, skin2 = pal.skinDark || pal.skin;
  const shirt = pal.shirt, shirt2 = pal.shirt2 || pal.shirt;
  const pants = pal.pants, pants2 = pal.pants2 || pal.pants;
  const boot = pal.boot || '#1d1a17';
  const hair = pal.hair || '#221c16';
  const dead = !!opts.dead;

  // тень
  g.fillStyle = 'rgba(0,0,0,0.38)';
  g.fillRect(4, 22, 8, 2);

  const legY = 16 + bob;
  // ноги
  g.fillStyle = pants;
  g.fillRect(5, legY, 2, 5 - Math.abs(swing));
  g.fillRect(9, legY, 2, 5 - (walk ? 1 - Math.abs(swing) : 0));
  if (walk && swing > 0) { g.fillRect(4, legY + 3, 2, 2); g.fillRect(10, legY + 1, 2, 2); }
  g.fillStyle = boot;
  g.fillRect(5, legY + 5, 3, 1); g.fillRect(9, legY + 5, 3, 1);

  // корпус
  g.fillStyle = shirt; g.fillRect(4, 9 + bob, 8, 8);
  g.fillStyle = shirt2; g.fillRect(4, 9 + bob, 8, 1);
  // складки/ремень
  g.fillStyle = shade(shirt, -18); g.fillRect(4, 14 + bob, 8, 1);
  g.fillStyle = shade(pants, 14); g.fillRect(4, 16 + bob, 8, 1);
  if (opts.bag) { g.fillStyle = '#3a3226'; g.fillRect(3, 10 + bob, 3, 5); g.fillStyle = '#524634'; g.fillRect(3, 10 + bob, 3, 1); }

  // руки
  const armY = 10 + bob;
  if (opts.armsForward) {
    // вытянуты вперёд (зомби/бег)
    g.fillStyle = skin;
    if (dirName === 'up') { g.fillRect(2, armY - 2, 2, 5); g.fillRect(12, armY - 2, 2, 5); }
    else if (dirName === 'left') { g.fillRect(1, armY, 5, 2); }
    else if (dirName === 'right') { g.fillRect(10, armY, 5, 2); }
    else { g.fillRect(1, armY + 1, 3, 2); g.fillRect(12, armY + 1, 3, 2); }
  } else {
    g.fillStyle = shirt;
    g.fillRect(2, armY, 2, 4); g.fillRect(12, armY, 2, 4);
    g.fillStyle = skin;
    g.fillRect(2, armY + 4 + swing, 2, 2); g.fillRect(12, armY + 4 - swing, 2, 2);
  }

  // голова
  const hy = 3 + bob;
  if (dirName === 'up') {
    // затылок
    g.fillStyle = skin2; g.fillRect(5, hy, 6, 6);
    g.fillStyle = hair; g.fillRect(4, hy - 1, 8, 7);
    g.fillStyle = shade(hair, 16); g.fillRect(5, hy, 6, 2);
    // уши
    g.fillStyle = skin2; g.fillRect(4, hy + 3, 1, 2); g.fillRect(11, hy + 3, 1, 2);
  } else if (dirName === 'left' || dirName === 'right') {
    const flip = dirName === 'left';
    g.fillStyle = skin; g.fillRect(5, hy, 6, 6);
    g.fillStyle = hair; g.fillRect(4, hy - 1, 8, 3);
    g.fillRect(flip ? 9 : 4, hy + 1, 3, 5);          // волосы сзади
    g.fillStyle = skin2; g.fillRect(flip ? 10 : 5, hy + 3, 1, 2); // ухо
    g.fillStyle = dead ? '#d8d2b0' : '#141210';
    g.fillRect(flip ? 5 : 9, hy + 2, 2, 2);          // глаз
    if (dead) { g.fillStyle = '#a02a2a'; g.fillRect(flip ? 5 : 9, hy + 3, 1, 1); }
    g.fillStyle = dead ? '#3a1414' : '#7a5240';
    g.fillRect(flip ? 5 : 8, hy + 5, 2, 1);          // рот
  } else {
    // лицо
    g.fillStyle = skin; g.fillRect(5, hy, 6, 6);
    g.fillStyle = hair; g.fillRect(5, hy - 1, 6, 2); g.fillRect(4, hy, 1, 3); g.fillRect(11, hy, 1, 3);
    g.fillStyle = dead ? '#d8d2b0' : '#141210';
    g.fillRect(6, hy + 2, 1, 2); g.fillRect(9, hy + 2, 1, 2);
    if (dead) { g.fillStyle = '#a02a2a'; g.fillRect(6, hy + 3, 1, 1); g.fillRect(9, hy + 3, 1, 1); }
    g.fillStyle = shade(skin, -14); g.fillRect(8, hy + 3, 1, 2);   // нос
    g.fillStyle = dead ? '#3a1414' : '#6e4434';
    g.fillRect(7, hy + 5, 3, 1);
    if (dead) { g.fillStyle = '#e8e2c0'; g.fillRect(7, hy + 5, 1, 1); g.fillRect(9, hy + 5, 1, 1); }
  }
  // шрам/повязка
  if (opts.bandage) { g.fillStyle = '#d8d0bc'; g.fillRect(4, hy + 4, 8, 2); g.fillStyle = '#a02a2a'; g.fillRect(6, hy + 4, 1, 2); }
  // кровь
  if (opts.blood) {
    g.fillStyle = pal.blood || '#5a1414';
    g.fillRect(4, 11 + bob, 2, 1); g.fillRect(9, 13 + bob, 2, 1); g.fillRect(6, hy + 6, 2, 1);
    g.fillRect(12, 15 + bob, 1, 2);
  }
}

/* осветлить/затемнить hex-цвет */
function shade(hex, amt) {
  const h = hex.replace('#', '');
  const n = parseInt(h.length === 3 ? h.split('').map(c => c + c).join('') : h, 16);
  let r = (n >> 16) + amt, gg = ((n >> 8) & 255) + amt, b = (n & 255) + amt;
  r = clamp(r, 0, 255); gg = clamp(gg, 0, 255); b = clamp(b, 0, 255);
  return '#' + ((1 << 24) | (r << 16) | (gg << 8) | b).toString(16).slice(1);
}

function dirNameOf(dir) {
  const a = Math.atan2(Math.sin(dir || 0), Math.cos(dir || 0));
  if (a > -Math.PI * 0.75 && a <= -Math.PI * 0.25) return 'up';
  if (a > Math.PI * 0.25 && a <= Math.PI * 0.75) return 'down';
  if (a <= -Math.PI * 0.75 || a > Math.PI * 0.75) return 'left';
  return 'right';
}

function spriteHuman(key, frame, opts) {
  opts = opts || {};
  const dn = opts.dirName || 'down';
  const ck = 'h_' + key + '_' + (frame & 1) + '_' + dn + '_' + (opts.walk ? 1 : 0) + (opts.armsForward ? 'a' : '') + (opts.blood ? 'b' : '') + (opts.bandage ? 'd' : '');
  if (SPR.cache.has(ck)) return SPR.cache.get(ck);
  const { c, g } = makeCanvas(16, 24);
  const pal = PAL[key] || PAL.player;
  drawHumanTop(g, pal, frame & 1, Object.assign({}, opts, { dirName: dn }));
  SPR.cache.set(ck, c);
  return c;
}

/* ---------- зомби ---------- */
function spriteZombie(kind, frame, opts) {
  opts = opts || {};
  const dn = opts.dirName || 'down';
  const ck = 'z_' + kind + '_' + (frame & 1) + '_' + dn;
  if (SPR.cache.has(ck)) return SPR.cache.get(ck);
  const { c, g } = makeCanvas(16, 24);
  const pal = PAL[kind === 'walker' ? 'zWalker' : kind === 'runner' ? 'zRunner' : kind === 'crawler' ? 'zCrawler' : kind === 'bloater' ? 'zBloater' : kind === 'screamer' ? 'zScreamer' : 'zBrute'];
  const o = Object.assign({}, opts, { dead: true, blood: true, armsForward: kind !== 'crawler', walk: true, dirName: dn });
  if (kind === 'crawler') {
    // ползун: горизонтальное тело
    g.fillStyle = 'rgba(0,0,0,0.35)'; g.fillRect(2, 20, 12, 2);
    g.fillStyle = pal.shirt; g.fillRect(3, 14, 9, 5);
    g.fillStyle = pal.skin; g.fillRect(11, 13, 4, 4);
    g.fillStyle = '#c9c2a0'; g.fillRect(13, 14, 1, 1); g.fillRect(13, 16, 1, 1);
    g.fillStyle = '#3a1414'; g.fillRect(14, 17, 1, 1);
    g.fillStyle = pal.pants; g.fillRect(1, 16, 4, 3);
    g.fillStyle = pal.blood; g.fillRect(4, 18, 3, 1); g.fillRect(8, 15, 2, 1);
    g.fillStyle = pal.skinDark; g.fillRect(2, 19, 2, 2); g.fillRect(9, 19, 2, 2);
  } else if (kind === 'brute') {
    // громила — крупнее
    g.fillStyle = 'rgba(0,0,0,0.4)'; g.fillRect(1, 23, 14, 1);
    g.fillStyle = pal.pants; g.fillRect(4, 18, 3, 5); g.fillRect(9, 18, 3, 5);
    g.fillStyle = pal.shirt; g.fillRect(2, 8, 12, 11);
    g.fillStyle = pal.shirt2; g.fillRect(2, 8, 12, 3);
    g.fillStyle = pal.skin; g.fillRect(0, 12, 3, 3); g.fillRect(13, 12, 3, 3);
    g.fillStyle = pal.skin; g.fillRect(4, 1, 8, 7);
    g.fillStyle = '#c9c2a0'; g.fillRect(5, 3, 2, 2); g.fillRect(9, 3, 2, 2);
    g.fillStyle = '#2a1414'; g.fillRect(6, 4, 1, 1); g.fillRect(10, 4, 1, 1);
    g.fillStyle = '#3a1414'; g.fillRect(6, 6, 4, 2);
    g.fillStyle = pal.blood; g.fillRect(3, 12, 3, 2); g.fillRect(10, 15, 2, 2);
  } else {
    drawHumanTop(g, pal, frame, o);
    if (kind === 'bloater') {
      g.fillStyle = pal.skin; g.fillRect(3, 11, 10, 5);
      g.fillStyle = pal.skinDark; g.fillRect(4, 12, 2, 2); g.fillRect(9, 13, 2, 2);
      g.fillStyle = '#b8b83a'; g.fillRect(6, 15, 2, 1);
    }
    if (kind === 'screamer') {
      g.fillStyle = '#3a1414'; g.fillRect(6, 8, 4, 3);
      g.fillStyle = '#d9c07a'; g.fillRect(7, 8, 1, 2); g.fillRect(9, 8, 1, 2);
    }
  }
  SPR.cache.set(ck, c);
  return c;
}

/* ---------- портрет 64×64 для диалогов ---------- */
function spritePortrait(id) {
  const ck = 'p_' + id;
  if (SPR.cache.has(ck)) return SPR.cache.get(ck);
  const { c, g } = makeCanvas(64, 64);
  const isZ = id.startsWith('z');
  const pal = PAL[id] || PAL.player;
  // фон
  g.fillStyle = isZ ? '#1a1c14' : '#171a1c';
  g.fillRect(0, 0, 64, 64);
  g.fillStyle = isZ ? '#22261a' : '#1f2426';
  for (let i = 0; i < 18; i++) g.fillRect((i * 37) % 64, (i * 23) % 64, 3, 3);
  // шея и плечи
  g.fillStyle = pal.shirt; g.fillRect(12, 46, 40, 18);
  g.fillStyle = pal.shirt2 || pal.shirt; g.fillRect(12, 46, 40, 4);
  // голова
  const skin = pal.skin, skin2 = pal.skinDark || pal.skin;
  g.fillStyle = skin; g.fillRect(20, 14, 24, 30);
  g.fillStyle = skin2; g.fillRect(20, 40, 24, 4);
  // уши
  g.fillStyle = skin2; g.fillRect(17, 26, 3, 8); g.fillRect(44, 26, 3, 8);
  // волосы
  g.fillStyle = pal.hair || '#222';
  if (pal.hood) { g.fillRect(16, 10, 32, 10); g.fillRect(16, 10, 6, 26); g.fillRect(42, 10, 6, 26); }
  else { g.fillRect(20, 8, 24, 8); g.fillRect(18, 12, 4, 12); g.fillRect(42, 12, 4, 12); }
  // глаза
  if (isZ) {
    g.fillStyle = '#d8d2b0'; g.fillRect(24, 26, 6, 5); g.fillRect(35, 26, 6, 5);
    g.fillStyle = '#1a1414'; g.fillRect(26, 28, 2, 2); g.fillRect(37, 28, 2, 2);
    g.fillStyle = '#4a1414'; g.fillRect(23, 25, 8, 1); g.fillRect(34, 25, 8, 1);
  } else {
    g.fillStyle = '#141210'; g.fillRect(25, 27, 4, 3); g.fillRect(36, 27, 4, 3);
    g.fillStyle = '#f0e8d8'; g.fillRect(26, 27, 1, 1); g.fillRect(37, 27, 1, 1);
  }
  // нос и рот
  g.fillStyle = skin2; g.fillRect(31, 30, 3, 6);
  g.fillStyle = isZ ? '#3a1414' : '#6a4434'; g.fillRect(26, 40, 12, 2);
  if (isZ) { g.fillStyle = '#d8d2b0'; g.fillRect(27, 40, 2, 2); g.fillRect(31, 40, 2, 2); g.fillRect(35, 40, 2, 2); }
  // кровь/шрамы
  if (pal.blood || isZ) {
    g.fillStyle = pal.blood || '#5a1414';
    g.fillRect(22, 34, 2, 8); g.fillRect(40, 20, 2, 6); g.fillRect(28, 44, 4, 2);
  }
  // рамка
  g.fillStyle = '#000'; g.fillRect(0, 0, 64, 2); g.fillRect(0, 62, 64, 2); g.fillRect(0, 0, 2, 64); g.fillRect(62, 0, 2, 64);
  SPR.cache.set(ck, c);
  return c;
}

/* ---------- иконки предметов 16×16 ---------- */
function iconItem(key) {
  const ck = 'i_' + key;
  if (SPR.cache.has(ck)) return SPR.cache.get(ck);
  const { c, g } = makeCanvas(16, 16);
  const R = (x, y, w, h, col) => { g.fillStyle = col; g.fillRect(x, y, w, h); };
  switch (key) {
    case 'medkit': R(2, 4, 12, 9, '#e8e4d8'); R(7, 6, 2, 5, '#c22'); R(5, 8, 6, 2, '#c22'); R(9, 2, 4, 2, '#b8b4a8'); break;
    case 'bandage': R(3, 5, 10, 6, '#e8e4d8'); R(5, 7, 6, 2, '#c8b8a0'); break;
    case 'pills': R(3, 6, 10, 5, '#e8e4d8'); R(4, 7, 3, 2, '#7ac'); R(9, 7, 3, 2, '#c77'); break;
    case 'food': R(3, 4, 10, 9, '#a8a8b0'); R(4, 5, 8, 2, '#7a7a84'); R(6, 7, 4, 4, '#c84'); break;
    case 'water': R(5, 3, 6, 10, '#5aa8d8'); R(6, 4, 4, 2, '#8cd'); R(5, 11, 6, 2, '#3a7898'); break;
    case 'battery': R(5, 3, 6, 11, '#c8c84a'); R(6, 2, 4, 1, '#888'); R(7, 5, 2, 4, '#fff'); R(6, 10, 4, 2, '#222'); break;
    case 'fuel': R(3, 4, 10, 10, '#c84a2a'); R(4, 5, 8, 2, '#e86a4a'); R(11, 3, 2, 3, '#888'); break;
    case 'flare': R(3, 6, 10, 4, '#d84a2a'); R(12, 5, 3, 3, '#ffd27a'); break;
    case 'bar': R(2, 7, 12, 2, '#a8a8b0'); R(3, 6, 3, 4, '#888'); break;
    case 'bat': R(3, 10, 8, 3, '#8a6a3a'); R(10, 5, 4, 6, '#a8844a'); break;
    case 'axe': R(4, 8, 10, 2, '#7a5a3a'); R(10, 3, 4, 6, '#b0b0b8'); break;
    case 'knife': R(3, 8, 7, 2, '#7a5a3a'); R(9, 6, 5, 3, '#c8c8d0'); break;
    case 'pistol': R(3, 6, 10, 3, '#3a3a3e'); R(9, 9, 3, 4, '#2a2a2e'); R(4, 9, 2, 2, '#555'); break;
    case 'shotgun': R(1, 7, 14, 2, '#5a4a3a'); R(3, 9, 4, 2, '#3a2a1a'); R(10, 6, 4, 2, '#888'); break;
    case 'rifle': R(1, 8, 14, 2, '#4a3a2a'); R(4, 6, 5, 2, '#6a5a4a'); R(9, 9, 2, 3, '#3a2a1a'); break;
    case 'ammo9': R(4, 5, 8, 7, '#8a7a4a'); R(5, 3, 2, 4, '#d8c87a'); R(9, 3, 2, 4, '#d8c87a'); break;
    case 'ammo12': R(3, 5, 10, 7, '#8a4a3a'); R(5, 2, 3, 5, '#d84a2a'); R(9, 2, 3, 5, '#d84a2a'); break;
    case 'ammo762': R(3, 5, 10, 7, '#4a6a4a'); R(5, 2, 2, 5, '#c8c87a'); R(9, 2, 2, 5, '#c8c87a'); break;
    default: R(4, 4, 8, 8, '#888');
  }
  SPR.cache.set(ck, c);
  return c;
}

/* ---------- портрет-медальон члена группы 32×32 ---------- */
function medallion(key) {
  const ck = 'm_' + key;
  if (SPR.cache.has(ck)) return SPR.cache.get(ck);
  const { c, g } = makeCanvas(32, 32);
  const pal = PAL[key] || PAL.player;
  g.fillStyle = '#12140f'; g.fillRect(0, 0, 32, 32);
  g.fillStyle = pal.shirt; g.fillRect(6, 24, 20, 8);
  g.fillStyle = pal.skin; g.fillRect(10, 8, 12, 16);
  g.fillStyle = pal.hair || '#222'; g.fillRect(10, 5, 12, 5); g.fillRect(8, 8, 3, 8); g.fillRect(21, 8, 3, 8);
  g.fillStyle = '#141210'; g.fillRect(12, 14, 2, 2); g.fillRect(18, 14, 2, 2);
  g.fillStyle = '#6a4434'; g.fillRect(13, 20, 6, 1);
  if (key === 'yan') { g.fillStyle = '#8a2a2a'; g.fillRect(9, 6, 14, 2); }   // повязка
  if (key === 'mara') { g.fillStyle = '#e8e4d8'; g.fillRect(19, 20, 2, 3); }
  SPR.cache.set(ck, c);
  return c;
}

/* ---------- иконки HUD 16×16 ---------- */
function iconHud(kind) {
  const ck = 'u_' + kind;
  if (SPR.cache.has(ck)) return SPR.cache.get(ck);
  const { c, g } = makeCanvas(16, 16);
  const R = (x, y, w, h, col) => { g.fillStyle = col; g.fillRect(x, y, w, h); };
  switch (kind) {
    case 'eye': R(2, 6, 12, 4, '#cfe'); R(6, 5, 4, 6, '#cfe'); R(7, 7, 2, 2, '#222'); break;
    case 'ear': R(3, 3, 3, 10, '#cfe'); R(6, 5, 3, 6, '#cfe'); R(8, 7, 3, 3, '#cfe'); break;
    case 'wind': R(2, 5, 8, 2, '#9cd'); R(4, 9, 9, 2, '#9cd'); R(6, 13, 6, 1, '#9cd'); break;
    case 'bang': R(4, 3, 8, 10, '#d9c07a'); R(6, 2, 4, 1, '#888'); break;
    case 'heart': R(4, 4, 3, 4, '#c44'); R(9, 4, 3, 4, '#c44'); R(4, 7, 9, 4, '#c44'); R(5, 10, 7, 3, '#c44'); R(7, 12, 3, 2, '#c44'); break;
    case 'flame': R(6, 3, 4, 6, '#e83'); R(5, 6, 6, 7, '#e83'); R(7, 8, 3, 4, '#fd6'); break;
    case 'drop': R(6, 3, 4, 4, '#5ad'); R(5, 6, 6, 6, '#5ad'); R(7, 10, 2, 3, '#8cf'); break;
    case 'skull': R(4, 3, 8, 8, '#cfe'); R(5, 11, 6, 3, '#cfe'); R(6, 6, 2, 2, '#111'); R(9, 6, 2, 2, '#111'); break;
    case 'run': R(2, 8, 4, 2, '#cfe'); R(5, 6, 4, 2, '#cfe'); R(8, 10, 4, 2, '#cfe'); break;
    case 'walk': R(4, 5, 3, 7, '#cfe'); R(8, 6, 3, 7, '#cfe'); break;
    case 'crouch': R(3, 10, 9, 3, '#cfe'); R(5, 6, 4, 4, '#cfe'); break;
    case 'light': R(6, 3, 4, 8, '#fdd'); R(5, 11, 6, 3, '#888'); break;
    case 'breath': R(5, 4, 6, 9, '#8ab'); R(6, 2, 4, 2, '#cfe'); R(4, 9, 8, 2, '#88a'); break;
    case 'menu': R(3, 4, 10, 2, '#cfe'); R(3, 7, 10, 2, '#cfe'); R(3, 10, 10, 2, '#cfe'); break;
    default: R(4, 4, 8, 8, '#888');
  }
  SPR.cache.set(ck, c);
  return c;
}

/* ---------- капля крови (для HUD) ---------- */
function bloodSplat(g, x, y, r, alpha) {
  g.globalAlpha = alpha;
  const rnd = mulberry32((x * 31 + y * 17) | 0);
  g.fillStyle = '#4a0e0e';
  for (let i = 0; i < 5; i++) {
    const rr = r * (0.3 + rnd() * 0.7);
    g.beginPath(); g.arc(x + (rnd() - 0.5) * r * 2.2, y + (rnd() - 0.5) * r * 1.6, rr, 0, 6.284); g.fill();
  }
  g.globalAlpha = 1;
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { PAL, spriteHuman, spriteZombie, spritePortrait, iconItem, iconHud, medallion, bloodSplat, makeCanvas };
}
