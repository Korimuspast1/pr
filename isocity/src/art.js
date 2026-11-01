// Pixel art. Everything here paints into world-pixel coordinates with integer
// positions and 1px outlines; the renderer only upscales the finished frame.

import { TW, TH, HW, HH, iso, corners, fill, box, diamond, pxLine, pitchedRoof, wallRect, outline } from './iso.js?v=2026-11-01-pixel-3';
import { T } from './world.js?v=2026-11-01-pixel-3';

export const PAL = {
  grass: ['#79ae47', '#7cb24a', '#76a845'],
  grassEdge: '#5c8a36',
  grassTuft: '#8fc45c',
  road: '#4c5159',
  roadEdge: '#3c4047',
  roadLine: '#cfd3d9',
  roadDash: '#8d939c',
  curb: '#9aa0a8',
  zoneR: 'rgba(120, 200, 110, 0.20)',
  zoneC: 'rgba(110, 165, 235, 0.20)',
  zoneI: 'rgba(235, 200, 110, 0.20)',
  zoneREdge: '#5fae58',
  zoneCEdge: '#5b93d8',
  zoneIEdge: '#cba64c',
  water: ['#3f86b8', '#387aa9', '#4692c4'],
  waterLine: '#a9dcf0',
  dirt: '#b08a55',
  outline: '#2b2f38',
  glass: '#9fc4d6',
  glassDark: '#7ea3b6',
  warm: '#ffd37a',
};

const HOUSE = [
  { wall: '#e8d8b8', roof: '#b5533f', trim: '#f6edd8', door: '#7b5136' },
  { wall: '#e2d2b6', roof: '#4f6a86', trim: '#f2e8d4', door: '#5b4a3a' },
  { wall: '#ecdcbe', roof: '#8a5a3c', trim: '#f8f0de', door: '#6b4a33' },
  { wall: '#dfd0b4', roof: '#5d7a63', trim: '#f0e7d3', door: '#71543c' },
];

const SHOP = { wall: '#f1e6cf', roof: '#3f6f6a', awning: ['#2f8f83', '#c0563c'], trim: '#fbf4e4', door: '#3f6f6a' };
const FACTORY = { wall: '#b5764a', wall2: '#9c6239', roof: '#6b6157', trim: '#d9b98e', door: '#5a4534' };
const HALL = { wall: '#e9e2ce', roof: '#48566b', trim: '#fbf6ea', door: '#6b4a36' };

const hash = (x, y) => {
  let h = (Math.imul(x | 0, 374761393) + Math.imul(y | 0, 668265263)) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return (h ^ (h >>> 16)) >>> 0;
};

/** Chunky pixel ellipse with three shades, used for tree crowns and bushes. */
function blob(ctx, cx, cy, rx, ry, dark, mid, light) {
  // solid dark silhouette
  for (let dy = -ry; dy <= ry; dy++) {
    const k = Math.sqrt(Math.max(0, 1 - (dy / (ry + 0.6)) ** 2));
    const hw = Math.max(1, Math.round(rx * k));
    ctx.fillStyle = dark;
    ctx.fillRect(cx - hw, cy + dy, hw * 2, 1);
  }
  // lighter core, shifted up-left
  for (let dy = -ry + 1; dy <= ry - 1; dy++) {
    const k = Math.sqrt(Math.max(0, 1 - (dy / (ry + 0.6)) ** 2));
    const hw = Math.max(0, Math.round(rx * k) - 1);
    if (!hw) continue;
    ctx.fillStyle = dy > 0 ? mid : light;
    ctx.fillRect(cx - hw, cy + dy - 1, hw * 2, 1);
  }
}

