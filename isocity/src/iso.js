// Isometric projection + primitive drawing helpers.
//
// Grid coordinates are in "tiles": x grows to the lower-right, y grows to the
// lower-left. z is a height in screen pixels above the ground plane.

export const TW = 64; // tile width  (screen px)
export const TH = 32; // tile height (screen px)

export function iso(gx, gy, z = 0) {
  return { x: (gx - gy) * (TW / 2), y: (gx + gy) * (TH / 2) - z };
}

export function isoX(gx, gy) {
  return (gx - gy) * (TW / 2);
}

export function isoY(gx, gy, z = 0) {
  return (gx + gy) * (TH / 2) - z;
}

/** Corners of a rectangular footprint on the ground: north, east, south, west. */
export function corners(x, y, w = 1, d = 1, z = 0) {
  return [iso(x, y, z), iso(x + w, y, z), iso(x + w, y + d, z), iso(x, y + d, z)];
}

export function poly(ctx, pts) {
  ctx.beginPath();
  ctx.moveTo(pts[0].x, pts[0].y);
  for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i].x, pts[i].y);
  ctx.closePath();
}

export function fillPoly(ctx, pts, fill) {
  poly(ctx, pts);
  ctx.fillStyle = fill;
  ctx.fill();
}

export function strokePoly(ctx, pts, stroke, width = 1) {
  poly(ctx, pts);
  ctx.strokeStyle = stroke;
  ctx.lineWidth = width;
  ctx.stroke();
}

export function fillDiamond(ctx, x, y, w, d, fill) {
  fillPoly(ctx, corners(x, y, w, d), fill);
}

export function traceDiamond(ctx, x, y, w = 1, d = 1) {
  const c = corners(x, y, w, d);
  ctx.beginPath();
  ctx.moveTo(c[0].x, c[0].y);
  ctx.lineTo(c[1].x, c[1].y);
  ctx.lineTo(c[2].x, c[2].y);
  ctx.lineTo(c[3].x, c[3].y);
  ctx.closePath();
}

/**
 * Point on a vertical wall.
 * side 'x' is the wall facing +x (screen lower-right), 'y' faces +y (lower-left).
 * u runs along the wall [0..1], v is the height in px above the wall base.
 */
export function wallPoint(x, y, w, d, side, u, v, z0 = 0) {
  if (side === 'x') return iso(x + w, y + u * d, z0 + v);
  return iso(x + w - u * w, y + d, z0 + v);
}

/** Quad on a wall: u0..u1, v0..v1 (v in px). */
export function wallQuad(ctx, x, y, w, d, side, u0, u1, v0, v1, z0, fill) {
  const pts = [
    wallPoint(x, y, w, d, side, u0, v0, z0),
    wallPoint(x, y, w, d, side, u1, v0, z0),
    wallPoint(x, y, w, d, side, u1, v1, z0),
    wallPoint(x, y, w, d, side, u0, v1, z0),
  ];
  fillPoly(ctx, pts, fill);
  return pts;
}

/** Draws the three visible faces of a box. Returns screen corners of the top. */
export function box(ctx, x, y, w, d, z0, z1, col) {
  const c0 = corners(x, y, w, d, z0);
  const c1 = corners(x, y, w, d, z1);
  const [A1, B1, C1, D1] = c1;

  if (col.roofTop) {
    fillPoly(ctx, [A1, B1, C1, D1], col.roofTop);
  }
  // +x wall (screen right)
  fillPoly(ctx, [c0[1], c0[2], C1, B1], col.faceX);
  // +y wall (screen left)
  fillPoly(ctx, [c0[2], c0[3], D1, C1], col.faceY);
  return c1;
}

/** Simple drop shadow on the ground under a footprint. */
export function groundShadow(ctx, x, y, w, d, alpha = 0.18, spread = 0.06) {
  const c = corners(x - spread, y - spread, w + spread * 2, d + spread * 2);
  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.filter = 'blur(2px)';
  fillPoly(ctx, c, '#1b2430');
  ctx.restore();
}

/** Rounded isometric top face (used for awnings, roofs, tables). */
export function quadRounded(ctx, pts, r, fill) {
  poly(ctx, pts);
  ctx.fillStyle = fill;
  ctx.fill();
}
