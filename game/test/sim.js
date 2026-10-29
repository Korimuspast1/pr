// Headless-проверка физики персонажа (node test/sim.js)
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const ctx = { console, Math, performance: { now: () => Date.now() } };
ctx.globalThis = ctx;
vm.createContext(ctx);
for (const f of ["terrain.js", "physics.js", "human.js"]) {
  vm.runInContext(fs.readFileSync(path.join(__dirname, "..", "js", f), "utf8"), ctx, { filename: f });
}

const terrain = new ctx.Terrain(1337);
const world = new ctx.Physics.World(terrain);
const human = new ctx.Human(world, 700);
const dt = 1 / 120;

function sim(sec, input) {
  for (let i = 0; i < Math.round(sec / dt); i++) {
    human.update(dt, input);
    world.step(dt);
  }
}
const maxStep = () => Math.max(...world.points.map((p) => Math.hypot(p.x - p.px, p.y - p.py))).toFixed(2);
const height = () => (terrain.groundY(human.rootX) - human.rootY).toFixed(1);

sim(1.5, {});
console.log("стойка: x смещ", (human.rootX - 700).toFixed(1), "высота", height(), "ноги", human.footL.state, human.footR.state);

const x0 = human.rootX;
sim(2, { right: true });
console.log("ходьба 2с: прошёл", (human.rootX - x0).toFixed(0), "px, высота", height(), "torso", human.torsoAngle.toFixed(2), "footY", human.footR.y.toFixed(1), "ground", terrain.groundY(human.footR.x).toFixed(1));

sim(0.5, { left: true });
console.log("ходьба влево: x", human.rootX.toFixed(0));

human.health = 100;
human.damage(30, human.chest.x, human.chest.y, 1, -0.3);
console.log("урон 30 -> режим", human.mode, "hp", human.health.toFixed(0));
sim(0.6, {});
console.log("рэгдолл 0.6с: таз", human.pelvis.x.toFixed(0), human.pelvis.y.toFixed(0), "земля", terrain.groundY(human.pelvis.x).toFixed(0), "макс шаг", maxStep());
sim(4, {});
console.log("после 4.6с: режим", human.mode, "высота", (terrain.groundY(human.pelvis.x) - human.pelvis.y).toFixed(1), "макс шаг", maxStep());

human.damage(200, human.chest.x, human.chest.y, 1, 0);
sim(3, {});
console.log("смерть: режим", human.mode, "лежит на земле y", human.chest.y.toFixed(0), "земля", terrain.groundY(human.chest.x).toFixed(0), "макс шаг", maxStep(), "высота таза", (terrain.groundY(human.pelvis.x) - human.pelvis.y).toFixed(0));
