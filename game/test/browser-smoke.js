// Прогон игрового цикла без браузера: стабы canvas/DOM + 3 секунды симуляции.
// node test/browser-smoke.js
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const ROOT = path.join(__dirname, "..");

function makeCtx2D() {
  const noop = () => {};
  const grad = { addColorStop: noop };
  const ctx = {
    canvas: null,
    save: noop,
    restore: noop,
    beginPath: noop,
    closePath: noop,
    moveTo: noop,
    lineTo: noop,
    quadraticCurveTo: noop,
    bezierCurveTo: noop,
    arc: noop,
    ellipse: noop,
    rect: noop,
    fill: noop,
    stroke: noop,
    fillRect: noop,
    strokeRect: noop,
    clip: noop,
    translate: noop,
    rotate: noop,
    scale: noop,
    setTransform: noop,
    drawImage: noop,
    createLinearGradient: () => grad,
    createRadialGradient: () => grad,
    measureText: () => ({ width: 10 }),
    fillText: noop,
    globalAlpha: 1,
    fillStyle: "#000",
    strokeStyle: "#000",
    lineWidth: 1,
  };
  return ctx;
}

function makeElement(id) {
  const ctx2d = makeCtx2D();
  return {
    id,
    clientWidth: 1280,
    clientHeight: 720,
    width: 1280,
    height: 720,
    style: {},
    textContent: "",
    innerHTML: "",
    classList: { add() {}, remove() {}, contains: () => false },
    getContext: () => ctx2d,
    addEventListener() {},
    getBoundingClientRect: () => ({ left: 0, top: 0, width: 1280, height: 720 }),
  };
}

const elements = {};
const listeners = {};
let rafQueue = [];

const sandbox = {
  console,
  Math,
  performance: { now: () => Date.now() },
  devicePixelRatio: 2,
  document: {
    getElementById: (id) => (elements[id] = elements[id] || makeElement(id)),
  },
  window: null,
  requestAnimationFrame: (cb) => {
    rafQueue.push(cb);
    return rafQueue.length;
  },
};
sandbox.window = sandbox;
sandbox.window.addEventListener = (type, fn) => {
  (listeners[type] = listeners[type] || []).push(fn);
};
sandbox.addEventListener = sandbox.window.addEventListener;
sandbox.globalThis = sandbox;
vm.createContext(sandbox);

for (const f of ["terrain.js", "physics.js", "effects.js", "human.js", "render.js", "main.js"]) {
  vm.runInContext(fs.readFileSync(path.join(ROOT, "js", f), "utf8"), sandbox, { filename: f });
}

// прогоняем ~3 секунды кадров
let now = 0;
let errors = 0;
for (let frame = 0; frame < 200; frame++) {
  now += 16.7;
  const queue = rafQueue;
  rafQueue = [];
  if (!queue.length) {
    console.log("цикл остановился на кадре", frame);
    break;
  }
  try {
    for (const cb of queue) cb(now);
  } catch (e) {
    errors++;
    console.log("ОШИБКА на кадре", frame, e && e.stack ? e.stack.split("\n").slice(0, 4).join("\n") : e);
    break;
  }
}

const g = sandbox.game;
console.log("кадров отрисовано:", 200, "ошибок:", errors);
console.log("игрок:", g.player.rootX.toFixed(0), g.player.rootY.toFixed(0), "режим:", g.player.mode);
console.log("точек физики:", g.world.points.length, "связей:", g.world.links.length);
console.log("частиц:", g.effects.particles.length, "декалей:", g.effects.decals.length);

// имитация урона и пары секунд
g.player.damage(45, g.player.chest.x, g.player.chest.y, 1, -0.4);
for (let i = 0; i < 150; i++) {
  now += 16.7;
  const q = rafQueue;
  rafQueue = [];
  for (const cb of q) cb(now);
}
console.log("после урона: режим", g.player.mode, "hp", g.player.health.toFixed(0), "частиц", g.effects.particles.length);
