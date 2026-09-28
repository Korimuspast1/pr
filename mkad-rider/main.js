import * as THREE from './three.module.min.js';
import { createTextures } from './textures.js';
import { buildBike } from './bike.js';
import { buildCar, buildTruck, CAR_COLORS, TRUCK_COLORS } from './cars.js';
import { World, LANES_X, X_MIN, X_MAX, ONCOMING_X } from './world.js';
import { GameAudio } from './audio.js';

// ============================================================
const $ = id => document.getElementById(id);
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const lerp = (a, b, t) => a + (b - a) * t;
const rnd = (a, b) => a + Math.random() * (b - a);

// ---------- рендер ----------
const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.setSize(innerWidth, innerHeight);
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.12;
$('game').appendChild(renderer.domElement);

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(62, innerWidth / innerHeight, 0.1, 3000);
camera.position.set(9, 2, -6);

const T = createTextures(renderer);
const world = new World(scene, renderer, T);
world.setCamera(camera);

// ---------- мотоцикл ----------
let paintHex = 0x2fa14e;
const bike = buildBike(paintHex, T);
scene.add(bike.group);

// ---------- состояние ----------
const S = {
  mode: 'title',        // title | ride | crash | pause
  x: LANES_X[2], z: 0,
  v: 0, vx: 0,
  steer: 0, steerTarget: 0,
  lean: 0, yaw: 0, pitch: 0,
  dist: 0, score: 0, nearMiss: 0,
  best: 0,
  camMode: 0,
  crashT: 0, crashSpeed: 0, crashReason: '',
  muted: false,
  // вилли (стант)
  wheelie: 0, wheelieVel: 0, wheelieTime: 0, stuntBank: 0,
};
try { S.best = parseFloat(localStorage.getItem('mkadRiderBest') || '0') || 0; } catch (e) { }

const bikeState = { v: 0, steer: 0, lean: 0, steerVis: 0 };

// ---------- звук ----------
const audio = new GameAudio();

// ---------- ввод ----------
const keys = {};
addEventListener('keydown', e => {
  if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space'].includes(e.code)) e.preventDefault();
  keys[e.code] = true;
  if (e.code === 'Enter' && S.mode === 'title') startGame();
  if (e.code === 'KeyR' && (S.mode === 'ride' || S.mode === 'crash')) restart();
  if (e.code === 'KeyC' && S.mode !== 'title') { S.camMode = (S.camMode + 1) % 3; }
  if (e.code === 'KeyM') { S.muted = !S.muted; audio.setMuted(S.muted); toast(S.muted ? 'Звук выключен' : 'Звук включен'); }
  if ((e.code === 'KeyP' || e.code === 'Escape') && (S.mode === 'ride' || S.mode === 'pause')) togglePause();
});
addEventListener('keyup', e => { keys[e.code] = false; });

const touch = { left: false, right: false, gas: false, brake: false, wheelie: false };
function bindTouch(id, prop) {
  const el = $(id);
  const on = e => { e.preventDefault(); touch[prop] = true; };
  const off = e => { e.preventDefault(); touch[prop] = false; };
  el.addEventListener('pointerdown', on);
  el.addEventListener('pointerup', off);
  el.addEventListener('pointercancel', off);
  el.addEventListener('pointerleave', off);
}
if ('ontouchstart' in window || navigator.maxTouchPoints > 0) {
  $('touch').classList.remove('hidden');
  bindTouch('tLeft', 'left'); bindTouch('tRight', 'right');
  bindTouch('tGas', 'gas'); bindTouch('tBrake', 'brake');
  bindTouch('tWheelie', 'wheelie');
}

// ---------- искры ----------
const SPARK_N = 260;
const sparkGeo = new THREE.BufferGeometry();
const sparkPos = new Float32Array(SPARK_N * 3);
const sparkVel = new Float32Array(SPARK_N * 3);
const sparkLife = new Float32Array(SPARK_N);
sparkGeo.setAttribute('position', new THREE.BufferAttribute(sparkPos, 3));
const sparkMat = new THREE.PointsMaterial({
  size: 0.09, map: T.spark, transparent: true, depthWrite: false,
  blending: THREE.AdditiveBlending, sizeAttenuation: true,
});
const sparks = new THREE.Points(sparkGeo, sparkMat);
sparks.frustumCulled = false;
scene.add(sparks);
let sparkHead = 0;
function emitSparks(x, y, z, n, spread = 2.4) {
  for (let k = 0; k < n; k++) {
    const i = sparkHead = (sparkHead + 1) % SPARK_N;
    sparkPos[i * 3] = x; sparkPos[i * 3 + 1] = y; sparkPos[i * 3 + 2] = z;
    sparkVel[i * 3] = rnd(-spread, spread);
    sparkVel[i * 3 + 1] = rnd(0.5, 4);
    sparkVel[i * 3 + 2] = rnd(-spread, spread * 0.4);
    sparkLife[i] = rnd(0.25, 0.7);
  }
}
function updateSparks(dt) {
  for (let i = 0; i < SPARK_N; i++) {
    if (sparkLife[i] <= 0) { sparkPos[i * 3 + 1] = -50; continue; }
    sparkLife[i] -= dt;
    sparkVel[i * 3 + 1] -= 12 * dt;
    sparkPos[i * 3] += sparkVel[i * 3] * dt;
    sparkPos[i * 3 + 1] += sparkVel[i * 3 + 1] * dt;
    sparkPos[i * 3 + 2] += sparkVel[i * 3 + 2] * dt;
    if (sparkPos[i * 3 + 1] < 0.02) { sparkPos[i * 3 + 1] = 0.02; sparkVel[i * 3 + 1] *= -0.35; sparkVel[i * 3] *= 0.7; sparkVel[i * 3 + 2] *= 0.7; }
  }
  sparkGeo.attributes.position.needsUpdate = true;
}

