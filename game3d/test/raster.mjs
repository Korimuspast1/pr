/* Software rasteriser for model inspection.
   Renders meshes from three.js scenes to PNG without WebGL, so models can be
   checked (and reviewed as images) inside Node.

   Usage: node test/render-models.mjs
*/
import * as THREE from '../vendor/three.module.js';
import { deflateSync } from 'node:zlib';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';

/* ------------------------------------------------------------------ */
/*  PNG encoder                                                        */
/* ------------------------------------------------------------------ */

const CRC_TABLE = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();

function crc32(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = CRC_TABLE[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function pngChunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length, 0);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body), 0);
  return Buffer.concat([len, body, crc]);
}

export function encodePNG(w, h, rgb) {
  const stride = w * 3;
  const raw = Buffer.alloc((stride + 1) * h);
  for (let y = 0; y < h; y++) {
    raw[y * (stride + 1)] = 0;
    rgb.copy ? rgb.copy(raw, y * (stride + 1) + 1, y * stride, y * stride + stride)
      : raw.set(rgb.subarray(y * stride, y * stride + stride), y * (stride + 1) + 1);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0);
  ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; ihdr[9] = 2; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
  return Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    pngChunk('IHDR', ihdr),
    pngChunk('IDAT', deflateSync(raw, { level: 9 })),
    pngChunk('IEND', Buffer.alloc(0)),
  ]);
}

/* ------------------------------------------------------------------ */
/*  Rasteriser                                                         */
/* ------------------------------------------------------------------ */

const srgb = (c) => (c <= 0.0031308 ? c * 12.92 : 1.055 * Math.pow(c, 1 / 2.4) - 0.055);

function collectTriangles(root) {
  const tris = [];
  root.updateMatrixWorld(true);
  root.traverse((o) => {
    if (!o.isMesh || o.isInstancedMesh || o.visible === false) return;
    let vis = true;
    let p = o.parent;
    while (p) { if (p.visible === false) vis = false; p = p.parent; }
    if (!vis) return;
    const geo = o.geometry;
    const pos = geo.getAttribute('position');
    if (!pos) return;
    const idx = geo.index;
    const mat = Array.isArray(o.material) ? o.material[0] : o.material;
    const color = mat && mat.color ? mat.color : new THREE.Color(0x999999);
    const emissive = mat && mat.emissive ? mat.emissive : new THREE.Color(0);
    const isMetal = mat && mat.metalness != null ? mat.metalness : 0;
    const rough = mat && mat.roughness != null ? mat.roughness : 0.8;
    const count = idx ? idx.count : pos.count;
    const a = new THREE.Vector3(), b = new THREE.Vector3(), c = new THREE.Vector3();
    for (let i = 0; i < count; i += 3) {
      const i0 = idx ? idx.getX(i) : i;
      const i1 = idx ? idx.getX(i + 1) : i + 1;
      const i2 = idx ? idx.getX(i + 2) : i + 2;
      a.fromBufferAttribute(pos, i0).applyMatrix4(o.matrixWorld);
      b.fromBufferAttribute(pos, i1).applyMatrix4(o.matrixWorld);
      c.fromBufferAttribute(pos, i2).applyMatrix4(o.matrixWorld);
      tris.push({
        a: a.clone(), b: b.clone(), c: c.clone(),
        color, emissive, isMetal, rough, cast: o.castShadow === true,
      });
    }
  });
  return tris;
}

