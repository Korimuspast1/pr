import * as THREE from '../vendor/three.module.js';

const HAS_DOM = typeof document !== 'undefined' && typeof document.createElement === 'function';

export function canvasEl(w, h) {
  if (!HAS_DOM) return null;
  const c = document.createElement('canvas');
  c.width = w;
  c.height = h;
  return c;
}

export function ctx2d(c) {
  if (!c) return null;
  try {
    const g = c.getContext('2d');
    return g && typeof g.createRadialGradient === 'function' ? g : null;
  } catch {
    return null;
  }
}

/**
 * Draws a square procedural texture. Returns null when there is no 2D canvas
 * (Node tests), in which case materials simply become flat colours.
 */
export function canvasTexture(size, draw, opts = {}) {
  const c = canvasEl(size, size);
  const g = ctx2d(c);
  if (!g) return null;
  draw(g, size);
  const t = new THREE.CanvasTexture(c);
  t.wrapS = t.wrapT = THREE.RepeatWrapping;
  const r = opts.repeat;
  if (Array.isArray(r)) t.repeat.set(r[0], r[1]);
  else if (r != null) t.repeat.set(r, r);
  t.anisotropy = opts.anisotropy || 8;
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

const rnd = (a, b) => a + Math.random() * (b - a);

/* ---------------- surfaces ---------------- */

export function asphaltTexture(repeat = 12) {
  return canvasTexture(512, (g, s) => {
    g.fillStyle = '#26262a';
    g.fillRect(0, 0, s, s);
    for (let i = 0; i < 24000; i++) {
      const v = 26 + Math.random() * 30;
      g.fillStyle = `rgba(${v},${v},${v + 3},${rnd(0.25, 0.7)})`;
      g.fillRect(Math.random() * s, Math.random() * s, rnd(1, 3), rnd(1, 3));
    }
    g.strokeStyle = 'rgba(10,10,12,0.5)';
    for (let i = 0; i < 22; i++) {
      let x = Math.random() * s, y = Math.random() * s;
      g.lineWidth = rnd(0.5, 1.6);
      g.beginPath();
      g.moveTo(x, y);
      for (let k = 0; k < 6; k++) {
        x += rnd(-70, 70); y += rnd(-70, 70);
        g.lineTo(x, y);
      }
      g.stroke();
    }
    for (let i = 0; i < 30; i++) {
      g.fillStyle = `rgba(12,12,14,${rnd(0.05, 0.18)})`;
      g.beginPath();
      g.ellipse(Math.random() * s, Math.random() * s, rnd(6, 34), rnd(4, 22), rnd(0, 6), 0, Math.PI * 2);
      g.fill();
    }
  }, { repeat });
}

export function concreteTexture(repeat = 4) {
  return canvasTexture(512, (g, s) => {
    g.fillStyle = '#8f8f8a';
    g.fillRect(0, 0, s, s);
    for (let i = 0; i < 16000; i++) {
      const v = Math.round(rnd(110, 168));
      g.fillStyle = `rgba(${v},${v},${v - 4},${rnd(0.15, 0.5)})`;
      g.fillRect(Math.random() * s, Math.random() * s, rnd(1, 3), rnd(1, 3));
    }
    // expansion joints
    g.strokeStyle = 'rgba(58,58,56,0.55)';
    g.lineWidth = 3;
    for (const p of [0, s / 2, s]) {
      g.beginPath(); g.moveTo(p, 0); g.lineTo(p, s); g.stroke();
      g.beginPath(); g.moveTo(0, p); g.lineTo(s, p); g.stroke();
    }
    for (let i = 0; i < 34; i++) {
      g.strokeStyle = `rgba(50,50,48,${rnd(0.08, 0.25)})`;
      g.lineWidth = rnd(0.6, 1.8);
      const x = Math.random() * s, y = Math.random() * s;
      g.beginPath(); g.moveTo(x, y);
      g.lineTo(x + rnd(-40, 40), y + rnd(-40, 40));
      g.stroke();
    }
    // stains
    for (let i = 0; i < 18; i++) {
      g.fillStyle = `rgba(60,58,52,${rnd(0.04, 0.14)})`;
      g.beginPath();
      g.ellipse(Math.random() * s, Math.random() * s, rnd(10, 50), rnd(8, 34), rnd(0, 6), 0, Math.PI * 2);
      g.fill();
    }
  }, { repeat });
}

export function brickTexture(repeat = 4) {
  return canvasTexture(512, (g, s) => {
    g.fillStyle = '#3a2a24';
    g.fillRect(0, 0, s, s);
    const bh = 26, bw = 62;
    for (let row = 0; row * bh < s; row++) {
      const off = row % 2 ? bw / 2 : 0;
      for (let col = -1; col * bw < s + bw; col++) {
        const x = col * bw + off;
        const y = row * bh;
        const r = Math.round(rnd(96, 138)), gg = Math.round(r * rnd(0.55, 0.7)), b = Math.round(r * rnd(0.45, 0.6));
        g.fillStyle = `rgb(${r},${gg},${b})`;
        g.fillRect(x + 2, y + 2, bw - 4, bh - 4);
        g.fillStyle = `rgba(0,0,0,${rnd(0.02, 0.16)})`;
        g.fillRect(x + 2, y + 2, bw - 4, bh - 4);
      }
    }
    for (let i = 0; i < 12; i++) {
      g.fillStyle = `rgba(20,16,14,${rnd(0.05, 0.16)})`;
      g.beginPath();
      g.ellipse(Math.random() * s, Math.random() * s, rnd(14, 60), rnd(10, 40), rnd(0, 6), 0, Math.PI * 2);
      g.fill();
    }
  }, { repeat });
}

export function metalTexture(repeat = 6) {
  return canvasTexture(512, (g, s) => {
    g.fillStyle = '#595d5f';
    g.fillRect(0, 0, s, s);
    // corrugation
    for (let x = 0; x < s; x += 16) {
      const grad = g.createLinearGradient(x, 0, x + 16, 0);
      grad.addColorStop(0, 'rgba(255,255,255,0.10)');
      grad.addColorStop(0.45, 'rgba(0,0,0,0.16)');
      grad.addColorStop(1, 'rgba(255,255,255,0.06)');
      g.fillStyle = grad;
      g.fillRect(x, 0, 16, s);
    }
    // rust
    for (let i = 0; i < 130; i++) {
      g.fillStyle = `rgba(${Math.round(rnd(110, 150))},${Math.round(rnd(60, 90))},${Math.round(rnd(25, 45))},${rnd(0.05, 0.3)})`;
      g.beginPath();
      g.ellipse(Math.random() * s, Math.random() * s, rnd(3, 26), rnd(2, 15), rnd(0, 6), 0, Math.PI * 2);
      g.fill();
    }
    g.strokeStyle = 'rgba(20,20,20,0.35)';
    for (let x = 0; x < s; x += 64) {
      g.beginPath(); g.moveTo(x, 0); g.lineTo(x, s); g.stroke();
    }
  }, { repeat });
}

export function woodTexture(repeat = 3) {
  return canvasTexture(512, (g, s) => {
    g.fillStyle = '#6a4a2c';
    g.fillRect(0, 0, s, s);
    for (let i = 0; i < 2400; i++) {
      const v = rnd(0.2, 1);
      g.strokeStyle = `rgba(${Math.round(60 + v * 90)},${Math.round(40 + v * 60)},${Math.round(20 + v * 30)},${rnd(0.05, 0.3)})`;
      g.lineWidth = rnd(0.5, 2);
      const y = Math.random() * s;
      g.beginPath();
      g.moveTo(0, y);
      g.bezierCurveTo(s * 0.3, y + rnd(-8, 8), s * 0.6, y + rnd(-8, 8), s, y + rnd(-4, 4));
      g.stroke();
    }
    g.strokeStyle = 'rgba(30,20,12,0.5)';
    g.lineWidth = 3;
    for (let x = 0; x < s; x += 128) { g.beginPath(); g.moveTo(x, 0); g.lineTo(x, s); g.stroke(); }
  }, { repeat });
}

export function dirtTexture(repeat = 16) {
  return canvasTexture(512, (g, s) => {
    g.fillStyle = '#3b332a';
    g.fillRect(0, 0, s, s);
    for (let i = 0; i < 26000; i++) {
      const v = Math.random();
      g.fillStyle = `rgba(${Math.round(48 + v * 60)},${Math.round(40 + v * 48)},${Math.round(30 + v * 38)},${rnd(0.2, 0.6)})`;
      g.fillRect(Math.random() * s, Math.random() * s, rnd(1, 3), rnd(1, 3));
    }
    for (let i = 0; i < 40; i++) {
      g.fillStyle = `rgba(20,17,13,${rnd(0.06, 0.22)})`;
      g.beginPath();
      g.ellipse(Math.random() * s, Math.random() * s, rnd(8, 44), rnd(6, 30), rnd(0, 6), 0, Math.PI * 2);
      g.fill();
    }
  }, { repeat });
}