// --------------------------------------------------------------------- ground
export function drawGroundTile(ctx, world, x, y, ox, oy) {
  const t = world.get(x, y);
  const c = corners(x, y);
  const p = (i) => ({ x: c[i].x + ox, y: c[i].y + oy });
  const [n, e, s, w] = [p(0), p(1), p(2), p(3)];
  const h = hash(x, y);

  if (t === T.ROAD) {
    fill(ctx, [n, e, s, w], PAL.road);
    // subtle asphalt speckle
    if (h % 7 === 0) {
      ctx.fillStyle = '#555b64';
      ctx.fillRect(n.x + (h % 9), n.y + 6 + (h % 3), 2, 1);
    }
    drawRoadMarks(ctx, world, x, y, ox, oy);
    return;
  }

  if (t === T.WATER) {
    fill(ctx, [n, e, s, w], PAL.water[(x + y) % 3]);
    ctx.fillStyle = PAL.waterLine;
    ctx.fillRect(w.x + 6 + ((x * 3) % 8), w.y + 6, 5, 1);
    ctx.fillRect(n.x + 2, n.y + 9, 4, 1);
    return;
  }

  if (t === T.PARK) {
    fill(ctx, [n, e, s, w], PAL.grass[2]);
    ctx.fillStyle = '#8ac35a';
    ctx.fillRect(n.x + 8, n.y + 6, 6, 2);
    ctx.fillStyle = '#6ba03f';
    ctx.fillRect(w.x + 3, w.y + 8, 5, 1);
    return;
  }

  // grass base with zone tint and dashed border
  const base = PAL.grass[(h >>> 3) % PAL.grass.length];
  fill(ctx, [n, e, s, w], base);
  const detail = h % 13;
  const px = n.x + 3 + ((h >>> 4) % 14);
  const py = n.y + 6 + ((h >>> 9) % 5);
  if (detail === 0) {
    ctx.fillStyle = PAL.grassEdge;
    ctx.fillRect(px, py, 2, 1);
    ctx.fillRect(px + 3, py + 1, 2, 1);
  } else if (detail === 3) {
    ctx.fillStyle = PAL.grassTuft;
    ctx.fillRect(px, py, 1, 2);
    ctx.fillRect(px + 2, py + 1, 1, 2);
  } else if (detail === 7) {
    ctx.fillStyle = '#6d9a3f';
    ctx.fillRect(w.x + 5, w.y + 8, 3, 1);
  }

  if (t === T.ZONE_R || t === T.ZONE_C || t === T.ZONE_I) {
    const tint = t === T.ZONE_R ? PAL.zoneR : t === T.ZONE_C ? PAL.zoneC : PAL.zoneI;
    const edge = t === T.ZONE_R ? PAL.zoneREdge : t === T.ZONE_C ? PAL.zoneCEdge : PAL.zoneIEdge;
    fill(ctx, [n, e, s, w], tint);
    // dashed outline only on the outer border of the zone
    const dashed = (a, b) => {
      const steps = 8;
      for (let i = 0; i < steps; i += 2) {
        const t0 = i / steps;
        const t1 = (i + 1) / steps;
        pxLine(ctx, a.x + (b.x - a.x) * t0, a.y + (b.y - a.y) * t0, a.x + (b.x - a.x) * t1, a.y + (b.y - a.y) * t1, edge);
      }
    };
    if (world.get(x, y - 1) !== t) dashed(n, e);
    if (world.get(x + 1, y) !== t) dashed(e, s);
    if (world.get(x, y + 1) !== t) dashed(s, w);
    if (world.get(x - 1, y) !== t) dashed(w, n);
  }
}

function drawRoadMarks(ctx, world, x, y, ox, oy) {
  const r = world.roadMask(x, y);
  const C = (gx, gy) => {
    const p = iso(gx, gy);
    return { x: p.x + ox, y: p.y + oy };
  };
  // curbs along the edges that touch non-road land
  const curb = (a, b) => pxLine(ctx, a.x, a.y, b.x, b.y, PAL.curb);
  if (!(r & 1)) curb(C(x, y), C(x + 1, y));
  if (!(r & 2)) curb(C(x + 1, y), C(x + 1, y + 1));
  if (!(r & 4)) curb(C(x + 1, y + 1), C(x, y + 1));
  if (!(r & 8)) curb(C(x, y + 1), C(x, y));

  const vertical = r & 1 || r & 4;
  const horizontal = r & 2 || r & 8;
  if (vertical && !horizontal) {
    pxLine(ctx, C(x + 0.5, y + 0.22).x, C(x + 0.5, y + 0.22).y, C(x + 0.5, y + 0.78).x, C(x + 0.5, y + 0.78).y, PAL.roadDash);
  } else if (horizontal && !vertical) {
    pxLine(ctx, C(x + 0.22, y + 0.5).x, C(x + 0.22, y + 0.5).y, C(x + 0.78, y + 0.5).x, C(x + 0.78, y + 0.5).y, PAL.roadDash);
  } else if (vertical && horizontal) {
    // crossing: little zebra stripes
    for (let i = 0; i < 3; i++) {
      const a = C(x + 0.12 + i * 0.26, y + 0.35);
      const b = C(x + 0.12 + i * 0.26, y + 0.65);
      pxLine(ctx, a.x, a.y, b.x, b.y, 'rgba(220,224,230,0.65)');
    }
  }
}