/* Directional shadow map (orthographic, clamped to a box around the focus). */
export function buildShadowMap(tris, sun, focus, range, size) {
  const fwd = sun.clone().multiplyScalar(-1).normalize();
  const right = new THREE.Vector3().crossVectors(fwd, new THREE.Vector3(0, 1, 0));
  if (right.lengthSq() < 1e-8) right.set(1, 0, 0);
  right.normalize();
  const up = new THREE.Vector3().crossVectors(right, fwd).normalize();

  const depth = new Float32Array(size * size).fill(Infinity);
  const k = size / (range * 2);
  const half = size / 2;
  const verts = [];
  for (const t of tris) {
    for (const v of [t.a, t.b, t.c]) {
      const x = v.x - focus.x, y = v.y - focus.y, z = v.z - focus.z;
      let lx = x * right.x + y * right.y + z * right.z;
      let ly = x * up.x + y * up.y + z * up.z;
      const lz = x * fwd.x + y * fwd.y + z * fwd.z;
      lx = Math.max(-range, Math.min(range, lx));
      ly = Math.max(-range, Math.min(range, ly));
      verts.push(lx * k + half, half - ly * k, lz);
    }
  }

  const triCount = verts.length / 9;
  const gn = new THREE.Vector3(), ge1 = new THREE.Vector3(), ge2 = new THREE.Vector3();
  const sunDir = sun.clone().normalize();
  for (let i = 0; i < triCount; i++) {
    if (tris[i] && tris[i].cast === false) continue;
    // only front faces write depth: standard shadow-map culling
    const tt = tris[i];
    if (tt) {
      ge1.subVectors(tt.b, tt.a);
      ge2.subVectors(tt.c, tt.a);
      gn.crossVectors(ge1, ge2);
      if (gn.dot(sunDir) <= 0) continue;
    }
    const o = i * 9;
    const ax = verts[o], ay = verts[o + 1], az = verts[o + 2];
    const bx = verts[o + 3], by = verts[o + 4], bz = verts[o + 5];
    const cx = verts[o + 6], cy = verts[o + 7], cz = verts[o + 8];
    const minX = Math.max(0, Math.floor(Math.min(ax, bx, cx)));
    const maxX = Math.min(size - 1, Math.ceil(Math.max(ax, bx, cx)));
    const minY = Math.max(0, Math.floor(Math.min(ay, by, cy)));
    const maxY = Math.min(size - 1, Math.ceil(Math.max(ay, by, cy)));
    if (minX > maxX || minY > maxY) continue;
    const area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay);
    if (Math.abs(area) < 1e-9) continue;
    const inv = 1 / area;
    for (let y = minY; y <= maxY; y++) {
      for (let x = minX; x <= maxX; x++) {
        const px = x + 0.5, py = y + 0.5;
        const w0 = ((bx - px) * (cy - py) - (cx - px) * (by - py)) * inv;
        const w1 = ((cx - px) * (ay - py) - (ax - px) * (cy - py)) * inv;
        const w2 = 1 - w0 - w1;
        if (w0 < -0.0001 || w1 < -0.0001 || w2 < -0.0001) continue;
        const z = az * w0 + bz * w1 + cz * w2;
        const off = y * size + x;
        if (z < depth[off]) depth[off] = z;
      }
    }
  }
  return { depth, size, k, half, right, up, fwd };
}

/**
 * Renders a scene to an RGB buffer.
 * opts: { width, height, dir: [x,y,z] (camera -> object), up: [x,y,z],
 *         target: [x,y,z], scale: world units across the width, bg,
 *         light: [x,y,z], wireframe }
 */