// ---------- ТРАФИК ----------
const LANESPEED = [20.5, 23.5, 26.5, 29.5, 32.5];
const cars = [];        // попутные
const oncoming = [];    // встречные (декор)

function makeCar(lane) {
  const r = Math.random();
  let car;
  if (r < 0.10 && lane <= 2) car = buildTruck(TRUCK_COLORS[(Math.random() * TRUCK_COLORS.length) | 0], T);
  else if (r < 0.32) car = buildCar('van', Math.random() < 0.75 ? 0xe9e9e9 : CAR_COLORS[(Math.random() * CAR_COLORS.length) | 0], T);
  else if (r < 0.58) car = buildCar('hatch', CAR_COLORS[(Math.random() * CAR_COLORS.length) | 0], T);
  else car = buildCar('sedan', CAR_COLORS[(Math.random() * CAR_COLORS.length) | 0], T);
  const base = car.kind === 'truck' ? rnd(17, 19.5) : LANESPEED[lane] * rnd(0.94, 1.07);
  const c = {
    ...car, lane, x: LANES_X[lane], targetX: LANES_X[lane], z: 0, v: base, baseV: base,
    changing: false, blinkT: 0, counted: false,
  };
  scene.add(car.group);
  return c;
}

function laneFree(lane, z, margin = 42, list = cars) {
  return !list.some(c => c.lane === lane && Math.abs(c.z - z) < margin + (c.hl || 0));
}

function spawnAhead() {
  for (let tries = 0; tries < 8; tries++) {
    const lane = Math.floor(Math.pow(Math.random(), 1.5) * 5); // чаще правые полосы
    const z = S.z + rnd(150, 520);
    if (!laneFree(lane, z)) continue;
    const c = makeCar(lane);
    c.z = z;
    c.group.position.set(c.x, 0, z);
    cars.push(c);
    return;
  }
}

function spawnOncoming() {
  const lane = (Math.random() * 5) | 0;
  const r = Math.random();
  const kind = r < 0.15 ? 'van' : (r < 0.5 ? 'hatch' : 'sedan');
  const car = buildCar(kind, CAR_COLORS[(Math.random() * CAR_COLORS.length) | 0], T);
  const c = { ...car, x: ONCOMING_X[lane], z: 0, v: rnd(22, 27) };
  c.group.rotation.y = Math.PI;
  scene.add(car.group);
  oncoming.push(c);
  return c;
}

function updateTraffic(dt) {
  // попутные
  for (let i = cars.length - 1; i >= 0; i--) {
    const c = cars[i];
    // впереди идущая машина
    let ahead = null, aheadGap = 1e9;
    for (const o of cars) {
      if (o !== c && Math.abs(o.x - c.x) < 2.2 && o.z > c.z) {
        const gap = o.z - c.z - o.hl - c.hl;
        if (gap < aheadGap) { aheadGap = gap; ahead = o; }
      }
    }
    if (ahead && aheadGap < 14) c.v = Math.max(c.v - 6 * dt, Math.min(c.v, ahead.v));
    else c.v = Math.min(c.v + 1.5 * dt, c.baseV);

    // перестроение, если подпирают
    if (!c.changing && ahead && aheadGap < 30 && ahead.v < c.v - 2 && Math.random() < 0.6 * dt * 10) {
      const dirs = c.lane > 0 ? [c.lane - 1] : [];
      if (c.lane < 4 && c.kind !== 'truck') dirs.push(c.lane + 1);
      for (const l of dirs.sort(() => Math.random() - 0.5)) {
        if (laneFree(l, c.z, 38)) {
          c.changing = true; c.lane = l; c.targetX = LANES_X[l];
          break;
        }
      }
    }
    if (c.changing) {
      const dx = c.targetX - c.x;
      const step = Math.sign(dx) * Math.min(Math.abs(dx), 1.35 * dt);
      c.x += step;
      const yawT = clamp(dx * 0.09, -0.09, 0.09);
      c.group.rotation.y = lerp(c.group.rotation.y, yawT, Math.min(1, 3 * dt));
      if (Math.abs(dx) < 0.05) { c.x = c.targetX; c.changing = false; c.group.rotation.y = 0; }
    }
    c.z += c.v * dt;
    c.blinkT += dt;
    const blink = c.changing && Math.sin(c.blinkT * 14) > 0;
    const side = c.targetX < c.x ? -1 : 1;
    (side < 0 ? c.blinkL : c.blinkR).emissiveIntensity = blink ? 1.6 : 0.12;
    (side < 0 ? c.blinkR : c.blinkL).emissiveIntensity = 0.12;

    for (const w of c.wheels) w.mesh.rotation.x += (c.v / w.r) * dt;

    if (c.z < S.z - 90) {
      scene.remove(c.group);
      cars.splice(i, 1);
    }
  }
  while (cars.length < 13) spawnAhead();

  // встречные (декор)
  for (const c of oncoming) {
    c.z -= c.v * dt;
    for (const w of c.wheels) w.mesh.rotation.x -= (c.v / w.r) * dt;
    if (c.z < S.z - 150) {
      c.z = S.z + rnd(400, 900);
      c.x = ONCOMING_X[(Math.random() * 5) | 0];
    }
    c.group.position.set(c.x, 0, c.z);
  }
}