// ------------------------------------------------------------------ buildings
export function drawBuilding(ctx, b, sim, night) {
  if (b.state === 'site') {
    drawSite(ctx, b, sim);
    return;
  }
  switch (b.kind) {
    case 'house':
      drawHouse(ctx, b, night);
      break;
    case 'shop':
      drawShop(ctx, b, night);
      break;
    case 'factory':
      drawFactory(ctx, b, night);
      break;
    case 'cityhall':
      drawHall(ctx, b, night);
      break;
    default:
      break;
  }
}

function shadow(ctx, b) {
  const c = corners(b.x + 0.06, b.y + 0.12, b.w, b.d);
  ctx.globalAlpha = 0.16;
  fill(ctx, c, '#1b2a17');
  ctx.globalAlpha = 1;
}

/** Feet of a wall in screen pixels: (u,v) on the side facing `side`. */
function wallFeet(b, side, u, v) {
  const x = b.x;
  const y = b.y;
  const w = b.w;
  const d = b.d;
  if (side === 'x') return iso(x + w, y + u * d, v);
  return iso(x + w - u * w, y + d, v);
}

function windowGrid(ctx, b, side, rows, cols, v0, step, night, warm) {
  for (let r = 0; r < rows; r++) {
    for (let i = 0; i < cols; i++) {
      const u = (i + 1) / (cols + 1);
      const v = v0 + r * step;
      const lit = night > 0.35 && hash(b.id * 7 + i, r + b.variant) % 5 > 1;
      wallRect(ctx, b.x, b.y, b.w, b.d, side, u - 0.055, u + 0.055, v, v + 5, 0, lit ? PAL.warm : side === 'x' ? PAL.glass : PAL.glassDark);
      wallRect(ctx, b.x, b.y, b.w, b.d, side, u - 0.07, u + 0.07, v - 1, v, 0, '#6b5a48');
      if (lit) {
        const p = wallFeet(b, side, u, v + 2);
        ctx.globalAlpha = 0.16;
        ctx.fillStyle = PAL.warm;
        ctx.fillRect(p.x - 4, p.y - 2, 8, 7);
        ctx.globalAlpha = 1;
      }
    }
  }
}

function roofOutline(ctx, b, z, inset = 0.05) {
  const c = corners(b.x + inset, b.y + inset, b.w - inset * 2, b.d - inset * 2, z);
  fill(ctx, c, '#f0e8d6');
  ctx.globalAlpha = 0.25;
  fill(ctx, c, '#000000');
  ctx.globalAlpha = 1;
}

function drawHouse(ctx, b, night) {
  const pal = HOUSE[b.variant % HOUSE.length];
  const { x, y } = b;
  const h = 17 + (b.variant % 2) * 4;
  shadow(ctx, b);
  box(ctx, x, y, b.w, b.d, 0, h, { top: pal.wall, left: shadeTone(pal.wall, -0.16), right: shadeTone(pal.wall, -0.28), outline: PAL.outline });
  windowGrid(ctx, b, 'x', 1, 2, 5, 8, night);
  wallRect(ctx, x, y, b.w, b.d, b.doorSide, 0.4, 0.62, 0, 9, 0, pal.door);
  wallRect(ctx, x, y, b.w, b.d, b.doorSide, 0.38, 0.64, 9, 10, 0, pal.trim);
  pitchedRoof(ctx, x, y, b.w, b.d, h, 12 + (b.variant % 2) * 3, {
    left: pal.roof,
    right: shadeTone(pal.roof, 0.12),
    gableBack: shadeTone(pal.roof, -0.3),
    gableFront: shadeTone(pal.roof, -0.12),
    outline: PAL.outline,
  });
  if (b.variant % 2 === 0) chimney(ctx, x + b.w * 0.68, y + b.d * 0.15, h, pal.roof);
}

