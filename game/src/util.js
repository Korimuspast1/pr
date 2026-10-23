// Small math / helper library used across the game.
export const VIEW_W = 512;
export const VIEW_H = 288;
export const GROUND_Y = 238;

export const TAU = Math.PI * 2;

export const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
export const lerp = (a, b, t) => a + (b - a) * t;
export const invLerp = (a, b, v) => (v - a) / (b - a || 1);

export function rand(a = 1, b) {
  if (b === undefined) { b = a; a = 0; }
  return a + Math.random() * (b - a);
}
export const randInt = (a, b) => Math.floor(rand(a, b + 1));
export const pick = (arr) => arr[(Math.random() * arr.length) | 0];
export const chance = (p) => Math.random() < p;
export const sign = (v) => (v < 0 ? -1 : 1);

export function dist(x1, y1, x2, y2) {
  const dx = x2 - x1, dy = y2 - y1;
  return Math.hypot(dx, dy);
}

export function angleDiff(a, b) {
  let d = (b - a + Math.PI) % TAU;
  if (d < 0) d += TAU;
  return d - Math.PI;
}

export function approachAngle(a, b, maxStep) {
  const d = angleDiff(a, b);
  return a + clamp(d, -maxStep, maxStep);
}

export const easeOut = (t) => 1 - (1 - t) * (1 - t);
export const easeIn = (t) => t * t;

// Closest point on segment AB to point P.
export function closestOnSeg(px, py, ax, ay, bx, by) {
  const abx = bx - ax, aby = by - ay;
  const len2 = abx * abx + aby * aby || 1e-6;
  let t = ((px - ax) * abx + (py - ay) * aby) / len2;
  t = clamp(t, 0, 1);
  return { x: ax + abx * t, y: ay + aby * t, t };
}

export function segPointDist(px, py, ax, ay, bx, by) {
  const c = closestOnSeg(px, py, ax, ay, bx, by);
  return { d: Math.hypot(px - c.x, py - c.y), t: c.t, x: c.x, y: c.y };
}

// Closest approach between two segments. Returns {d, s, t, x, y} where s is the
// parameter along AB and t along CD.
export function segSegClosest(ax, ay, bx, by, cx, cy, dx, dy) {
  const ux = bx - ax, uy = by - ay;
  const vx = dx - cx, vy = dy - cy;
  const wx = ax - cx, wy = ay - cy;
  const a = ux * ux + uy * uy;
  const b = ux * vx + uy * vy;
  const c = vx * vx + vy * vy;
  const d = ux * wx + uy * wy;
  const e = vx * wx + vy * wy;
  const D = a * c - b * b;
  let sN, sD = D, tN, tD = D;

  if (D < 1e-6) {
    sN = 0; sD = 1; tN = e; tD = c || 1e-6;
  } else {
    sN = b * e - c * d;
    tN = a * e - b * d;
    if (sN < 0) { sN = 0; tN = e; tD = c || 1e-6; }
    else if (sN > sD) { sN = sD; tN = e + b; tD = c || 1e-6; }
  }
  if (tN < 0) { tN = 0; if (-d < 0) sN = 0; else if (-d > a) sN = sD; else { sN = -d; sD = a || 1e-6; } }
  else if (tN > tD) { tN = tD; if (-d + b < 0) sN = 0; else if (-d + b > a) sN = sD; else { sN = -d + b; sD = a || 1e-6; } }

  const s = clamp(sN / (sD || 1e-6), 0, 1);
  const t = clamp(tN / (tD || 1e-6), 0, 1);
  const px = ax + ux * s, py = ay + uy * s;
  const qx = cx + vx * t, qy = cy + vy * t;
  return { d: Math.hypot(px - qx, py - qy), s, t, x: (px + qx) / 2, y: (py + qy) / 2 };
}

// Deterministic pseudo random from an integer seed.
export function hash01(n) {
  let x = Math.sin(n * 127.1 + 311.7) * 43758.5453;
  return x - Math.floor(x);
}