export function render(scene, opts = {}) {
  const W = opts.width || 480;
  const H = opts.height || 360;
  const SS = opts.ss || 2;
  const w = W * SS, h = H * SS;
  const up = new THREE.Vector3(...(opts.up || [0, 1, 0])).normalize();
  const target = new THREE.Vector3(...(opts.target || [0, 0.9, 0]));
  const bg = opts.bg || [0.055, 0.065, 0.08];
  const exposure = opts.exposure != null ? opts.exposure : 1;
  const light = new THREE.Vector3(...(opts.light || [-0.5, 1, 0.6])).normalize();

  const persp = !!opts.eye;
  let eye, fwd, right, camUp, focal;
  if (persp) {
    eye = new THREE.Vector3(...opts.eye);
    fwd = target.clone().sub(eye).normalize();
    right = new THREE.Vector3().crossVectors(fwd, up).normalize();
    camUp = new THREE.Vector3().crossVectors(right, fwd).normalize();
    const fov = (opts.fov || 62) * Math.PI / 180;
    focal = (h / 2) / Math.tan(fov / 2);
  } else {
    const dir = new THREE.Vector3(...(opts.dir || [0, 0, 1])).normalize();
    right = new THREE.Vector3().crossVectors(up, dir).normalize();
    camUp = new THREE.Vector3().crossVectors(dir, right).normalize();
    eye = target.clone().addScaledVector(dir, -40);
    fwd = dir;
    focal = 0;
  }

  const worldW = opts.scale || 2.4;
  const worldH = worldW / (W / H);
  const sx = w / worldW, sy = h / worldH;
  const NEAR = 0.08;

  const sky = opts.sky; // [[r,g,b] top, [r,g,b] horizon] as sRGB 0..1
  const rgb = new Float32Array(w * h * 3);
  for (let y = 0; y < h; y++) {
    const t = y / (h - 1);
    let r, g, b;
    if (sky) {
      const e = Math.pow(t, 0.5);
      r = sky[0][0] + (sky[1][0] - sky[0][0]) * e;
      g = sky[0][1] + (sky[1][1] - sky[0][1]) * e;
      b = sky[0][2] + (sky[1][2] - sky[0][2]) * e;
    } else {
      r = bg[0] * (1.15 - t * 0.25);
      g = bg[1] * (1.15 - t * 0.25);
      b = bg[2] * (1.2 - t * 0.25);
    }
    for (let x = 0; x < w; x++) {
      const o = (y * w + x) * 3;
      rgb[o] = r * 255; rgb[o + 1] = g * 255; rgb[o + 2] = b * 255;
    }
  }
  const depth = new Float32Array(w * h).fill(Infinity);

  const tris = collectTriangles(scene);

  // ---- sun shadow map -------------------------------------------------
  const sun = new THREE.Vector3(...(opts.sun || opts.light || [-0.5, 1, 0.6])).normalize();
  const focus = new THREE.Vector3(...(opts.shadowFocus || opts.target || [0, 0, 0]));
  const sm = opts.shadows === false ? null
    : buildShadowMap(tris, sun, focus, opts.shadowRange || 46, opts.shadowSize || 1024);
  const eRel = sm ? new THREE.Vector3().subVectors(eye, focus) : null;
  const eR = sm ? eRel.dot(sm.right) : 0;
  const eU = sm ? eRel.dot(sm.up) : 0;
  const eF = sm ? eRel.dot(sm.fwd) : 0;
  const rR = sm ? right.dot(sm.right) : 0;
  const uR = sm ? camUp.dot(sm.right) : 0;
  const fR = sm ? fwd.dot(sm.right) : 0;
  const rU = sm ? right.dot(sm.up) : 0;
  const uU = sm ? camUp.dot(sm.up) : 0;
  const fU = sm ? fwd.dot(sm.up) : 0;
  const rF = sm ? right.dot(sm.fwd) : 0;
  const uF = sm ? camUp.dot(sm.fwd) : 0;
  const fF = sm ? fwd.dot(sm.fwd) : 0;
  const sdepth = sm ? sm.depth : null;
  const ssize = sm ? sm.size : 0;
  const sk = sm ? sm.k : 1;
  const shalf = sm ? sm.half : 0;
  const sTexel = sm ? (2 * (opts.shadowRange || 46)) / (ssize || 1) : 0;
  const sBias = opts.shadowBias != null ? opts.shadowBias : sTexel * 0.25 + 0.008;
  const sOffset = sTexel * 0.85;

  const fog = opts.fog || null;
  const fogC = fog ? new THREE.Color().setRGB(...(fog.color || [0.06, 0.075, 0.1])) : null;
  const fogD = fog ? (fog.density || 0.02) : 0;

  // PCF: a small disk of taps. R > 1 also hides depth-buffer quantisation.
  const R = opts.shadowSoft != null ? opts.shadowSoft : 2.2;
  const TAPS = [];
  const TAP_ROT = 0.4;
  for (let i = 0; i < 12; i++) {
    const a = (i / 12) * Math.PI * 2 + TAP_ROT;
    TAPS.push([Math.cos(a) * R, Math.sin(a) * R]);
  }
  TAPS.push([0, 0]);
  function litAt(sx, sy, az) {
    let sum = 0;
    for (let i = 0; i < TAPS.length; i++) {
      const xx = (sx + TAPS[i][0]) | 0;
      const yy = (sy + TAPS[i][1]) | 0;
      if (xx < 0 || xx >= ssize || yy < 0 || yy >= ssize) { sum += 1; continue; }
      sum += az <= sdepth[yy * ssize + xx] + sBias ? 1 : 0;
    }
    return sum / TAPS.length;
  }

  const camSpace = (v) => {
    const rel = v.clone().sub(eye);
    return { x: rel.dot(right), y: rel.dot(camUp), z: rel.dot(fwd) };
  };
  const toScreen = (c) => {
    if (persp) return { x: (c.x / c.z) * focal + w / 2, y: (-c.y / c.z) * focal + h / 2, z: c.z };
    return { x: c.x * sx + w / 2, y: -c.y * sy + h / 2, z: c.z };
  };

  const n = new THREE.Vector3();
  const e1 = new THREE.Vector3(), e2 = new THREE.Vector3();
  const lightDir = light.clone();

  const fill = (A, B, C, t) => {
    const minX = Math.max(0, Math.floor(Math.min(A.x, B.x, C.x)));
    const maxX = Math.min(w - 1, Math.ceil(Math.max(A.x, B.x, C.x)));
    const minY = Math.max(0, Math.floor(Math.min(A.y, B.y, C.y)));
    const maxY = Math.min(h - 1, Math.ceil(Math.max(A.y, B.y, C.y)));
    if (minX > maxX || minY > maxY) return;

    e1.subVectors(t.b, t.a);
    e2.subVectors(t.c, t.a);
    n.crossVectors(e1, e2).normalize();
    let lam = n.dot(lightDir);
    if (lam < 0) lam = -lam * 0.5;
    const spec = Math.pow(Math.max(0, n.dot(lightDir)), 18) * (0.3 - t.rough * 0.25) * (0.4 + t.isMetal);
    const shadeA = 0.20 + lam * 0.86;
    const area = (B.x - A.x) * (C.y - A.y) - (C.x - A.x) * (B.y - A.y);
    if (Math.abs(area) < 1e-9) return;
    const inv = 1 / area;
    const cr0 = t.color.r, cg0 = t.color.g, cb0 = t.color.b;
    // 1/z is affine in screen space, so depth must be interpolated that way
    const izA = persp ? 1 / A.z : 0, izB = persp ? 1 / B.z : 0, izC = persp ? 1 / C.z : 0;
    const er = t.emissive.r, eg = t.emissive.g, eb = t.emissive.b;
    for (let y = minY; y <= maxY; y++) {
      for (let x = minX; x <= maxX; x++) {
        const px = x + 0.5, py = y + 0.5;
        const w0 = ((B.x - px) * (C.y - py) - (C.x - px) * (B.y - py)) * inv;
        const w1 = ((C.x - px) * (A.y - py) - (A.x - px) * (C.y - py)) * inv;
        const w2 = 1 - w0 - w1;
        if (w0 < 0 || w1 < 0 || w2 < 0) continue;
        const z = persp ? 1 / (izA * w0 + izB * w1 + izC * w2) : A.z * w0 + B.z * w1 + C.z * w2;
        const off = y * w + x;
        if (z >= depth[off]) continue;
        depth[off] = z;

        let lit = 1;
        if (sm && lam <= 0.02) { lit = 1; }
        else if (sm) {
          let cx, cy, cz;
          if (persp) {
            cz = z;
            cx = ((px - w / 2) / focal) * cz;
            cy = (-(py - h / 2) / focal) * cz;
          } else {
            cx = (px - w / 2) / sx;
            cy = -(py - h / 2) / sy;
            cz = z;
          }
          const nx = n.x * sOffset, ny = n.y * sOffset, nz = n.z * sOffset;
          const ax = eR + rR * cx + uR * cy + fR * cz + right.x * nx + camUp.x * ny + fwd.x * nz;
          const ay = eU + rU * cx + uU * cy + fU * cz + right.y * nx + camUp.y * ny + fwd.y * nz;
          const az = eF + rF * cx + uF * cy + fF * cz + right.z * nx + camUp.z * ny + fwd.z * nz;
          lit = litAt(ax * sk + shalf, shalf - ay * sk, az);

        }
        const shade = shadeA * (0.26 + 0.74 * lit) * exposure;
        const sp = spec * lit;
        let r = cr0 * shade + sp + er;
        let g = cg0 * shade + sp + eg;
        let b = cb0 * shade + sp + eb;
        if (fog) {
          const dist = z;
          const f = 1 - Math.exp(-fogD * dist);
          r += (fogC.r - r) * f;
          g += (fogC.g - g) * f;
          b += (fogC.b - b) * f;
        }
        if (r > 1) r = 1;
        if (g > 1) g = 1;
        if (b > 1) b = 1;
        rgb[off * 3] = srgb(r) * 255;
        rgb[off * 3 + 1] = srgb(g) * 255;
        rgb[off * 3 + 2] = srgb(b) * 255;
      }
    }
  };

  const clipNear = (poly) => {
    const out = [];
    for (let i = 0; i < poly.length; i++) {
      const a = poly[i], b = poly[(i + 1) % poly.length];
      const ain = a.z >= NEAR, bin = b.z >= NEAR;
      if (ain) out.push(a);
      if (ain !== bin) {
        const t2 = (NEAR - a.z) / (b.z - a.z);
        out.push({ x: a.x + (b.x - a.x) * t2, y: a.y + (b.y - a.y) * t2, z: NEAR });
      }
    }
    return out;
  };

  for (const t of tris) {
    let poly;
    if (persp) {
      poly = [camSpace(t.a), camSpace(t.b), camSpace(t.c)];
      if (poly.every((p) => p.z < NEAR)) continue;
      if (poly.some((p) => p.z < NEAR)) {
        poly = clipNear(poly);
        if (poly.length < 3) continue;
      }
      poly = poly.map(toScreen);
      for (let i = 1; i < poly.length - 1; i++) fill(poly[0], poly[i], poly[i + 1], t);
    } else {
      const A = toScreen(camSpace(t.a)), B = toScreen(camSpace(t.b)), C = toScreen(camSpace(t.c));
      fill(A, B, C, t);
    }
  }

  // downsample
  const out = Buffer.alloc(W * H * 3);
  for (let y = 0; y < H; y++) {
    for (let x = 0; x < W; x++) {
      let r = 0, g = 0, b = 0;
      for (let dy = 0; dy < SS; dy++) {
        for (let dx = 0; dx < SS; dx++) {
          const off = (y * SS + dy) * w + (x * SS + dx);
          r += rgb[off * 3]; g += rgb[off * 3 + 1]; b += rgb[off * 3 + 2];
        }
      }
      const o = (y * W + x) * 3;
      out[o] = Math.min(255, Math.round(r / (SS * SS)));
      out[o + 1] = Math.min(255, Math.round(g / (SS * SS)));
      out[o + 2] = Math.min(255, Math.round(b / (SS * SS)));
    }
  }
  return { width: W, height: H, data: out };
}