function drawShop(ctx, b, night) {
  const { x, y } = b;
  const h = 15;
  shadow(ctx, b);
  box(ctx, x, y, b.w, b.d, 0, h, { top: SHOP.wall, left: shadeTone(SHOP.wall, -0.14), right: shadeTone(SHOP.wall, -0.26), outline: PAL.outline });
  const side = b.doorSide;
  // shop window and door
  wallRect(ctx, x, y, b.w, b.d, side, 0.12, 0.88, 3, 11, 0, night > 0.35 ? '#ffe3a8' : PAL.glass);
  for (let i = 1; i < 4; i++) wallRect(ctx, x, y, b.w, b.d, side, i / 4 - 0.012, i / 4 + 0.012, 3, 11, 0, '#6f5b46');
  wallRect(ctx, x, y, b.w, b.d, side, 0.12, 0.88, 10, 11, 0, '#6f5b46');
  // striped awning
  const aw = SHOP.awning[b.variant % 2];
  for (let i = 0; i < 6; i++) {
    const u0 = 0.08 + i * 0.14;
    const p0 = wallFeet(b, side, u0, 13);
    const p1 = wallFeet(b, side, u0 + 0.14, 13);
    const q0 = { x: p0.x, y: p0.y + 5 };
    const q1 = { x: p1.x, y: p1.y + 5 };
    fill(ctx, [p0, p1, q1, q0], i % 2 ? aw : '#f4ecdc');
  }
  // sign
  const sp = wallFeet(b, side, 0.5, h - 1);
  ctx.fillStyle = '#2f3a3a';
  ctx.fillRect(sp.x - 7, sp.y - 6, 14, 5);
  ctx.fillStyle = '#e8d9a8';
  ctx.fillRect(sp.x - 5, sp.y - 4, 10, 1);
  pitchedRoof(ctx, x, y, b.w, b.d, h, 8, {
    left: SHOP.roof,
    right: shadeTone(SHOP.roof, 0.14),
    gableBack: shadeTone(SHOP.roof, -0.3),
    gableFront: shadeTone(SHOP.roof, -0.12),
    outline: PAL.outline,
  });
}

function drawFactory(ctx, b, night) {
  const { x, y, w, d } = b;
  const h = 20;
  shadow(ctx, b);
  box(ctx, x, y, w, d, 0, h, { top: FACTORY.wall, left: shadeTone(FACTORY.wall2, -0.12), right: shadeTone(FACTORY.wall, -0.22), outline: PAL.outline });
  for (let v = 4; v < h; v += 5) {
    pxLine(ctx, iso(x, y + d, v).x, iso(x, y + d, v).y, iso(x + w, y + d, v).x, iso(x + w, y + d, v).y, shadeTone(FACTORY.wall2, -0.24));
    pxLine(ctx, iso(x + w, y, v).x, iso(x + w, y, v).y, iso(x + w, y + d, v).x, iso(x + w, y + d, v).y, shadeTone(FACTORY.wall, -0.34));
  }
  windowGrid(ctx, b, 'x', 1, 3, 5, 9, night);
  // roller door
  wallRect(ctx, x, y, w, d, b.doorSide, 0.3, 0.7, 0, 11, 0, FACTORY.door);
  for (let v = 2; v < 11; v += 3) {
    wallRect(ctx, x, y, w, d, b.doorSide, 0.3, 0.7, v, v + 1, 0, shadeTone(FACTORY.door, 0.18));
  }
  // flat roof: outline first, then the actual roof, then vents
  roofOutline(ctx, b, h, 0.02);
  const r = corners(x + 0.05, y + 0.05, w - 0.1, d - 0.1, h);
  fill(ctx, r, shadeTone(FACTORY.roof, 0.08));
  box(ctx, x + 0.4, y + 0.3, 0.45, 0.45, h, h + 5, { top: '#8b8378', left: '#6f6a60', right: '#5d584f', outline: PAL.outline });
  chimney(ctx, x + w - 0.35, y + 0.25, h, FACTORY.roof, true);
}

