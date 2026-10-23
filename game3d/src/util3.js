export const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
export const lerp = (a, b, t) => a + (b - a) * t;
export const TAU = Math.PI * 2;

export function rand(a = 1, b) {
  if (b === undefined) { b = a; a = 0; }
  return a + Math.random() * (b - a);
}
export const randInt = (a, b) => Math.floor(rand(a, b + 1));
export const pick = (arr) => arr[(Math.random() * arr.length) | 0];
export const chance = (p) => Math.random() < p;

export const v3 = (x = 0, y = 0, z = 0) => ({ x, y, z });
export const add = (a, b) => ({ x: a.x + b.x, y: a.y + b.y, z: a.z + b.z });
export const sub = (a, b) => ({ x: a.x - b.x, y: a.y - b.y, z: a.z - b.z });
export const mul = (a, s) => ({ x: a.x * s, y: a.y * s, z: a.z * s });
export const len = (a) => Math.hypot(a.x, a.y, a.z);
export const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y, a.z - b.z);

export function norm(a) {
  const l = Math.hypot(a.x, a.y, a.z) || 1;
  return { x: a.x / l, y: a.y / l, z: a.z / l };
}

export function dot(a, b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
export function cross(a, b) {
  return { x: a.y * b.z - a.z * b.y, y: a.z * b.x - a.x * b.z, z: a.x * b.y - a.y * b.x };
}

export function lerpV(a, b, t) {
  return { x: lerp(a.x, b.x, t), y: lerp(a.y, b.y, t), z: lerp(a.z, b.z, t) };
}

// closest point on segment [a,b] to p
export function closestOnSeg(p, a, b) {
  const abx = b.x - a.x, aby = b.y - a.y, abz = b.z - a.z;
  const l2 = abx * abx + aby * aby + abz * abz || 1e-9;
  let t = ((p.x - a.x) * abx + (p.y - a.y) * aby + (p.z - a.z) * abz) / l2;
  t = clamp(t, 0, 1);
  return { x: a.x + abx * t, y: a.y + aby * t, z: a.z + abz * t, t };
}

export function distToSegment(p, a, b) {
  const c = closestOnSeg(p, a, b);
  return Math.hypot(p.x - c.x, p.y - c.y, p.z - c.z);
}

// Ray vs segment: returns { hit, dist (perpendicular distance), rayT (distance
// along the ray), point (on the ray), segT (0..1 on the segment), segPoint }.
export function raySegment(origin, dir, a, b) {
  const u = sub(b, a);
  const v = dir;
  const aa = dot(u, u), bb = dot(u, v), cc = dot(v, v);
  const w = sub(origin, a);
  const dd = dot(u, w), ee = dot(v, w);
  const D = aa * cc - bb * bb;
  const project = (t) => ({ x: origin.x + v.x * t, y: origin.y + v.y * t, z: origin.z + v.z * t });
  const at = (s) => ({ x: a.x + u.x * s, y: a.y + u.y * s, z: a.z + u.z * s });
  const gap = (p, q) => Math.hypot(p.x - q.x, p.y - q.y, p.z - q.z);

  let sc = 0, tc = 0;
  if (aa < 1e-12) {
    sc = 0;
    tc = cc > 1e-12 ? -ee / cc : 0;
  } else if (Math.abs(D) < 1e-9 * aa * cc) {
    // parallel: the nearer endpoint wins
    let best = null;
    for (const s of [0, 1]) {
      const p = at(s);
      const t = Math.max(0, (bb * s - ee) / (cc || 1));
      const d = gap(p, project(t));
      if (!best || d < best.d) best = { s, t, d };
    }
    sc = best.s;
    tc = best.t;
  } else {
    tc = (aa * ee - bb * dd) / (bb * bb - aa * cc);
    if (tc < 0) tc = 0;
    sc = (dd + bb * tc) / aa;
    if (sc < 0) {
      sc = 0;
      tc = Math.max(0, -ee / cc);
    } else if (sc > 1) {
      sc = 1;
      tc = Math.max(0, (bb - ee) / cc);
    }
  }
  const segPoint = at(sc);
  const point = project(tc);
  return { hit: true, dist: gap(segPoint, point), rayT: tc, point, segT: sc, segPoint };
}

export function segAlignAngle(a, b, c, d) {
  // angle between segment ab and cd in radians (0 = parallel)
  const u = norm(sub(b, a));
  const v = norm(sub(d, c));
  return Math.acos(clamp(dot(u, v), -1, 1));
}

export function hash01(n) {
  const x = Math.sin(n * 127.1 + 311.7) * 43758.5453;
  return x - Math.floor(x);
}

// angle wrappers
export function angleDiff(a, b) {
  let d = (b - a + Math.PI) % TAU;
  if (d < 0) d += TAU;
  return d - Math.PI;
}
export function approachAngle(a, b, step) {
  return a + clamp(angleDiff(a, b), -step, step);
}