function placeTraffic() {
  for (const c of cars) c.group.position.set(c.x, 0, c.z);
}

// ---------- столкновения ----------
function checkCollisions() {
  for (const c of cars) {
    const dz = Math.abs(c.z - S.z);
    if (dz > c.hl + 2.2) { c.counted = false; continue; }
    const dx = Math.abs(c.x - S.x);
    if (dz < c.hl + 1.0 && dx < c.hw + 0.38) {
      crash('Влетел в ' + (c.kind === 'truck' ? 'фуру' : c.kind === 'van' ? 'фургон' : 'машину'));
      return;
    }
    if (!c.counted && dz < c.hl + 1.6 && dx < c.hw + 1.35 && S.v - c.v > 8) {
      c.counted = true;
      S.nearMiss++;
      S.score += 250;
      showBonus('+250 &nbsp;вжух!');
    }
  }
}

// ---------- HUD: приборка как на реальном спортбайке ----------
const gaugeCtx = $('gauge').getContext('2d');
let needleRpm = 0, gaugeT = 0;

function drawCluster(dt) {
  const ctx = gaugeCtx, W = 460, H = 460;
  if (!ctx.roundRect) ctx.roundRect = function (x, y, w, h) { this.rect(x, y, w, h); };
  gaugeT += dt;
  needleRpm += (curRPM - needleRpm) * Math.min(1, 16 * dt);
  const kmh = S.v * 3.6;
  ctx.clearRect(0, 0, W, H);
  ctx.lineCap = 'butt';
  const cx = W / 2, cy = H / 2, R = 205;

  // === корпус (безель) ===
  let g = ctx.createLinearGradient(0, 0, 0, H);
  g.addColorStop(0, '#63696f'); g.addColorStop(0.5, '#22252a'); g.addColorStop(1, '#4e535a');
  ctx.beginPath(); ctx.arc(cx, cy, R + 26, 0, Math.PI * 2);
  ctx.fillStyle = g; ctx.fill();
  ctx.lineWidth = 2; ctx.strokeStyle = '#141619'; ctx.stroke();
  ctx.beginPath(); ctx.arc(cx, cy, R + 8, 0, Math.PI * 2);
  ctx.lineWidth = 4; ctx.strokeStyle = 'rgba(0,0,0,0.65)'; ctx.stroke();
  // болтики
  for (const ba of [Math.PI * 0.25, Math.PI * 0.75, Math.PI * 1.25, Math.PI * 1.75]) {
    ctx.beginPath();
    ctx.arc(cx + Math.cos(ba) * (R + 14), cy + Math.sin(ba) * (R + 14), 5, 0, Math.PI * 2);
    ctx.fillStyle = '#31353a'; ctx.fill();
    ctx.strokeStyle = '#0e1012'; ctx.lineWidth = 1.5; ctx.stroke();
  }

  // === циферблат ===
  const gd = ctx.createRadialGradient(cx, cy - 70, 30, cx, cy, R);
  gd.addColorStop(0, '#191d22'); gd.addColorStop(1, '#0a0c0f');
  ctx.beginPath(); ctx.arc(cx, cy, R, 0, Math.PI * 2);
  ctx.fillStyle = gd; ctx.fill();

  // === тахометр (главный аналоговый) ===
  const a0 = Math.PI * 0.75, SWEEP = Math.PI * 1.55, MAXR = 11; // 0..11 ×1000 об/мин
  const ang = r => a0 + SWEEP * clamp(r / MAXR, 0, 1.04);
  ctx.beginPath(); ctx.arc(cx, cy, R - 20, a0, ang(9.5));
  ctx.lineWidth = 13; ctx.strokeStyle = '#e8ecef'; ctx.stroke();
  ctx.beginPath(); ctx.arc(cx, cy, R - 20, ang(9.5), a0 + SWEEP);
  ctx.lineWidth = 13; ctx.strokeStyle = '#c1121f'; ctx.stroke();
  for (let r = 0; r <= MAXR; r += 0.5) {
    const a = ang(r), major = Number.isInteger(r);
    const r1 = R - 32, r2 = major ? R - 54 : R - 44;
    ctx.beginPath();
    ctx.moveTo(cx + Math.cos(a) * r1, cy + Math.sin(a) * r1);
    ctx.lineTo(cx + Math.cos(a) * r2, cy + Math.sin(a) * r2);
    ctx.lineWidth = major ? 5 : 2.5;
    ctx.strokeStyle = r >= 9.5 ? '#ff574a' : 'rgba(255,255,255,0.85)';
    ctx.stroke();
  }
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.font = '700 32px "Segoe UI",Arial';
  for (let r = 1; r <= MAXR; r++) {
    const a = ang(r);
    ctx.fillStyle = r >= 10 ? '#ff574a' : '#edf0f2';
    ctx.fillText(String(r), cx + Math.cos(a) * (R - 84), cy + Math.sin(a) * (R - 84));
  }
  ctx.font = '600 19px "Segoe UI",Arial';
  ctx.fillStyle = '#8b949c';
  ctx.fillText('об/мин ×1000', cx, cy - 118);
  ctx.font = '800 24px "Segoe UI",Arial';
  ctx.fillStyle = '#c8d0d5';
  ctx.fillText('МКАД·РАЙДЕР 1100', cx, cy - 152);

  // === индикаторы ===
  const blink = (gaugeT * 2.4) % 1 < 0.55;
  const fastBlink = (gaugeT * 6) % 1 < 0.5;
  ctx.font = '900 34px "Segoe UI",Arial';
  if (S.steerTarget > 0.5 && blink) { ctx.fillStyle = '#2ecc71'; ctx.fillText('◀', cx - 108, cy - 52); }
  if (S.steerTarget < -0.5 && blink) { ctx.fillStyle = '#2ecc71'; ctx.fillText('▶', cx + 108, cy - 52); }
  if (S.v < 0.6) {
    ctx.font = '800 26px "Segoe UI",Arial';
    ctx.fillStyle = '#2ecc71';
    ctx.fillText('N', cx, cy - 84);
  }
  // шифт-лайт (перед отсечкой)
  if (needleRpm > 0.88) {
    ctx.beginPath(); ctx.arc(cx, cy - 176, 10, 0, Math.PI * 2);
    ctx.fillStyle = fastBlink ? '#ff3b2e' : 'rgba(120,20,16,0.7)';
    ctx.shadowColor = '#ff3b2e'; ctx.shadowBlur = fastBlink ? 18 : 0;
    ctx.fill(); ctx.shadowBlur = 0;
  }
  // индикатор станта
  if (S.wheelie > 0.12 || S.stuntBank > 40) {
    ctx.font = '800 27px "Segoe UI",Arial';
    ctx.fillStyle = S.wheelie > 0.12 ? '#ffa62b' : 'rgba(255,166,43,0.55)';
    ctx.shadowColor = '#ffa62b'; ctx.shadowBlur = 10;
    ctx.fillText(`СТАНТ +${Math.round(S.stuntBank)}`, cx, cy + 178);
    ctx.shadowBlur = 0;
  }

  // === ЖК-центр: скорость ===
  ctx.shadowColor = '#9fffc4'; ctx.shadowBlur = 16;
  ctx.font = '900 96px Consolas,"Courier New",monospace';
  ctx.fillStyle = '#c8ffdd';
  ctx.fillText(String(Math.min(999, Math.round(kmh))), cx, cy - 8);
  ctx.shadowBlur = 0;
  ctx.font = '600 22px "Segoe UI",Arial';
  ctx.fillStyle = '#8b949c';
  ctx.fillText('км/ч', cx, cy + 48);

  // === передача ===
  ctx.strokeStyle = '#3a4147'; ctx.lineWidth = 3;
  const gw = 58, gh = 58, gx = cx, gy = cy + 108;
  ctx.beginPath(); ctx.roundRect(gx - gw / 2, gy - gh / 2, gw, gh, 9); ctx.stroke();
  ctx.font = '900 40px Consolas,"Courier New",monospace';
  ctx.fillStyle = S.v < 0.6 ? '#2ecc71' : '#eef3f6';
  ctx.fillText(S.v < 0.6 ? 'N' : String(curGear), gx, gy + 2);
  ctx.font = '600 15px "Segoe UI",Arial';
  ctx.fillStyle = '#767f86';
  ctx.fillText('ПЕРЕДАЧА', gx, gy + 48);

  // === одометр ===
  ctx.font = '600 19px Consolas,"Courier New",monospace';
  ctx.fillStyle = '#9fb3a8';
  ctx.fillText(`ОДО ${(S.dist / 1000).toFixed(1)} км`, cx, cy - 62);

  // === топливо и температура ===
  const fuel = clamp(1 - (S.dist % 12000) / 12000, 0, 1);
  const temp = clamp(S.dist / 2500, 0, 1);
  const bar = (x, y, val, hot) => {
    ctx.fillStyle = '#20262b'; ctx.fillRect(x, y, 84, 10);
    ctx.fillStyle = hot ? '#ff8c42' : val < 0.15 ? '#ff4b3e' : '#39d98a';
    ctx.fillRect(x, y, 84 * val, 10);
    ctx.strokeStyle = '#3a4147'; ctx.lineWidth = 1.5;
    ctx.strokeRect(x, y, 84, 10);
  };
  ctx.font = '700 15px "Segoe UI",Arial';
  ctx.fillStyle = '#8b949c';
  ctx.fillText('E', cx - 186, cy + 113); bar(cx - 176, cy + 106, fuel, false); ctx.fillText('F', cx - 80, cy + 113);
  ctx.fillText('C', cx + 84, cy + 113); bar(cx + 94, cy + 106, temp, true); ctx.fillText('H', cx + 188, cy + 113);

  // === стрелка тахометра ===
  const a = ang(needleRpm * MAXR);
  ctx.save();
  ctx.translate(cx, cy);
  ctx.rotate(a);
  ctx.shadowColor = '#ff2418'; ctx.shadowBlur = 12;
  ctx.beginPath();
  ctx.moveTo(-26, 0);
  ctx.lineTo(R - 44, -4);
  ctx.lineTo(R - 44, 4);
  ctx.closePath();
  ctx.fillStyle = '#ff2418'; ctx.fill();
  ctx.shadowBlur = 0;
  ctx.restore();
  // ступица
  ctx.beginPath(); ctx.arc(cx, cy, 17, 0, Math.PI * 2);
  ctx.fillStyle = '#22262b'; ctx.fill();
  ctx.lineWidth = 3; ctx.strokeStyle = '#565d64'; ctx.stroke();
  ctx.beginPath(); ctx.arc(cx, cy, 5, 0, Math.PI * 2);
  ctx.fillStyle = '#8a9299'; ctx.fill();
}