function drawHall(ctx, b, night) {
  const { x, y, w, d } = b;
  const h = 24;
  shadow(ctx, b);
  box(ctx, x, y, w, d, 0, h, { top: HALL.wall, left: shadeTone(HALL.wall, -0.13), right: shadeTone(HALL.wall, -0.25), outline: PAL.outline });
  // windows, then columns in front of them
  windowGrid(ctx, b, 'x', 1, 4, 7, 8, night);
  windowGrid(ctx, b, 'y', 1, 4, 7, 8, night);
  for (let i = 0; i < 5; i++) {
    const u = 0.1 + i * 0.2;
    wallRect(ctx, x, y, w, d, 'x', u - 0.022, u + 0.022, 1, h - 3, 0, HALL.trim);
    wallRect(ctx, x, y, w, d, 'y', u - 0.022, u + 0.022, 1, h - 3, 0, shadeTone(HALL.trim, -0.12));
  }
  wallRect(ctx, x, y, w, d, 'x', 0.42, 0.58, 0, 11, 0, HALL.door);
  wallRect(ctx, x, y, w, d, 'x', 0.34, 0.66, 0, 2, 0, '#d9d2be');
  wallRect(ctx, x, y, w, d, 'x', 0, 1, h - 3, h, 0, HALL.trim);
  wallRect(ctx, x, y, w, d, 'y', 0, 1, h - 3, h, 0, shadeTone(HALL.trim, -0.1));

  // slate roof with white gable and a clock on the street side
  pitchedRoof(ctx, x, y, w, d, h, 13, {
    left: HALL.roof,
    right: shadeTone(HALL.roof, 0.16),
    gableBack: shadeTone(HALL.roof, -0.28),
    gableFront: HALL.trim,
    outline: PAL.outline,
  });
  const gp = iso(x + w * 0.5, y + d, h + 5);
  ctx.fillStyle = '#f4ecd8';
  ctx.fillRect(gp.x - 3, gp.y - 3, 6, 6);
  ctx.fillStyle = '#3a3f48';
  ctx.fillRect(gp.x - 1, gp.y - 2, 1, 3);
  ctx.fillRect(gp.x, gp.y, 1, 1);
  // flag on the ridge
  const fp = iso(x + w * 0.5, y + d * 0.5, h + 13);
  pxLine(ctx, fp.x, fp.y, fp.x, fp.y - 14, '#c8ccd4');
  fill(ctx, [{ x: fp.x + 1, y: fp.y - 14 }, { x: fp.x + 10, y: fp.y - 11 }, { x: fp.x + 10, y: fp.y - 6 }, { x: fp.x + 1, y: fp.y - 9 }], '#d8574a');
}

function chimney(ctx, cx, cy, h, roofColor, tall = false) {
  const top = h + (tall ? 16 : 9);
  box(ctx, cx, cy, 0.2, 0.2, h, top, {
    top: '#8d8377',
    left: shadeTone(roofColor, -0.2),
    right: shadeTone(roofColor, -0.4),
    outline: PAL.outline,
  });
}