/* ------------------------------------------------------------------ */
/*  Sheet compositor                                                   */
/* ------------------------------------------------------------------ */

export function sheet(cells, opts = {}) {
  const cols = opts.cols || 3;
  const cw = opts.cellW || 260, ch = opts.cellH || 190;
  const pad = opts.pad || 6;
  const rows = Math.ceil(cells.length / cols);
  const W = cols * (cw + pad) + pad;
  const H = rows * (ch + pad) + pad;
  const out = Buffer.alloc(W * H * 3, 18);
  cells.forEach((cell, i) => {
    const cx = pad + (i % cols) * (cw + pad);
    const cy = pad + Math.floor(i / cols) * (ch + pad);
    for (let y = 0; y < ch; y++) {
      for (let x = 0; x < cw; x++) {
        const si = ((y * cell.width / ch) | 0) * cell.width + ((x * cell.width / cw) | 0);
        const di = ((cy + y) * W + cx + x) * 3;
        out[di] = cell.data[si * 3];
        out[di + 1] = cell.data[si * 3 + 1];
        out[di + 2] = cell.data[si * 3 + 2];
      }
    }
  });
  return { width: W, height: H, data: out };
}

export function savePng(path, img) {
  mkdirSync(join(path, '..'), { recursive: true });
  writeFileSync(path, encodePNG(img.width, img.height, img.data));
  return path;
}

export { srgb, collectTriangles };
