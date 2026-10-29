(function (root) {
  "use strict";

  /* =========================================================
     render.js — небо, параллакс, рельеф
     ========================================================= */

  function hash(i) {
    let h = Math.imul(i ^ 0x9e3779b9, 0x85ebca6b);
    h ^= h >>> 13;
    h = Math.imul(h, 0xc2b2ae35);
    h ^= h >>> 16;
    return (h >>> 0) / 4294967296;
  }

  function drawSky(ctx, cam, w, h) {
    const horizon = -cam.y * cam.zoom + h * 0.62 + (cam.y * 0.0);
    const g = ctx.createLinearGradient(0, 0, 0, h);
    g.addColorStop(0, "#5f9dc9");
    g.addColorStop(0.45, "#9fc8e0");
    g.addColorStop(0.75, "#d3e4ec");
    g.addColorStop(1, "#e8eef0");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);

    // солнце
    const sunX = w * 0.78 - cam.x * 0.02;
    const sunY = h * 0.16 - cam.y * 0.02;
    const sg = ctx.createRadialGradient(sunX, sunY, 10, sunX, sunY, 190);
    sg.addColorStop(0, "rgba(255,246,214,0.95)");
    sg.addColorStop(0.3, "rgba(255,240,196,0.35)");
    sg.addColorStop(1, "rgba(255,240,196,0)");
    ctx.fillStyle = sg;
    ctx.beginPath();
    ctx.arc(sunX, sunY, 190, 0, Math.PI * 2);
    ctx.fill();

    // облака
    const cloudOffset = -cam.x * 0.06;
    ctx.fillStyle = "rgba(255,255,255,0.55)";
    for (let i = 0; i < 16; i++) {
      const baseX = i * 720 + hash(i * 3) * 380;
      let x = ((baseX + cloudOffset) % (w + 900)) - 450;
      const y = 40 + hash(i * 7) * h * 0.28 - cam.y * 0.03;
      const s = 0.7 + hash(i * 11) * 0.9;
      ctx.beginPath();
      ctx.ellipse(x, y, 90 * s, 26 * s, 0, 0, Math.PI * 2);
      ctx.ellipse(x + 70 * s, y + 8 * s, 62 * s, 20 * s, 0, 0, Math.PI * 2);
      ctx.ellipse(x - 62 * s, y + 10 * s, 52 * s, 17 * s, 0, 0, Math.PI * 2);
      ctx.fill();
    }

    // далёкие холмы (параллакс, несколько слоёв)
    drawHills(ctx, cam, w, h, 0.10, h * 0.58, 90, "#8fabc0", 0.55, 1.7);
    drawHills(ctx, cam, w, h, 0.22, h * 0.67, 74, "#7b9aa8", 0.6, 3.1);
    drawHills(ctx, cam, w, h, 0.36, h * 0.75, 58, "#698a83", 0.7, 5.3);
    drawHills(ctx, cam, w, h, 0.52, h * 0.82, 44, "#5b7d6a", 0.85, 8.9);
    return horizon;
  }

  function drawHills(ctx, cam, w, h, parallax, baseY, amp, color, alpha, freq) {
    const offset = -cam.x * parallax;
    const y0 = baseY - cam.y * parallax * 0.08;
    ctx.globalAlpha = alpha;
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.moveTo(-40, y0 + amp * 3 + 200);
    const stepPx = 18;
    for (let x = -40; x <= w + 40; x += stepPx) {
      const wx = (x - offset) * 0.001 * freq;
      const y =
        y0 -
        (Math.sin(wx) * amp * 0.55 +
          Math.sin(wx * 2.13 + 1.7) * amp * 0.3 +
          Math.sin(wx * 4.7 + 0.4) * amp * 0.15) -
        amp * 0.35;
      ctx.lineTo(x, y);
    }
    ctx.lineTo(w + 40, y0 + amp * 3 + 200);
    ctx.closePath();
    ctx.fill();

    // дымка у основания слоя
    const grad = ctx.createLinearGradient(0, y0 - amp, 0, y0 + amp * 1.6);
    grad.addColorStop(0, "rgba(214,231,240,0)");
    grad.addColorStop(1, "rgba(214,231,240,0.45)");
    ctx.fillStyle = grad;
    ctx.fill();
    ctx.globalAlpha = 1;
  }

  function surfacePath(ctx, terrain, left, right, step) {
    ctx.beginPath();
    ctx.moveTo(left, terrain.groundY(left));
    for (let x = left + step; x < right; x += step) {
      ctx.lineTo(x, terrain.groundY(x));
    }
    ctx.lineTo(right, terrain.groundY(right));
  }

  function drawTerrain(ctx, cam, terrain, worldH) {
    const left = cam.left - 20;
    const right = cam.right + 20;
    const bottom = cam.bottom + 200;

    // основной грунт
    surfacePath(ctx, terrain, left, right, 6);
    ctx.lineTo(right, bottom);
    ctx.lineTo(left, bottom);
    ctx.closePath();
    const g = ctx.createLinearGradient(0, cam.top, 0, bottom);
    g.addColorStop(0, "#6b5238");
    g.addColorStop(0.35, "#54402c");
    g.addColorStop(1, "#33271c");
    ctx.fillStyle = g;
    ctx.fill();

    // слой почвы под травой
    surfacePath(ctx, terrain, left, right, 6);
    for (let x = right; x >= left; x -= 6) ctx.lineTo(x, terrain.groundY(x) + 16);
    ctx.closePath();
    ctx.fillStyle = "#6a5236";
    ctx.fill();

    // травянистая кромка
    ctx.beginPath();
    surfacePath(ctx, terrain, left, right, 5);
    for (let x = right; x >= left; x -= 5) ctx.lineTo(x, terrain.groundY(x) + 9);
    ctx.closePath();
    ctx.fillStyle = "#4c7a3c";
    ctx.fill();

    // верхняя яркая линия травы
    ctx.beginPath();
    surfacePath(ctx, terrain, left, right, 4);
    ctx.lineWidth = 4;
    ctx.strokeStyle = "#5f9648";
    ctx.stroke();

    // травинки
    const stepX = 26;
    const startI = Math.floor(left / stepX);
    for (let i = startI; i < startI + Math.ceil((right - left) / stepX) + 2; i++) {
      const x = i * stepX + hash(i) * 14;
      const gy = terrain.groundY(x);
      const bent = (hash(i * 5) - 0.5) * 6;
      const hh = 5 + hash(i * 9) * 9;
      ctx.strokeStyle = hash(i * 3) > 0.5 ? "#68a04e" : "#46803a";
      ctx.lineWidth = 1.6;
      ctx.beginPath();
      ctx.moveTo(x, gy);
      ctx.quadraticCurveTo(x + bent * 0.4, gy - hh * 0.6, x + bent, gy - hh);
      ctx.stroke();
    }

    // камешки в грунте
    for (let i = startI; i < startI + Math.ceil((right - left) / 90) + 2; i++) {
      const x = i * 90 + hash(i * 13) * 60;
      const gy = terrain.groundY(x);
      const depth = 22 + hash(i * 7) * 90;
      const r = 2 + hash(i * 19) * 4;
      ctx.fillStyle = "rgba(0,0,0,0.18)";
      ctx.beginPath();
      ctx.ellipse(x, gy + depth, r * 1.6, r, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "rgba(255,255,255,0.05)";
      ctx.beginPath();
      ctx.ellipse(x - r * 0.3, gy + depth - r * 0.4, r, r * 0.7, 0, 0, Math.PI * 2);
      ctx.fill();
    }

    // затемнение глубины
    const dg = ctx.createLinearGradient(0, cam.top + (worldH || 0) * 0.2, 0, bottom);
    dg.addColorStop(0, "rgba(0,0,0,0)");
    dg.addColorStop(1, "rgba(0,0,0,0.55)");
    ctx.fillStyle = dg;
    surfacePath(ctx, terrain, left, right, 12);
    ctx.lineTo(right, bottom);
    ctx.lineTo(left, bottom);
    ctx.closePath();
    ctx.fill();
  }

  /* ---------------- растительность ---------------- */

  function drawTree(ctx, x, groundY, scale, hue, alpha) {
    ctx.save();
    ctx.translate(x, groundY);
    ctx.scale(scale, scale);
    ctx.globalAlpha = alpha;

    // ствол
    ctx.fillStyle = hue.trunk;
    ctx.beginPath();
    ctx.moveTo(-3.5, 0);
    ctx.lineTo(-2, -44);
    ctx.lineTo(2, -44);
    ctx.lineTo(3.5, 0);
    ctx.closePath();
    ctx.fill();

    // ветки
    ctx.strokeStyle = hue.trunk;
    ctx.lineWidth = 2.4;
    ctx.beginPath();
    ctx.moveTo(0, -28);
    ctx.lineTo(-11, -38);
    ctx.moveTo(0, -34);
    ctx.lineTo(10, -44);
    ctx.stroke();

    // крона
    const blobs = [
      [0, -58, 20],
      [-14, -46, 14],
      [15, -48, 15],
      [-6, -70, 13],
      [9, -68, 12],
    ];
    for (const [bx, by, r] of blobs) {
      ctx.beginPath();
      ctx.arc(bx, by, r, 0, Math.PI * 2);
      ctx.fillStyle = hue.leafDark;
      ctx.fill();
    }
    for (const [bx, by, r] of blobs) {
      ctx.beginPath();
      ctx.arc(bx - r * 0.22, by - r * 0.26, r * 0.72, 0, Math.PI * 2);
      ctx.fillStyle = hue.leaf;
      ctx.fill();
    }
    ctx.restore();
  }

  function drawBush(ctx, x, groundY, scale, hue, alpha) {
    ctx.save();
    ctx.translate(x, groundY);
    ctx.scale(scale, scale);
    ctx.globalAlpha = alpha;
    const blobs = [
      [0, -8, 10],
      [-9, -5, 7.5],
      [9, -6, 8],
      [0, -15, 7],
    ];
    for (const [bx, by, r] of blobs) {
      ctx.beginPath();
      ctx.arc(bx, by, r, 0, Math.PI * 2);
      ctx.fillStyle = hue.leafDark;
      ctx.fill();
    }
    for (const [bx, by, r] of blobs) {
      ctx.beginPath();
      ctx.arc(bx - r * 0.25, by - r * 0.3, r * 0.66, 0, Math.PI * 2);
      ctx.fillStyle = hue.leaf;
      ctx.fill();
    }
    ctx.restore();
  }

  const FAR_TREE = { trunk: "#3f5a44", leaf: "#5d7f60", leafDark: "#4a6a4f" };
  const NEAR_TREE = { trunk: "#4a3a29", leaf: "#5a8a4e", leafDark: "#487240" };
  const BUSH_HUE = { leaf: "#5f9550", leafDark: "#4c7a42" };

  /** деревья на заднем плане (за рельефом) */
  function drawBackgroundProps(ctx, cam, terrain) {
    const left = cam.left - 200;
    const right = cam.right + 200;
    const spacing = 150;
    const start = Math.floor(left / spacing) - 1;

    for (let i = start; i < start + Math.ceil((right - left) / spacing) + 3; i++) {
      const wx = i * spacing + hash(i * 31) * 110;
      if (wx < 0 || wx > terrain.width) continue;
      const kind = hash(i * 17);
      const scale = 1.15 + hash(i * 7) * 0.95;
      const gy = terrain.groundY(wx) + 6;
      if (kind < 0.62) {
        drawTree(ctx, wx, gy, scale, FAR_TREE, 0.78);
      } else if (kind < 0.86) {
        drawBush(ctx, wx, gy, scale * 0.8, BUSH_HUE, 0.7);
      } else {
        // камень
        ctx.globalAlpha = 0.55;
        ctx.fillStyle = "#6f757c";
        ctx.beginPath();
        ctx.ellipse(wx, gy - 4 * scale, 10 * scale, 6 * scale, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.globalAlpha = 1;
      }
    }
  }

  /** трава перед персонажем */
  function drawForeground(ctx, cam, terrain) {
    const spacing = 120;
    const left = cam.left - 60;
    const right = cam.right + 60;
    const start = Math.floor(left / spacing) - 1;
    for (let i = start; i < start + Math.ceil((right - left) / spacing) + 3; i++) {
      const wx = i * spacing + hash(i * 53) * 80;
      if (hash(i * 11) < 0.55) continue;
      const gy = terrain.groundY(wx) + 3;
      const s = 0.8 + hash(i * 23) * 0.5;
      ctx.strokeStyle = hash(i * 3) > 0.5 ? "#4f8a3f" : "#427a36";
      ctx.lineWidth = 2 * s;
      for (let k = -2; k <= 2; k++) {
        const bend = k * 3.2 + (hash(i * 5 + k) - 0.5) * 3;
        const hgt = (10 + hash(i * 7 + k) * 12) * s;
        ctx.beginPath();
        ctx.moveTo(wx + k * 2.4, gy);
        ctx.quadraticCurveTo(wx + k * 2.4 + bend * 0.4, gy - hgt * 0.6, wx + k * 2.4 + bend, gy - hgt);
        ctx.stroke();
      }
    }
  }

  root.Render = { drawSky, drawTerrain, drawBackgroundProps, drawForeground };
})(typeof globalThis !== "undefined" ? globalThis : this);