function drawSite(ctx, b, sim) {
  const { x, y, w, d } = b;
  // dirt lot
  const c = corners(x + 0.04, y + 0.04, w - 0.08, d - 0.08);
  fill(ctx, c, PAL.dirt);
  ctx.fillStyle = '#9c7a4a';
  ctx.fillRect(iso(x + 0.3, y + 0.3).x, iso(x + 0.3, y + 0.3).y, 4, 2);
  // scaffolding: four posts plus rails
  const posts = [
    [x + 0.15, y + 0.15],
    [x + w - 0.15, y + 0.15],
    [x + 0.15, y + d - 0.15],
    [x + w - 0.15, y + d - 0.15],
  ];
  const top = 16;
  for (const [px, py] of posts) {
    const a = iso(px, py, 0);
    const bb = iso(px, py, top);
    pxLine(ctx, a.x, a.y, bb.x, bb.y, '#8a6a44');
  }
  const r1 = corners(x + 0.15, y + 0.15, w - 0.3, d - 0.3, top);
  for (let i = 0; i < 4; i++) {
    const a = r1[i];
    const bb = r1[(i + 1) % 4];
    pxLine(ctx, a.x, a.y, bb.x, bb.y, '#a07a4c');
  }
  const r2 = corners(x + 0.15, y + 0.15, w - 0.3, d - 0.3, 8);
  for (let i = 0; i < 4; i++) {
    const a = r2[i];
    const bb = r2[(i + 1) % 4];
    pxLine(ctx, a.x, a.y, bb.x, bb.y, '#8a6a44');
  }
  // crane
  const bx = iso(x + w - 0.2, y + 0.2, 0);
  const tx = iso(x + w - 0.2, y + 0.2, 30);
  pxLine(ctx, bx.x, bx.y, tx.x, tx.y, '#c8a24a');
  const armEnd = iso(x + 0.1, y + 0.1, 32);
  pxLine(ctx, tx.x, tx.y, armEnd.x, armEnd.y, '#c8a24a');
  pxLine(ctx, armEnd.x, armEnd.y, armEnd.x, armEnd.y + 10, '#8a7a4a');
  // progress bar
  const site = b.site;
  if (site) {
    const pct = Math.min(1, site.work / site.progressNeeded);
    const bar = iso(x + 0.5, y + 0.5, 2);
    ctx.fillStyle = '#3a3f48';
    ctx.fillRect(bar.x - 10, bar.y - 2, 20, 3);
    ctx.fillStyle = '#8fd08a';
    ctx.fillRect(bar.x - 9, bar.y - 1, Math.round(18 * pct), 1);
  }
}

