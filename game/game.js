/* =========================================================
   2D Map — процедурная карта мира + панель инструментов
   Чистый canvas 2D, без библиотек.
   ========================================================= */

(() => {
  "use strict";

  /* ---------------- Константы ---------------- */

  const TILE = 32;          // размер тайла в мировых пикселях
  const MAP = 128;          // карта MAP x MAP тайлов
  const WORLD = TILE * MAP;
  const SEED = 20261029;

  const canvas = document.getElementById("world");
  const ctx = canvas.getContext("2d", { alpha: false });
  const coordsEl = document.getElementById("coords");

  /* ---------------- RNG / шум ---------------- */

  function mulberry32(a) {
    return function () {
      a |= 0;
      a = (a + 0x6d2b79f5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  const rand = mulberry32(SEED);

  function makeNoiseGrid(size, rng) {
    const g = new Float32Array((size + 1) * (size + 1));
    for (let i = 0; i < g.length; i++) g[i] = rng();
    return g;
  }

  const smooth = (t) => t * t * (3 - 2 * t);
  const lerp = (a, b, t) => a + (b - a) * t;

  function sampleGrid(grid, size, x, y) {
    const xi = Math.floor(x), yi = Math.floor(y);
    const tx = smooth(x - xi), ty = smooth(y - yi);
    const x0 = ((xi % size) + size) % size;
    const y0 = ((yi % size) + size) % size;
    const x1 = (x0 + 1) % size;
    const y1 = (y0 + 1) % size;
    const row0 = y0 * (size + 1), row1 = y1 * (size + 1);
    const a = grid[row0 + x0], b = grid[row0 + x1];
    const c = grid[row1 + x0], d = grid[row1 + x1];
    return lerp(lerp(a, b, tx), lerp(c, d, tx), ty);
  }

  /* ---------------- Генерация карты ---------------- */

  const WATER_DEEP = 0;
  const WATER = 1;
  const SAND = 2;
  const GRASS = 3;
  const GRASS_DARK = 4;
  const FOREST = 5;
  const ROCK = 6;
  const SNOW = 7;

  const elevation = new Float32Array(MAP * MAP);
  const moisture = new Float32Array(MAP * MAP);
  const tiles = new Uint8Array(MAP * MAP);

  const elevNoise = [
    makeNoiseGrid(16, rand),
    makeNoiseGrid(32, rand),
    makeNoiseGrid(64, rand),
    makeNoiseGrid(128, rand),
  ];
  const moistNoise = [
    makeNoiseGrid(16, rand),
    makeNoiseGrid(32, rand),
    makeNoiseGrid(64, rand),
  ];

  function fbm(grids, x, y, baseScale) {
    let amp = 1, freq = baseScale, sum = 0, norm = 0;
    for (let o = 0; o < grids.length; o++) {
      const size = grids[o].size;
      sum += sampleGrid(grids[o], size, x * freq, y * freq) * amp;
      norm += amp;
      amp *= 0.5;
      freq *= 2;
    }
    return sum / norm;
  }

  // подготавливаем размеры сеток
  (function attachSizes() {
    for (const g of elevNoise) g.size = Math.sqrt(g.length) - 1;
    for (const g of moistNoise) g.size = Math.sqrt(g.length) - 1;
  })();

  (function generate() {
    // --- высота ---
    for (let y = 0; y < MAP; y++) {
      for (let x = 0; x < MAP; x++) {
        const i = y * MAP + x;
        let e = fbm(elevNoise, x, y, 1 / 26);

        // лёгкая «островная» форма: края уходят под воду
        const nx = (x / (MAP - 1)) * 2 - 1;
        const ny = (y / (MAP - 1)) * 2 - 1;
        const d = Math.min(1, Math.hypot(nx, ny) * 0.92);
        e = e * (1 - d * 0.75) + 0.04;

        elevation[i] = e;
        moisture[i] = fbm(moistNoise, x, y, 1 / 34);
      }
    }

    // --- река: извилистая линия сверху вниз ---
    const riverWobble = makeNoiseGrid(64, rand);
    riverWobble.size = Math.sqrt(riverWobble.length) - 1;
    let rx = MAP * (0.32 + rand() * 0.3);
    for (let y = -2; y < MAP + 2; y++) {
      const drift = (sampleGrid(riverWobble, riverWobble.size, y * 0.6, 0.5) - 0.5) * 2.6;
      rx += drift;
      rx = Math.max(6, Math.min(MAP - 7, rx));

      const width = 1.1 + sampleGrid(riverWobble, riverWobble.size, y * 0.9, 20.5) * 1.6;
      const x0 = Math.round(rx - width);
      const x1 = Math.round(rx + width);

      for (let x = x0; x <= x1; x++) {
        if (x < 0 || x >= MAP || y < 0 || y >= MAP) continue;
        const i = y * MAP + x;
        const t = Math.abs(x - rx) / (width + 0.001);
        elevation[i] = Math.min(elevation[i], 0.10 + t * 0.10);
        moisture[i] = Math.min(1, moisture[i] + 0.25);
      }
      // берега
      for (const bx of [x0 - 1, x0 - 2, x1 + 1, x1 + 2]) {
        if (bx < 0 || bx >= MAP || y < 0 || y >= MAP) continue;
        const i = y * MAP + bx;
        elevation[i] = Math.min(elevation[i], 0.365);
      }
    }

    // --- классификация тайлов ---
    for (let y = 0; y < MAP; y++) {
      for (let x = 0; x < MAP; x++) {
        const i = y * MAP + x;
        const e = elevation[i];
        const m = moisture[i];

        let t;
        if (e < 0.245) t = WATER_DEEP;
        else if (e < 0.315) t = WATER;
        else if (e < 0.365) t = SAND;
        else if (e > 0.80) t = SNOW;
        else if (e > 0.70) t = ROCK;
        else if (m > 0.56 + (0.5 - e) * 0.55) t = FOREST;
        else if (m < 0.36) t = GRASS_DARK;
        else t = GRASS;

        tiles[i] = t;
      }
    }
  })();

  /* ---------------- Спрайты тайлов ---------------- */

  const PALETTE = {
    [WATER_DEEP]: { base: "#173d63", light: "#1d4c78", dark: "#102f4e" },
    [WATER]: { base: "#25618f", light: "#3a7cae", dark: "#1b4c72" },
    [SAND]: { base: "#d9c48d", light: "#e8d7a6", dark: "#bfa877" },
    [GRASS]: { base: "#5d9b51", light: "#6cae5c", dark: "#4a8043" },
    [GRASS_DARK]: { base: "#4d8747", light: "#5c9a53", dark: "#3d6f3b" },
    [FOREST]: { base: "#3f7440", light: "#4c8a49", dark: "#315c34" },
    [ROCK]: { base: "#7d8188", light: "#959aa1", dark: "#63676e" },
    [SNOW]: { base: "#dfe8ee", light: "#f2f7fb", dark: "#c3d1dc" },
  };

  const TILE_VARIANTS = 4;
  const tileSprites = new Map(); // "type:variant" -> canvas

  function makeTileSprite(type, variant) {
    const c = document.createElement("canvas");
    c.width = TILE;
    c.height = TILE;
    const g = c.getContext("2d");
    const pal = PALETTE[type];
    const rng = mulberry32(SEED + type * 977 + variant * 131 + 7);

    g.fillStyle = pal.base;
    g.fillRect(0, 0, TILE, TILE);

    if (type === WATER_DEEP || type === WATER) {
      // волны
      g.strokeStyle = pal.light;
      g.globalAlpha = 0.45;
      g.lineWidth = 1;
      for (let k = 0; k < 3; k++) {
        const y = 5 + rng() * 22;
        const x = rng() * 10;
        g.beginPath();
        g.moveTo(x, y);
        g.quadraticCurveTo(x + 6, y - 2.2, x + 13, y);
        g.stroke();
      }
      g.globalAlpha = 1;
    } else {
      // зернистость
      for (let k = 0; k < 46; k++) {
        const x = rng() * TILE;
        const y = rng() * TILE;
        const s = rng() < 0.7 ? 1 : 2;
        g.fillStyle = rng() < 0.5 ? pal.light : pal.dark;
        g.globalAlpha = 0.18 + rng() * 0.32;
        g.fillRect(x, y, s, s);
      }
      g.globalAlpha = 1;
    }

    if (type === GRASS || type === GRASS_DARK || type === FOREST) {
      g.strokeStyle = pal.light;
      g.globalAlpha = 0.3;
      g.lineWidth = 1;
      for (let k = 0; k < 5; k++) {
        const x = 2 + rng() * (TILE - 4);
        const y = 3 + rng() * (TILE - 5);
        g.beginPath();
        g.moveTo(x, y + 3);
        g.lineTo(x + (rng() - 0.5) * 2, y);
        g.stroke();
      }
      g.globalAlpha = 1;
    }

    if (type === ROCK || type === SNOW) {
      g.strokeStyle = pal.dark;
      g.globalAlpha = 0.28;
      for (let k = 0; k < 3; k++) {
        const x = rng() * TILE, y = rng() * TILE;
        g.beginPath();
        g.moveTo(x, y);
        g.lineTo(x + 4 + rng() * 6, y + (rng() - 0.5) * 5);
        g.stroke();
      }
      g.globalAlpha = 1;
    }

    return c;
  }

  function getTileSprite(type, variant) {
    const key = type + ":" + variant;
    let s = tileSprites.get(key);
    if (!s) {
      s = makeTileSprite(type, variant);
      tileSprites.set(key, s);
    }
    return s;
  }

  // предгенерация всех вариантов
  for (const t of Object.keys(PALETTE)) {
    for (let v = 0; v < TILE_VARIANTS; v++) getTileSprite(Number(t), v);
  }

  /* ---------------- Декорации ---------------- */

  function makeTreeSprite(kind, rng) {
    const c = document.createElement("canvas");
    c.width = TILE;
    c.height = TILE * 2;
    const g = c.getContext("2d");
    const cx = TILE / 2;
    const baseY = TILE * 2 - 3;

    // тень
    g.fillStyle = "rgba(0,0,0,0.22)";
    g.beginPath();
    g.ellipse(cx, baseY - 1, 9, 4, 0, 0, Math.PI * 2);
    g.fill();

    // ствол
    g.fillStyle = "#5b3f2a";
    g.fillRect(cx - 2, baseY - 12, 4, 12);
    g.fillStyle = "#6d4c33";
    g.fillRect(cx - 1, baseY - 12, 2, 12);

    const greens =
      kind === 0
        ? ["#2f6b39", "#3c8245", "#4e9a51"]
        : kind === 1
        ? ["#285f34", "#357a3d", "#468f48"]
        : ["#3a7440", "#4a8b4a", "#5ba058"];

    // крона: несколько кругов
    const blobs = [
      [0, -30, 12],
      [-7, -24, 9],
      [7, -24, 9],
      [0, -21, 10],
      [-4, -34, 7],
      [5, -33, 6],
    ];
    for (const [dx, dy, r] of blobs) {
      g.fillStyle = greens[0];
      g.beginPath();
      g.arc(cx + dx, baseY + dy, r, 0, Math.PI * 2);
      g.fill();
    }
    // блики
    for (const [dx, dy, r] of blobs) {
      g.fillStyle = greens[1];
      g.beginPath();
      g.arc(cx + dx - r * 0.22, baseY + dy - r * 0.28, r * 0.6, 0, Math.PI * 2);
      g.fill();
    }
    for (let k = 0; k < 4; k++) {
      g.fillStyle = greens[2];
      g.globalAlpha = 0.55;
      g.beginPath();
      g.arc(cx - 5 + rng() * 10, baseY - 36 + rng() * 16, 2 + rng() * 1.6, 0, Math.PI * 2);
      g.fill();
    }
    g.globalAlpha = 1;

    return c;
  }

  function makeRockSprite(rng) {
    const c = document.createElement("canvas");
    c.width = TILE;
    c.height = TILE;
    const g = c.getContext("2d");
    g.fillStyle = "rgba(0,0,0,0.2)";
    g.beginPath();
    g.ellipse(16, 27, 9, 3.5, 0, 0, Math.PI * 2);
    g.fill();

    g.beginPath();
    g.moveTo(7, 26);
    g.lineTo(10, 14);
    g.lineTo(17, 9);
    g.lineTo(24, 15);
    g.lineTo(26, 26);
    g.closePath();
    g.fillStyle = "#8a9099";
    g.fill();
    g.strokeStyle = "#5f656d";
    g.lineWidth = 1;
    g.stroke();

    g.beginPath();
    g.moveTo(11, 25);
    g.lineTo(13, 16);
    g.lineTo(18, 12);
    g.lineTo(20, 22);
    g.closePath();
    g.fillStyle = "#a2a9b2";
    g.fill();
    return c;
  }

  function makeBushSprite(rng) {
    const c = document.createElement("canvas");
    c.width = TILE;
    c.height = TILE;
    const g = c.getContext("2d");
    g.fillStyle = "rgba(0,0,0,0.18)";
    g.beginPath();
    g.ellipse(16, 27, 8, 3, 0, 0, Math.PI * 2);
    g.fill();
    const blobs = [
      [16, 20, 7],
      [11, 22, 5.5],
      [21, 22, 5.5],
      [16, 16, 5],
    ];
    for (const [x, y, r] of blobs) {
      g.fillStyle = "#3f7d43";
      g.beginPath();
      g.arc(x, y, r, 0, Math.PI * 2);
      g.fill();
    }
    for (const [x, y, r] of blobs) {
      g.fillStyle = "#4f9450";
      g.beginPath();
      g.arc(x - 1.5, y - 1.5, r * 0.58, 0, Math.PI * 2);
      g.fill();
    }
    return c;
  }

  function makeFlowerSprite(color) {
    const c = document.createElement("canvas");
    c.width = TILE;
    c.height = TILE;
    const g = c.getContext("2d");
    g.strokeStyle = "#3f7d43";
    g.lineWidth = 1;
    g.beginPath();
    g.moveTo(16, 26);
    g.lineTo(16, 20);
    g.stroke();
    g.fillStyle = color;
    for (let k = 0; k < 5; k++) {
      const a = (k / 5) * Math.PI * 2;
      g.beginPath();
      g.arc(16 + Math.cos(a) * 2.6, 19 + Math.sin(a) * 2.6, 1.9, 0, Math.PI * 2);
      g.fill();
    }
    g.fillStyle = "#f6e27a";
    g.beginPath();
    g.arc(16, 19, 1.5, 0, Math.PI * 2);
    g.fill();
    return c;
  }

  const decorations = []; // { x, y (мировые px, точка "ног"), sprite }

  (function placeDecorations() {
    const decoRand = mulberry32(SEED ^ 0x9e3779b9);
    const treeSprites = [0, 1, 2].map((k) => makeTreeSprite(k, decoRand));
    const rockSprite = makeRockSprite(decoRand);
    const bushSprite = makeBushSprite(decoRand);
    const flowerSprites = ["#f2f0e4", "#f0d35a", "#e78bb0", "#c9a2ea"].map(makeFlowerSprite);

    const at = (x, y) => tiles[y * MAP + x];

    for (let y = 1; y < MAP - 1; y++) {
      for (let x = 1; x < MAP - 1; x++) {
        const t = at(x, y);
        const cx = x * TILE + TILE / 2 + (decoRand() - 0.5) * 10;
        const cy = y * TILE + TILE / 2 + (decoRand() - 0.5) * 8;
        const r = decoRand();

        if (t === FOREST) {
          if (r < 0.42) {
            decorations.push({
              x: cx,
              y: cy,
              sprite: treeSprites[(decoRand() * 3) | 0],
              scale: 0.85 + decoRand() * 0.4,
            });
          } else if (r < 0.5) {
            decorations.push({ x: cx, y: cy, sprite: bushSprite, scale: 0.9 });
          }
        } else if (t === GRASS || t === GRASS_DARK) {
          if (r < 0.045) {
            decorations.push({
              x: cx,
              y: cy,
              sprite: treeSprites[(decoRand() * 3) | 0],
              scale: 0.8 + decoRand() * 0.35,
            });
          } else if (r < 0.075) {
            decorations.push({ x: cx, y: cy, sprite: bushSprite, scale: 0.85 });
          } else if (r < 0.115) {
            decorations.push({
              x: cx,
              y: cy,
              sprite: flowerSprites[(decoRand() * flowerSprites.length) | 0],
              scale: 0.9,
            });
          }
        } else if (t === ROCK || t === SNOW) {
          if (r < 0.16) {
            decorations.push({
              x: cx,
              y: cy,
              sprite: rockSprite,
              scale: 0.8 + decoRand() * 0.5,
            });
          }
        } else if (t === SAND) {
          if (r < 0.03) {
            decorations.push({ x: cx, y: cy, sprite: rockSprite, scale: 0.7 });
          }
        }
      }
    }

    decorations.sort((a, b) => a.y - b.y);
  })();

  /* ---------------- Камера ---------------- */

  const cam = {
    x: WORLD / 2,
    y: WORLD / 2,
    zoom: 1,
    targetZoom: 1,
  };

  const MIN_ZOOM = 0.45;
  const MAX_ZOOM = 2.6;

  let viewW = 0;
  let viewH = 0;
  let dpr = 1;

  function resize() {
    dpr = Math.min(window.devicePixelRatio || 1, 2);
    viewW = canvas.clientWidth;
    viewH = canvas.clientHeight;
    canvas.width = Math.round(viewW * dpr);
    canvas.height = Math.round(viewH * dpr);
    clampCamera();
  }

  function clampCamera() {
    const halfW = viewW / (2 * cam.zoom);
    const halfH = viewH / (2 * cam.zoom);
    const margin = TILE * 4;

    if (halfW * 2 >= WORLD + margin * 2) cam.x = WORLD / 2;
    else cam.x = Math.min(WORLD + margin - halfW, Math.max(-margin + halfW, cam.x));

    if (halfH * 2 >= WORLD + margin * 2) cam.y = WORLD / 2;
    else cam.y = Math.min(WORLD + margin - halfH, Math.max(-margin + halfH, cam.y));
  }

  /* ---------------- Ввод ---------------- */

  const keys = new Set();
  let dragging = false;
  let lastPX = 0;
  let lastPY = 0;
  let hoverTile = null;

  window.addEventListener("keydown", (e) => {
    const k = e.key.toLowerCase();
    if (["w", "a", "s", "d", "arrowup", "arrowdown", "arrowleft", "arrowright"].includes(k)) {
      keys.add(k);
      e.preventDefault();
    }
    if (k === "+" || k === "=") setZoom(cam.targetZoom * 1.2, viewW / 2, viewH / 2);
    if (k === "-") setZoom(cam.targetZoom / 1.2, viewW / 2, viewH / 2);
  });

  window.addEventListener("keyup", (e) => keys.delete(e.key.toLowerCase()));
  window.addEventListener("blur", () => keys.clear());

  canvas.addEventListener("pointerdown", (e) => {
    dragging = true;
    lastPX = e.clientX;
    lastPY = e.clientY;
    canvas.classList.add("dragging");
    canvas.setPointerCapture(e.pointerId);
  });

  canvas.addEventListener("pointermove", (e) => {
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;
    updateHover(mx, my);

    if (!dragging) return;
    const dx = e.clientX - lastPX;
    const dy = e.clientY - lastPY;
    lastPX = e.clientX;
    lastPY = e.clientY;

    cam.x -= dx / cam.zoom;
    cam.y -= dy / cam.zoom;
    clampCamera();
  });

  const endDrag = (e) => {
    dragging = false;
    canvas.classList.remove("dragging");
    if (e && e.pointerId != null && canvas.hasPointerCapture?.(e.pointerId)) {
      canvas.releasePointerCapture(e.pointerId);
    }
  };
  canvas.addEventListener("pointerup", endDrag);
  canvas.addEventListener("pointercancel", endDrag);
  canvas.addEventListener("pointerleave", () => {
    hoverTile = null;
    updateCoords(null);
  });

  canvas.addEventListener(
    "wheel",
    (e) => {
      e.preventDefault();
      const rect = canvas.getBoundingClientRect();
      const factor = Math.exp(-e.deltaY * 0.0016);
      setZoom(cam.targetZoom * factor, e.clientX - rect.left, e.clientY - rect.top);
    },
    { passive: false }
  );

  function setZoom(z, anchorX, anchorY) {
    const next = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, z));
    if (next === cam.targetZoom) return;

    // мировая точка под курсором
    const before = screenToWorld(anchorX, anchorY);
    cam.targetZoom = next;
    cam.zoom = next; // мгновенный зум — предсказуемее при перетаскивании
    const after = screenToWorld(anchorX, anchorY);
    cam.x += before.x - after.x;
    cam.y += before.y - after.y;
    clampCamera();
    updateHover(anchorX, anchorY);
  }

  function screenToWorld(sx, sy) {
    return {
      x: cam.x + (sx - viewW / 2) / cam.zoom,
      y: cam.y + (sy - viewH / 2) / cam.zoom,
    };
  }

  function updateHover(sx, sy) {
    const w = screenToWorld(sx, sy);
    const tx = Math.floor(w.x / TILE);
    const ty = Math.floor(w.y / TILE);
    if (tx < 0 || ty < 0 || tx >= MAP || ty >= MAP) {
      hoverTile = null;
      updateCoords(null);
    } else {
      hoverTile = { x: tx, y: ty };
      updateCoords(hoverTile);
    }
  }

  function updateCoords(t) {
    coordsEl.innerHTML = t
      ? `x: ${String(t.x).padStart(3, "0")}&nbsp;&nbsp;y: ${String(t.y).padStart(3, "0")}`
      : "x: ---&nbsp;&nbsp;y: ---";
  }

  /* ---------------- Рендер ---------------- */

  function drawTile(type, variant, dx, dy, size) {
    ctx.drawImage(getTileSprite(type, variant), dx, dy, size + 0.6, size + 0.6);
  }

  function render() {
    const zoom = cam.zoom;
    const tileSize = TILE * zoom;

    // фон за пределами карты
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.fillStyle = "#0d2b47";
    ctx.fillRect(0, 0, viewW, viewH);

    ctx.save();
    ctx.translate(viewW / 2, viewH / 2);
    ctx.scale(zoom, zoom);
    ctx.translate(-cam.x, -cam.y);

    const left = cam.x - viewW / (2 * zoom);
    const top = cam.y - viewH / (2 * zoom);
    const right = cam.x + viewW / (2 * zoom);
    const bottom = cam.y + viewH / (2 * zoom);

    const x0 = Math.max(0, Math.floor(left / TILE));
    const y0 = Math.max(0, Math.floor(top / TILE));
    const x1 = Math.min(MAP - 1, Math.floor(right / TILE));
    const y1 = Math.min(MAP - 1, Math.floor(bottom / TILE));

    for (let y = y0; y <= y1; y++) {
      for (let x = x0; x <= x1; x++) {
        const i = y * MAP + x;
        const type = tiles[i];
        const variant = (x * 7 + y * 13 + ((x * y) & 3)) & (TILE_VARIANTS - 1);
        drawTile(type, variant, x * TILE, y * TILE, TILE);
      }
    }

    // декорации (уже отсортированы по y)
    for (let k = 0; k < decorations.length; k++) {
      const d = decorations[k];
      if (d.y < top - TILE * 3 || d.y > bottom + TILE * 3) continue;
      if (d.x < left - TILE * 2 || d.x > right + TILE * 2) continue;
      const w = d.sprite.width * d.scale;
      const h = d.sprite.height * d.scale;
      ctx.drawImage(d.sprite, d.x - w / 2, d.y - h + 4 * d.scale, w, h);
    }

    // подсветка тайла под курсором
    if (hoverTile) {
      ctx.lineWidth = 2 / zoom;
      ctx.strokeStyle = "rgba(255,255,255,0.75)";
      ctx.strokeRect(
        hoverTile.x * TILE + 0.5,
        hoverTile.y * TILE + 0.5,
        TILE - 1,
        TILE - 1
      );
      ctx.fillStyle = "rgba(255,255,255,0.10)";
      ctx.fillRect(hoverTile.x * TILE, hoverTile.y * TILE, TILE, TILE);
    }

    ctx.restore();

    // виньетка
    const grad = ctx.createRadialGradient(
      viewW / 2,
      viewH / 2,
      Math.min(viewW, viewH) * 0.35,
      viewW / 2,
      viewH / 2,
      Math.max(viewW, viewH) * 0.78
    );
    grad.addColorStop(0, "rgba(0,0,0,0)");
    grad.addColorStop(1, "rgba(4,10,20,0.42)");
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, viewW, viewH);
  }

  /* ---------------- Цикл ---------------- */

  let last = performance.now();

  function update(dt) {
    const speed = 620 / cam.zoom;
    let dx = 0, dy = 0;
    if (keys.has("a") || keys.has("arrowleft")) dx -= 1;
    if (keys.has("d") || keys.has("arrowright")) dx += 1;
    if (keys.has("w") || keys.has("arrowup")) dy -= 1;
    if (keys.has("s") || keys.has("arrowdown")) dy += 1;

    if (dx || dy) {
      const len = Math.hypot(dx, dy) || 1;
      cam.x += (dx / len) * speed * dt;
      cam.y += (dy / len) * speed * dt;
      clampCamera();
    }

    if (Math.abs(cam.zoom - cam.targetZoom) > 0.0005) {
      cam.zoom += (cam.targetZoom - cam.zoom) * Math.min(1, dt * 14);
      clampCamera();
    }
  }

  function frame(now) {
    const dt = Math.min(0.05, (now - last) / 1000);
    last = now;
    update(dt);
    render();
    requestAnimationFrame(frame);
  }

  /* ---------------- Старт ---------------- */

  window.addEventListener("resize", resize);
  resize();
  requestAnimationFrame(frame);
})();
