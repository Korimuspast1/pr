// Pixel isometric projection and drawing primitives.
//
// One tile is 32x16 art pixels. Everything is drawn on integer coordinates and
// outlined with 1px dark lines, so the whole scene stays crisp when it is
// upscaled by a whole number with image smoothing turned off.

export const TW = 32;
export const TH = 16;
export const HW = TW / 2;
export const HH = TH / 2;

export const iso = (gx, gy, z = 0) => ({
  x: Math.round((gx - gy) * HW),
  y: Math.round((gx + gy) * HH - z),
});

export const isoX = (gx, gy) => Math.round((gx - gy) * HW);
export const isoY = (gx, gy, z = 0) => Math.round((gx + gy) * HH - z);

export function corners(x, y, w = 1, d = 1, z = 0) {
  return [iso(x, y, z), iso(x + w, y, z), iso(x + w, y + d, z), iso(x, y + d, z)];
}

export function poly(ctx, pts) {
  ctx.beginPath();
  ctx.moveTo(pts[0].x, pts[0].y);
  for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i].x, pts[i].y);
  ctx.closePath();
}

export function fill(ctx, pts, color) {
  poly(ctx, pts);
  ctx.fillStyle = color;
  ctx.fill();
}

/** 1px line without anti-aliasing. */
export function pxLine(ctx, x0, y0, x1, y1, color) {
  x0 = Math.round(x0); y0 = Math.round(y0);
  x1 = Math.round(x1); y1 = Math.round(y1);
  ctx.fillStyle = color;
  let dx = Math.abs(x1 - x0);
  let dy = Math.abs(y1 - y0);
  const sx = x0 < x1 ? 1 : -1;
  const sy = y0 < y1 ? 1 : -1;
  let err = dx - dy;
  for (;;) {
    ctx.fillRect(x0, y0, 1, 1);
    if (x0 === x1 && y0 === y1) break;
    const e2 = 2 * err;
    if (e2 > -dy) { err -= dy; x0 += sx; }
    if (e2 < dx) { err += dx; y0 += sy; }
  }
}

export function outline(ctx, pts, color) {
  for (let i = 0; i < pts.length; i++) {
    const a = pts[i];
    const b = pts[(i + 1) % pts.length];
    pxLine(ctx, a.x, a.y, b.x, b.y, color);
  }
}

export function diamond(ctx, x, y, w, d, color) {
  fill(ctx, corners(x, y, w, d), color);
}

export function rect(ctx, x, y, w, h, color) {
  ctx.fillStyle = color;
  ctx.fillRect(Math.round(x), Math.round(y), Math.round(w), Math.round(h));
}

/**
 * Isometric box with a flat top: the workhorse for buildings.
 * z0/z1 are heights in art pixels above the ground plane.
 */
export function box(ctx, x, y, w, d, z0, z1, colors) {
  const b0 = corners(x, y, w, d, z0);
  const b1 = corners(x, y, w, d, z1);
  const [n0, e0, s0, w0] = b0;
  const [n1, e1, s1, w1] = b1;

  // left face (+y side, darker)
  fill(ctx, [w0, s0, s1, w1], colors.left);
  // right face (+x side)
  fill(ctx, [s0, e0, e1, s1], colors.right);
  // top
  fill(ctx, [n1, e1, s1, w1], colors.top);

  if (colors.outline) {
    outline(ctx, [n1, e1, s1, w1], colors.outline);
    pxLine(ctx, w0.x, w0.y, s0.x, s0.y, colors.outline);
    pxLine(ctx, s0.x, s0.y, e0.x, e0.y, colors.outline);
    pxLine(ctx, w0.x, w0.y, w1.x, w1.y, colors.outline);
    pxLine(ctx, e0.x, e0.y, e1.x, e1.y, colors.outline);
  }
  return b1;
}

/** Point on a vertical wall. side 'x' faces +x, 'y' faces +y; u in [0..1]. */
export function wallPoint(x, y, w, d, side, u, v, z0 = 0) {
  if (side === 'x') return iso(x + w, y + u * d, z0 + v);
  return iso(x + w - u * w, y + d, z0 + v);
}

/** Rectangle on a wall: u0..u1 along the wall, v0..v1 in height pixels. */
export function wallRect(ctx, x, y, w, d, side, u0, u1, v0, v1, z0, color) {
  const pts = [
    wallPoint(x, y, w, d, side, u0, v0, z0),
    wallPoint(x, y, w, d, side, u1, v0, z0),
    wallPoint(x, y, w, d, side, u1, v1, z0),
    wallPoint(x, y, w, d, side, u0, v1, z0),
  ];
  fill(ctx, pts, color);
  return pts;
}

/** A pitched roof over a w x d footprint: ridge runs along the y axis. */
export function pitchedRoof(ctx, x, y, w, d, z0, rise, colors) {
  const cx = x + w / 2;
  const r0 = iso(cx, y, z0 + rise);
  const r1 = iso(cx, y + d, z0 + rise);
  const a0 = iso(x + w, y, z0);
  const a1 = iso(x + w, y + d, z0);
  const b0 = iso(x, y, z0);
  const b1 = iso(x, y + d, z0);
  // right slope (facing +x)
  fill(ctx, [r0, a0, a1, r1], colors.right);
  // gable ends
  fill(ctx, [iso(x, y, z0), iso(x + w, y, z0), r0], colors.gableBack);
  fill(ctx, [iso(x, y + d, z0), iso(x + w, y + d, z0), r1], colors.gableFront);
  // left slope
  fill(ctx, [r0, b0, b1, r1], colors.left);
  if (colors.outline) {
    pxLine(ctx, r0.x, r0.y, a0.x, a0.y, colors.outline);
    pxLine(ctx, r0.x, r0.y, b0.x, b0.y, colors.outline);
    pxLine(ctx, r1.x, r1.y, a1.x, a1.y, colors.outline);
    pxLine(ctx, r1.x, r1.y, b1.x, b1.y, colors.outline);
    pxLine(ctx, a0.x, a0.y, a1.x, a1.y, colors.outline);
    pxLine(ctx, b0.x, b0.y, b1.x, b1.y, colors.outline);
  }
}