// --------------------------------------------------------------------- props
export function drawProp(ctx, p, sim) {
  const g = iso(p.x, p.y, 0);
  switch (p.kind) {
    case 'tuft': {
      ctx.fillStyle = PAL.grassEdge;
      ctx.fillRect(g.x - 1, g.y - 1, 1, 2);
      ctx.fillRect(g.x + 1, g.y - 2, 1, 3);
      break;
    }
    case 'tree': {
      const sc = p.s || 1;
      const crownR = Math.round(9 * sc);
      const trunkH = Math.round(8 * sc);
      ctx.fillStyle = 'rgba(18,30,14,0.22)';
      ctx.fillRect(g.x - crownR + 1, g.y - 1, crownR * 2 - 2, 3);
      // trunk
      ctx.fillStyle = '#5a3d29';
      ctx.fillRect(g.x - 1, g.y - trunkH - 3, 3, trunkH + 3);
      ctx.fillStyle = '#7d5a3e';
      ctx.fillRect(g.x, g.y - trunkH - 3, 1, trunkH + 3);
      // crown: a big round mass with a lit top-left corner
      const cy = g.y - trunkH - crownR - 1;
      blob(ctx, g.x, cy, crownR, Math.round(crownR * 0.85), '#2f5326', '#4a7c36', '#6fae4c');
      blob(ctx, g.x - 2, cy - 1, Math.round(crownR * 0.7), Math.round(crownR * 0.6), '#3f6b2f', '#5d9342', '#86c861');
      ctx.fillStyle = '#96d06c';
      ctx.fillRect(g.x - Math.round(crownR * 0.55), cy - Math.round(crownR * 0.5), 3, 2);
      break;
    }
    case 'bench': {
      // pixel bench: seat, back, legs
      const flip = p.rot ? -1 : 1;
      const x = g.x;
      const y = g.y;
      ctx.fillStyle = '#8a6a44';
      ctx.fillRect(x - 7, y - 6, 13, 2);
      ctx.fillStyle = '#a8895c';
      ctx.fillRect(x - 7, y - 7, 13, 1);
      ctx.fillStyle = '#7a5c3a';
      ctx.fillRect(x - 7, y - 8, 13, 2);
      ctx.fillStyle = '#4c4034';
      ctx.fillRect(x - 6, y - 4, 1, 4);
      ctx.fillRect(x + 5 * flip, y - 4, 1, 4);
      ctx.fillStyle = 'rgba(20,26,20,0.18)';
      ctx.fillRect(x - 7, y, 13, 1);
      break;
    }
    case 'rock': {
      ctx.fillStyle = 'rgba(18,30,14,0.18)';
      ctx.fillRect(g.x - 4, g.y, 9, 2);
      ctx.fillStyle = '#6e747b';
      ctx.fillRect(g.x - 4, g.y - 2, 9, 3);
      ctx.fillStyle = '#8d939a';
      ctx.fillRect(g.x - 3, g.y - 4, 7, 3);
      ctx.fillStyle = '#b3b9c0';
      ctx.fillRect(g.x - 2, g.y - 4, 3, 2);
      break;
    }
    case 'bush': {
      ctx.fillStyle = 'rgba(18,30,14,0.18)';
      ctx.fillRect(g.x - 4, g.y, 9, 2);
      blob(ctx, g.x, g.y - 3, 4, 3, '#2f5326', '#4a7c36', '#6fae4c');
      break;
    }
    case 'flowers': {
      for (let i = 0; i < 4; i++) {
        ctx.fillStyle = ['#e2728f', '#e8c455', '#d7d2e0', '#e2728f'][i];
        ctx.fillRect(g.x - 4 + i * 3, g.y - 2 - (i % 2), 1, 1);
      }
      break;
    }
    case 'lamp': {
      ctx.fillStyle = '#3f4652';
      ctx.fillRect(g.x, g.y - 16, 1, 16);
      ctx.fillStyle = '#ffdf9e';
      ctx.fillRect(g.x - 1, g.y - 18, 3, 2);
      if (sim && sim.nightAmount > 0.25) {
        // pixel light pool under the lamp
        const lit = Math.min(1, sim.nightAmount);
        ctx.globalAlpha = 0.20 * lit;
        ctx.fillStyle = '#ffd27a';
        ctx.fillRect(g.x - 7, g.y - 6, 15, 8);
        ctx.fillRect(g.x - 4, g.y - 20, 9, 6);
        ctx.globalAlpha = 1;
      }
      break;
    }
    case 'fountain': {
      const c = corners(p.x + 0.15, p.y + 0.15, 0.7, 0.7);
      fill(ctx, c, '#cfc3ab');
      diamond(ctx, p.x + 0.28, p.y + 0.28, 0.44, 0.44, '#5fa8cd');
      ctx.fillStyle = '#bfe6f7';
      const t = sim ? Math.floor(sim.absMinute / 20) % 3 : 0;
      ctx.fillRect(g.x, g.y - 5 - t, 1, 4 + t);
      ctx.fillRect(g.x - 2 - t, g.y - 3, 1, 1);
      break;
    }
    default:
      break;
  }
}

// ------------------------------------------------------------------- people
export function drawCitizen(ctx, c) {
  const g = iso(c.x, c.y, 0);
  const walking = c.moving;
  const frame = walking ? (Math.floor(c.phase) % 2) : 0;
  const bob = walking ? frame : 0;

  ctx.fillStyle = 'rgba(18,24,16,0.28)';
  ctx.fillRect(g.x - 2, g.y, 5, 2);

  const y = g.y - bob;
  // legs
  ctx.fillStyle = '#3c4150';
  if (frame === 0) {
    ctx.fillRect(g.x - 2, y - 4, 1, 4);
    ctx.fillRect(g.x + 1, y - 4, 1, 4);
  } else {
    ctx.fillRect(g.x - 3, y - 4, 1, 4);
    ctx.fillRect(g.x + 2, y - 4, 1, 4);
  }
  // body
  ctx.fillStyle = c.color;
  ctx.fillRect(g.x - 2, y - 9, 4, 5);
  // arms
  ctx.fillStyle = shadeTone(c.color, -0.2);
  ctx.fillRect(g.x - 3, y - 8, 1, 3);
  ctx.fillRect(g.x + 2, y - 8, 1, 3);
  // head
  ctx.fillStyle = c.skin;
  ctx.fillRect(g.x - 1, y - 12, 3, 3);
  // hair
  ctx.fillStyle = '#463229';
  ctx.fillRect(g.x - 1, y - 13, 3, 1);
  ctx.fillRect(g.x - 1, y - 12, 1, 1);
  // mood pixel
  if (c.hunger > 1.5) {
    ctx.fillStyle = '#e0574d';
    ctx.fillRect(g.x + 3, y - 15, 1, 2);
    ctx.fillRect(g.x + 3, y - 12, 1, 1);
  }
}