let hudTimer = 0;
function updateHUD(dt) {
  drawCluster(dt);
  hudTimer += dt;
  if (hudTimer > 0.2) {
    hudTimer = 0;
    $('dist').textContent = (S.dist / 1000).toFixed(1) + ' км';
    $('best').textContent = (Math.max(S.best, S.dist) / 1000).toFixed(1) + ' км';
    $('kmad').textContent = ((Math.floor(S.dist / 1000) + 1) % 109 || 1);
  }
}

let bonusT = null;
function showBonus(html) {
  const b = $('bonus');
  b.innerHTML = html;
  b.classList.add('show');
  clearTimeout(bonusT);
  bonusT = setTimeout(() => b.classList.remove('show'), 1300);
}

function toast(msg) {
  const t = $('toast');
  t.textContent = msg;
  t.style.display = 'block';
  setTimeout(() => { t.style.display = 'none'; }, 2600);
}

// показ ошибок (для отладки)
addEventListener('error', e => {
  const t = $('toast');
  if (t) {
    t.textContent = 'Ошибка: ' + (e.message || e.error);
    t.style.display = 'block';
  }
});

// ---------- физика мотоцикла ----------
const GEARS = [0, 11, 19, 28, 38, 49, 63];
let curGear = 1, curRPM = 0.12;