export function drawTruck(ctx, t) {
  const g = iso(t.x, t.y, 0);
  const next = t.path[Math.min(t.index, t.path.length - 1)];
  const dx = next.x - t.x;
  const dy = next.y - t.y;
  const alongX = Math.abs(dx) >= Math.abs(dy);
  const dir = alongX ? (dx >= 0 ? 1 : -1) : dy >= 0 ? 1 : -1;

  ctx.fillStyle = 'rgba(18,24,16,0.25)';
  ctx.fillRect(g.x - 6, g.y, 13, 2);

  if (alongX) {
    const bodyX = dir > 0 ? g.x - 7 : g.x + 1;
    const cabX = dir > 0 ? g.x + 1 : g.x - 5;
    // wheels
    ctx.fillStyle = '#22262c';
    ctx.fillRect(bodyX + 1, g.y - 1, 3, 2);
    ctx.fillRect(bodyX + 6, g.y - 1, 3, 2);
    // cargo box
    ctx.fillStyle = '#d9d3c4';
    ctx.fillRect(bodyX, g.y - 8, 7, 7);
    ctx.fillStyle = '#efe9db';
    ctx.fillRect(bodyX, g.y - 8, 7, 1);
    ctx.fillStyle = '#b3ac9c';
    ctx.fillRect(bodyX, g.y - 2, 7, 1);
    // cab
    ctx.fillStyle = '#4f8fbf';
    ctx.fillRect(cabX, g.y - 6, 4, 5);
    ctx.fillStyle = '#9fd0e6';
    ctx.fillRect(cabX + (dir > 0 ? 1 : 0), g.y - 5, 2, 2);
    ctx.fillStyle = '#3a6f96';
    ctx.fillRect(cabX, g.y - 2, 4, 1);
  } else {
    ctx.fillStyle = '#22262c';
    ctx.fillRect(g.x - 5, g.y - 1, 3, 2);
    ctx.fillRect(g.x + 1, g.y - 1, 3, 2);
    ctx.fillStyle = '#d9d3c4';
    ctx.fillRect(g.x - 5, g.y - 9, 6, 8);
    ctx.fillStyle = '#efe9db';
    ctx.fillRect(g.x - 5, g.y - 9, 6, 1);
    ctx.fillStyle = '#b3ac9c';
    ctx.fillRect(g.x - 5, g.y - 2, 6, 1);
    ctx.fillStyle = '#4f8fbf';
    ctx.fillRect(g.x - 4, g.y - 14, 5, 5);
    ctx.fillStyle = '#9fd0e6';
    ctx.fillRect(g.x - 3, g.y - 13, 3, 2);
    ctx.fillStyle = '#3a6f96';
    ctx.fillRect(g.x - 4, g.y - 10, 5, 1);
  }
}

// ------------------------------------------------------------------- helper
export function shadeTone(hex, amount) {
  const h = hex.replace('#', '');
  const n = parseInt(h.length === 3 ? h.split('').map((c) => c + c).join('') : h, 16);
  const r = (n >> 16) & 255;
  const g = (n >> 8) & 255;
  const b = n & 255;
  const f = (v) => Math.max(0, Math.min(255, Math.round(amount > 0 ? v + (255 - v) * amount : v * (1 + amount))));
  return `rgb(${f(r)},${f(g)},${f(b)})`;
}

export { outline };