function physics(dt) {
  const gas = (keys['KeyW'] || keys['ArrowUp'] || touch.gas) ? 1 : 0;
  const brake = (keys['KeyS'] || keys['ArrowDown'] || keys['Space'] || touch.brake) ? 1 : 0;
  const wantWheelie = (keys['ShiftLeft'] || keys['ShiftRight'] || touch.wheelie) && S.v > 4 && !brake;
  let steerIn = 0;
  if (keys['KeyA'] || keys['ArrowLeft'] || touch.left) steerIn += 1;   // влево = +x
  if (keys['KeyD'] || keys['ArrowRight'] || touch.right) steerIn -= 1; // вправо = -x
  steerIn = clamp(steerIn, -1, 1);

  // руль: мягкое нарастание / возврат
  const rate = steerIn === 0 ? 5.5 : 2.9;
  S.steer = lerp(S.steer, steerIn, Math.min(1, rate * dt));
  S.steerTarget = steerIn;

  // ---------- ВИЛЛИ (стант на Shift) ----------
  const bal = 1.02;                 // точка баланса (рад)
  if (wantWheelie) {
    const speedK = clamp(1.15 - S.v / 45, 0.55, 1.15);
    const lift = (2.0 + gas * 4.4) * clamp(1 - S.wheelie / 1.12, 0, 1) * speedK;
    S.wheelieVel += lift * dt;
    S.wheelieVel += (bal - S.wheelie) * 2.4 * dt;      // стремление к балансу
    // «подхват сцеплением»: с малой скорости — резкий старт
    if (gas && S.v < 13 && S.wheelie < 0.25) S.wheelieVel += 3.0 * clamp(1.6 - S.v / 8, 0, 1.6) * dt;
    // НИЗКАЯ СКОРОСТЬ = НЕУСТОЙЧИВОСТЬ (нет гироскопа) — выше 60° тянет на спину
    if (S.wheelie > 1.05) S.wheelieVel += clamp(0.9 - S.v / 13, 0, 0.9) * 2.4 * dt;
    S.wheelieTime += dt;
    S.stuntBank += dt * (15 + S.v * 1.6 + S.wheelie * 55);
  } else {
    S.wheelieVel -= 5.0 * dt;        // газ сброшен — перед опускается
    if (S.wheelie <= 0.001 && S.stuntBank > 40) {
      // приземлился — банк станта в зачёт
      S.score += S.stuntBank;
      showBonus(`+${Math.round(S.stuntBank)} СТАНТ!`);
      S.stuntBank = 0;
      S.wheelieTime = 0;
    }
    if (S.wheelie <= 0.001) S.stuntBank = 0;
  }
  const damp = S.wheelie > 1.05 ? 1.7 : 3.6;           // у предела контроль теряется
  S.wheelieVel *= Math.max(0, 1 - damp * dt);
  S.wheelie += S.wheelieVel * dt;
  if (S.wheelie < 0) { S.wheelie = 0; S.wheelieVel = 0; }
  if (S.wheelie > 1.28) { crash('Перевернулся на вилли! Держи газ и баланс'); return; }

  // ---------- продольная динамика ----------
  const aEngine = Math.min(11.5, 165 / (S.v + 6));
  let a = gas * aEngine - 0.00062 * S.v * S.v - 0.35;
  if (brake) a -= 11.5;
  if (!gas && !brake) a -= 0.5; // торможение двигателем
  if (S.wheelie > 0.1) a -= 0.8; // вилли тормозит
  const vPrev = S.v;
  S.v = Math.max(0, S.v + a * dt);
  if (S.v > 63) S.v = 63;

  // ---------- поперечная динамика (плавно, с лимитом ускорения) ----------
  const maxLat = 3.1 + 6.5 / (1 + S.v * 0.13);
  const steerEff = S.steer * (S.wheelie > 0.15 ? 0.35 : 1); // вилли — руль слабее
  const targetVx = steerEff * maxLat;
  const LAT_ACC = 7.5;
  const dv = clamp(targetVx - S.vx, -LAT_ACC * dt, LAT_ACC * dt);
  S.vx += dv;
  S.x += S.vx * dt;
  S.z += S.v * dt;
  S.dist += S.v * dt;
  S.score += S.v * dt;

  // границы: отбойник и бетон
  const R_LIMIT = 19.55, L_LIMIT = 0.95;
  if (S.x > R_LIMIT) {
    emitSparks(S.x + 0.1, 0.5, S.z + rnd(-0.5, 0.5), 6, 3);
    if (S.vx < -4.5) crash('Втёрся в отбойник');
    S.x = R_LIMIT; S.vx = Math.min(S.vx, -Math.abs(S.vx) * 0.4 - 0.2);
    S.v *= Math.max(0.75, 1 - 0.5 * dt * 10);
  }
  if (S.x < L_LIMIT) {
    emitSparks(S.x - 0.35, 0.55, S.z + rnd(-0.5, 0.5), 6, 3);
    if (S.vx > 4.5) crash('Догнал бетон МКАДа');
    S.x = L_LIMIT; S.vx = Math.max(S.vx, Math.abs(S.vx) * 0.4 + 0.2);
    S.v *= Math.max(0.75, 1 - 0.5 * dt * 10);
  }

  // визуальный наклон и курс — всё сглажено
  const leanTarget = clamp(S.vx / Math.max(maxLat, 0.1) * 0.58 * Math.min(S.v / 9, 1), -0.62, 0.62);
  S.lean = lerp(S.lean, leanTarget, Math.min(1, 4.5 * dt));
  const yawTarget = Math.atan2(S.vx, Math.max(S.v, 6)) * 0.85;
  S.yaw = lerp(S.yaw, yawTarget, Math.min(1, 6 * dt));
  const acc = (S.v - vPrev) / dt;
  const pitchTarget = clamp(-acc * 0.004 - gas * (S.v < 12 ? 0.05 : 0.01), -0.09, 0.12);
  S.pitch = lerp(S.pitch, pitchTarget, Math.min(1, 6 * dt));

  // передачи
  let g = 1;
  for (let i = 0; i < GEARS.length - 1; i++) if (S.v >= GEARS[i]) g = i + 1;
  curGear = g;
  const lo = GEARS[g - 1], hi = GEARS[g];
  curRPM = clamp(0.12 + 0.88 * (S.v - lo) / (hi - lo), 0.10, 1) + (S.wheelie > 0.1 ? 0.06 : 0);

  checkCollisions();
}

// ---------- авария ----------
function crash(reason = 'ДТП на МКАДе') {
  if (S.mode !== 'ride') return;
  S.mode = 'crash';
  S.crashT = 0;
  S.crashSpeed = S.v;
  S.crashReason = reason;
  S.stuntBank = 0;
  audio.crash();
  emitSparks(S.x, 0.6, S.z, 40, 5);
  S.best = Math.max(S.best, S.dist);
  try { localStorage.setItem('mkadRiderBest', String(S.best)); } catch (e) { }
  setTimeout(() => {
    if (S.mode !== 'crash') return;
    $('crashReason').textContent = reason;
    $('cDist').textContent = (S.dist / 1000).toFixed(1);
    $('cSpeed').textContent = Math.round(S.crashSpeed * 3.6);
    $('cBest').textContent = (S.best / 1000).toFixed(1);
    $('crash').classList.remove('hidden');
  }, 1100);
}

function restart() {
  $('crash').classList.add('hidden');
  for (const c of cars) scene.remove(c.group);
  cars.length = 0;
  S.mode = 'ride';
  S.x = LANES_X[2]; S.z += 10; S.v = 8; S.vx = 0;
  S.dist = 0; S.score = 0; S.nearMiss = 0;
  S.lean = 0; S.yaw = 0; S.pitch = 0; S.steer = 0;
  S.wheelie = 0; S.wheelieVel = 0; S.wheelieTime = 0; S.stuntBank = 0;
  bike.group.rotation.set(0, 0, 0);
  for (let i = 0; i < 10; i++) spawnAhead();
  audio.resume();
}

// ---------- камера ----------
const camPos = new THREE.Vector3(9, 2, -6);
const camLookSm = new THREE.Vector3(9, 1, 10);
const camDesired = new THREE.Vector3();
const camLookT = new THREE.Vector3();
const _headW = new THREE.Vector3();
const _fwd = new THREE.Vector3();
const _UPV = new THREE.Vector3(0, 1, 0);
let fovCur = 62;

function updateCamera(dt, t) {
  const v = S.v;

  if (S.mode === 'title') {
    const a = t * 0.22;
    camPos.set(S.x + Math.sin(a) * 5.2, 1.55 + Math.sin(t * 0.3) * 0.25, S.z - Math.cos(a) * 5.2);
    camera.position.copy(camPos);
    camLookSm.set(S.x, 0.9, S.z);
    camera.lookAt(camLookSm);
    fovCur = lerp(fovCur, 50, Math.min(1, 4 * dt));
    camera.fov = fovCur;
    camera.updateProjectionMatrix();
    return;
  }

  if (S.camMode === 2) {
    // ===== камера от шлема: сидим на голове райдера =====
    bike.group.updateMatrixWorld();
    bike.head.getWorldPosition(_headW);
    camera.position.copy(_headW);
    // направление взгляда — вперёд по мотоциклу (с наклоном и вилли)
    _fwd.set(0, 0, 1).applyQuaternion(bike.group.quaternion);
    camLookT.copy(_headW).addScaledVector(_fwd, 40);
    camLookT.y -= 1.6; // чуть вниз к дороге
    camLookSm.lerp(camLookT, Math.min(1, 22 * dt));
    camera.up.set(0, 1, 0);
    camera.lookAt(camLookSm);
    // крен камеры = крен головы (частично выпрямленный)
    camera.rotateZ(S.lean * 0.62);
    // микро-тряска от дороги
    const amp = Math.pow(Math.min(v / 60, 1), 2) * 0.006;
    camera.rotateZ(Math.sin(t * 47) * amp);
    fovCur = lerp(fovCur, 78 + v * 0.12, Math.min(1, 6 * dt));
  } else {
    // ===== погоня / близкая (плавный демпфер) =====
    const back = (S.camMode === 0 ? 4.7 : 3.4) + v * 0.016;
    const up = (S.camMode === 0 ? 1.68 : 1.42) + v * 0.003;
    camDesired.set(S.x * 0.96, up, S.z - back);
    const k = 1 - Math.exp(-5.5 * dt);
    camPos.x += (camDesired.x - camPos.x) * k;
    camPos.y += (camDesired.y - camPos.y) * k;
    camPos.z += (camDesired.z - camPos.z) * k;

    // мягкая вибрация (синусоиды, не белый шум)
    const shakeAmp = Math.pow(Math.min(v / 62, 1), 2.2) * 0.05 * (1 + S.wheelie * 2.5);
    const vibX = (Math.sin(t * 31.4) * 0.6 + Math.sin(t * 47.1 + 2.1) * 0.4) * shakeAmp;
    const vibY = (Math.sin(t * 36.7 + 1.0) * 0.6 + Math.sin(t * 53.0 + 3.0) * 0.4) * shakeAmp;
    const vibZ = (Math.sin(t * 41.3 + 0.5)) * shakeAmp * 0.6;

    camera.position.set(camPos.x + vibX, camPos.y + vibY, camPos.z + vibZ);
    camLookT.set(S.x, 1.0 + S.wheelie * 0.55, S.z + 7 + v * 0.05);
    camLookSm.lerp(camLookT, Math.min(1, 9 * dt));
    camera.up.set(0, 1, 0);
    camera.lookAt(camLookSm);
    camera.rotateZ(S.lean * 0.09); // лёгкий крен за байком
    fovCur = lerp(fovCur, (S.camMode === 0 ? 62 : 66) + v * 0.15, Math.min(1, 4 * dt));
  }
  camera.fov = fovCur;
  camera.updateProjectionMatrix();
}

// ---------- пауза / старт ----------
function togglePause() {
  if (S.mode === 'ride') {
    S.mode = 'pause';
    $('pause').classList.remove('hidden');
    audio.idle();
  } else if (S.mode === 'pause') {
    S.mode = 'ride';
    $('pause').classList.add('hidden');
    audio.resume();
  }
}
document.addEventListener('visibilitychange', () => {
  if (document.hidden && S.mode === 'ride') togglePause();
});

function startGame() {
  if (S.mode !== 'title') return;
  $('title').classList.add('hidden');
  $('hud').classList.remove('hidden');
  S.mode = 'ride';
  S.v = 6;
  audio.init();
  audio.resume();
  for (let i = 0; i < 12; i++) spawnAhead();
  for (let i = 0; i < 9; i++) {
    const c = spawnOncoming();
    c.z = rnd(-100, 800);
    c.group.position.set(c.x, 0, c.z);
  }
}
$('startBtn').addEventListener('click', startGame);
$('againBtn').addEventListener('click', restart);
$('resumeBtn').addEventListener('click', togglePause);

// выбор цвета
document.querySelectorAll('.swatch').forEach(sw => {
  sw.addEventListener('click', () => {
    document.querySelectorAll('.swatch').forEach(s => s.classList.remove('sel'));
    sw.classList.add('sel');
    paintHex = parseInt(sw.dataset.c.slice(1), 16);
    bike.setPaint(paintHex);
  });
});

addEventListener('resize', () => {
  camera.aspect = innerWidth / innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(innerWidth, innerHeight);
});

// ---------- адаптивное качество ----------
let slowTime = 0, pr = Math.min(window.devicePixelRatio, 2);
function adaptQuality(dt) {
  if (dt > 0.034 && S.mode === 'ride') slowTime += dt; else slowTime = Math.max(0, slowTime - dt * 0.5);
  if (slowTime > 2.2 && pr > 1) {
    pr = 1; renderer.setPixelRatio(1); slowTime = 0;
  }
}

// ---------- главный цикл ----------
let last = performance.now();
const PHYS_DT = 1 / 120;      // фиксированный шаг физики = плавный ход
let physAcc = 0;
// вращение вокруг задней оси (вилли)
const RA_LOCAL = new THREE.Vector3(0, 0.33, -0.68); // задняя ось в координатах байка
const _eul = new THREE.Euler(0, 0, 0, 'YXZ');
const _quat = new THREE.Quaternion();
const _raW = new THREE.Vector3();

function frame(now) {
  requestAnimationFrame(frame);
  let dt = (now - last) / 1000;
  last = now;
  if (dt > 0.1) dt = 0.1;
  const t = now / 1000;

  if (S.mode === 'ride' && !window.__mkad?.freeze) {
    // физика фиксированными шагами — движение ровное при любом FPS
    physAcc = Math.min(physAcc + dt, 0.25);
    while (physAcc >= PHYS_DT) {
      physics(PHYS_DT);
      updateTraffic(PHYS_DT);
      physAcc -= PHYS_DT;
      if (S.mode !== 'ride') break;
    }
    updateHUD(dt);
    audio.update(curRPM, ((keys['KeyW'] || keys['ArrowUp'] || touch.gas) ? 0.85 : 0)
      + (S.wheelie > 0.1 ? 0.15 : 0), S.v);
    adaptQuality(dt);
  } else if (S.mode === 'ride' && window.__mkad?.freeze) {
    updateHUD(dt);
  } else if (S.mode === 'crash') {
    S.crashT += dt;
    // скольжение
    S.v = Math.max(0, S.v - 9 * dt);
    S.z += S.v * dt;
    S.x += S.vx * dt * 0.3;
    S.vx *= Math.max(0, 1 - 2.5 * dt);
    S.wheelie = lerp(S.wheelie, 0, Math.min(1, 4 * dt));
    updateTraffic(dt);
    updateHUD(dt);
    audio.idle();
    if (S.crashT < 0.4 && Math.random() < 0.4) emitSparks(S.x, 0.3, S.z - 0.5, 3, 3);
  } else if (S.mode === 'title') {
    // на титуле машина времени не идёт
  }

  // визуал мотоцикла
  const fallen = S.mode === 'crash';
  const targetRoll = fallen ? (S.x > 10 ? -1.35 : 1.35) : -S.lean;
  // питч: вилли задирает морду, тормоз клюёт носом
  const targetPitch = fallen ? 0.15 : (-S.wheelie + S.pitch * 0.4);

  bike.group.rotation.y = fallen ? S.yaw + (S.x > 10 ? -0.5 : 0.5) : S.yaw;
  bike.group.rotation.z = lerp(bike.group.rotation.z, targetRoll, Math.min(1, (fallen ? 9 : 8) * dt));
  bike.group.rotation.x = lerp(bike.group.rotation.x, targetPitch, Math.min(1, 9 * dt));

  if (fallen) {
    bike.group.position.set(S.x, -0.28, S.z);
  } else {
    // точка вращения — пятно контакта заднего колеса (вилли вокруг него)
    _eul.set(bike.group.rotation.x, bike.group.rotation.y, bike.group.rotation.z);
    _quat.setFromEuler(_eul);
    _raW.copy(RA_LOCAL).applyQuaternion(_quat);
    bike.group.position.set(S.x - _raW.x, -_raW.y + 0.33, S.z - 0.68 - _raW.z);
  }

  bikeState.v = S.v; bikeState.steer = S.steer; bikeState.lean = S.lean;
  bikeState.steerVis = S.steer * 0.3 * (1 / (1 + S.v * 0.09)) * (S.wheelie > 0.15 ? 0.3 : 1);
  if (!fallen) bike.animate(dt, bikeState);
  else {
    // колёса останавливаются
    bike.animate(dt * 0.05, { v: 0, steer: 0, lean: 0, steerVis: 0 });
  }

  updateSparks(dt);
  world.update(S.x, S.z, dt);
  updateCamera(dt, t);
  placeTraffic();

  renderer.render(scene, camera);
}
requestAnimationFrame(frame);

// скрин/тест хуки
const params = new URLSearchParams(location.hash.slice(1));
window.__mkad = {
  S, start: startGame, restart, setSpeed: v => { S.v = v; }, setCam: m => { S.camMode = m; },
  setZ: z => { S.z = z; }, freeze: false, crashNow: () => crash(),
  bbox: () => {
    const b = new THREE.Box3().setFromObject(bike.group);
    return { min: b.min.toArray().map(v => +v.toFixed(2)), max: b.max.toArray().map(v => +v.toFixed(2)) };
  },
  spawnCustom: (kind, colorHex, lane, dz) => {
    let c = makeCar(lane);
    scene.remove(c.group);
    if (kind === 'truck') c = buildTruck(colorHex, T);
    else c = buildCar(kind, colorHex, T);
    c.lane = lane; c.x = LANES_X[lane]; c.targetX = c.x; c.z = S.z + dz;
    c.v = 22; c.baseV = 22; c.changing = false; c.blinkT = 0; c.counted = false;
    scene.add(c.group);
    c.group.position.set(c.x, 0, c.z);
    cars.push(c);
    return c;
  },
  project: (x, y, z) => {
    const v = new THREE.Vector3(x, y, z).project(camera);
    return [(v.x + 1) / 2 * innerWidth, (1 - v.y) / 2 * innerHeight, v.z < 1 ? 1 : 0];
  },
  headCamCheck: () => {
    bike.group.updateMatrixWorld();
    const h = bike.head.getWorldPosition(new THREE.Vector3());
    return {
      cam: camera.position.toArray().map(v => +v.toFixed(3)),
      head: h.toArray().map(v => +v.toFixed(3)),
      dist: +camera.position.distanceTo(h).toFixed(3),
      camMode: S.camMode,
    };
  },
  info: () => ({
    calls: renderer.info.render.calls, tris: renderer.info.render.triangles,
    cars: cars.length, oncoming: oncoming.length, mode: S.mode,
    v: S.v, x: S.x, z: S.z, dist: S.dist,
  }),
  spawnNear: () => {
    const c = makeCar(2); c.z = S.z + 40; c.v = 20; c.group.position.set(c.x, 0, c.z); cars.push(c);
  },
};
if (params.has('auto')) setTimeout(startGame, 250);
if (params.get('speed')) S.v = parseFloat(params.get('speed'));
if (params.has('cam')) S.camMode = parseInt(params.get('cam')) || 0;
