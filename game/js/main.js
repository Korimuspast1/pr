(function () {
  "use strict";

  const canvas = document.getElementById("world");
  const ctx = canvas.getContext("2d");
  const { WORLD_W, WORLD_H } = window.WORLD_CONST;

  /* ---------------- мир ---------------- */

  const terrain = new window.Terrain(20261030);
  const world = new window.Physics.World(terrain);
  const effects = new window.Effects(terrain);
  world.effects = effects;

  const player = new window.Human(world, 760, {
    name: "Игрок",
    colors: {
      skin: "#e6b58f",
      shirt: "#3f6fa8",
      pants: "#2f3b4d",
      hair: "#2f2620",
      shoe: "#1d222b",
    },
  });

  const npcs = [];
  const npcSpots = [1500, 2300, 3050];
  for (let i = 0; i < npcSpots.length; i++) {
    npcs.push(
      new window.Human(world, npcSpots[i], {
        name: "NPC " + (i + 1),
        facing: i % 2 === 0 ? -1 : 1,
        colors: [
          { shirt: "#a8503f", pants: "#333a44", hair: "#1f1a16" },
          { shirt: "#4a7d5a", pants: "#2b3038", hair: "#40301f" },
          { shirt: "#6e5aa8", pants: "#37303f", hair: "#241d18" },
        ][i % 3],
      })
    );
  }

  const humans = [player].concat(npcs);

  /* ---------------- камера ---------------- */

  const cam = {
    x: player.rootX,
    y: player.rootY - 40,
    zoom: 1.45,
    left: 0,
    right: 0,
    top: 0,
    bottom: 0,
  };

  let viewW = 0;
  let viewH = 0;
  let dpr = 1;

  function resize() {
    dpr = Math.min(window.devicePixelRatio || 1, 2);
    viewW = canvas.clientWidth;
    viewH = canvas.clientHeight;
    canvas.width = Math.round(viewW * dpr);
    canvas.height = Math.round(viewH * dpr);
  }

  function updateCameraBounds() {
    cam.left = cam.x - viewW / (2 * cam.zoom);
    cam.right = cam.x + viewW / (2 * cam.zoom);
    cam.top = cam.y - viewH / (2 * cam.zoom);
    cam.bottom = cam.y + viewH / (2 * cam.zoom);
  }

  /* ---------------- ввод ---------------- */

  const keys = new Set();
  const inputState = { left: false, right: false, jump: false, run: false };
  let jumpPressed = false;

  window.addEventListener("keydown", (e) => {
    const k = e.key.toLowerCase();
    if (["arrowleft", "arrowright", "arrowup", "arrowdown", " ", "spacebar"].includes(k)) e.preventDefault();

    if (k === " " || k === "spacebar" || k === "arrowup" || k === "w") jumpPressed = true;
    keys.add(k);
    if (k === "f1") {
      player.debugSkeleton = !player.debugSkeleton;
      for (const h of humans) h.debugSkeleton = player.debugSkeleton;
    }
  });

  window.addEventListener("keyup", (e) => keys.delete(e.key.toLowerCase()));
  window.addEventListener("blur", () => keys.clear());

  canvas.addEventListener(
    "wheel",
    (e) => {
      e.preventDefault();
      const factor = Math.exp(-e.deltaY * 0.0012);
      cam.zoom = Math.max(0.4, Math.min(2.4, cam.zoom * factor));
    },
    { passive: false }
  );

  /* ---------------- мышь: хватать и бросать тела ---------------- */

  const mouse = { x: 0, y: 0, wx: 0, wy: 0, down: false, grabVelX: 0, grabVelY: 0, hover: null };

  function screenToWorld(sx, sy) {
    return {
      x: cam.x + (sx - viewW / 2) / cam.zoom,
      y: cam.y + (sy - viewH / 2) / cam.zoom,
    };
  }

  function pickPart(wx, wy) {
    let best = null;
    let bestD = 34;
    for (const h of humans) {
      for (const key in h.parts) {
        const p = h.parts[key];
        const d = Math.hypot(p.x - wx, p.y - wy);
        if (d < bestD) {
          bestD = d;
          best = { human: h, part: p, key };
        }
      }
    }
    return best;
  }

  canvas.addEventListener("pointerdown", (e) => {
    const rect = canvas.getBoundingClientRect();
    const w = screenToWorld(e.clientX - rect.left, e.clientY - rect.top);
    const hit = pickPart(w.x, w.y);
    mouse.down = true;
    if (!hit) return;
    const h = hit.human;
    // живого человека хватание роняет в рэгдолл (как в People Playground)
    if (h.mode === "alive") {
      if (h === player) return;
      h.knockdown(2.5, 0, 0);
      h.update(1 / 120, {});
    }
    world.grab(hit.part);
    canvas.setPointerCapture?.(e.pointerId);
    canvas.style.cursor = "grabbing";
  });

  canvas.addEventListener("pointermove", (e) => {
    const rect = canvas.getBoundingClientRect();
    const w = screenToWorld(e.clientX - rect.left, e.clientY - rect.top);
    mouse.wx = w.x;
    mouse.wy = w.y;
    mouse.x = e.clientX - rect.left;
    mouse.y = e.clientY - rect.top;
    if (!mouse.down) {
      mouse.hover = pickPart(w.x, w.y);
      canvas.style.cursor = mouse.hover ? "grab" : "default";
    }
  });

  function endPointer() {
    if (world.grabbed) {
      world.release(mouse.grabVelX, mouse.grabVelY);
    }
    mouse.down = false;
    canvas.style.cursor = "default";
  }

  canvas.addEventListener("pointerup", endPointer);
  canvas.addEventListener("pointercancel", endPointer);
  canvas.addEventListener("pointerleave", () => {
    if (mouse.down) endPointer();
    mouse.hover = null;
  });

  function updateGrab() {
    if (!world.grabbed) return;
    const p = world.grabbed;
    const beforeX = p.x;
    const beforeY = p.y;
    world.moveGrab(mouse.wx, mouse.wy);
    // скорость броска
    const vx = (p.x - beforeX) * 1.4;
    const vy = (p.y - beforeY) * 1.4;
    mouse.grabVelX = mouse.grabVelX * 0.6 + vx * 0.4;
    mouse.grabVelY = mouse.grabVelY * 0.6 + vy * 0.4;
  }

  function readInput() {
    inputState.left = keys.has("a") || keys.has("arrowleft");
    inputState.right = keys.has("d") || keys.has("arrowright");
    inputState.run = keys.has("shift");
    inputState.jump = jumpPressed;
    jumpPressed = false;
    return inputState;
  }

  /* ---------------- HUD ---------------- */

  const hpFill = document.getElementById("hp-fill");
  const hpText = document.getElementById("hp-text");
  const stateText = document.getElementById("state-text");
  const coordsEl = document.getElementById("coords");
  const fpsEl = document.getElementById("fps");

  /* ---------------- цикл ---------------- */

  const FIXED = 1 / 120;
  let acc = 0;
  let last = performance.now();
  let fpsSmoothed = 60;

  function step(dt) {
    const input = readInput();
    for (const h of humans) h.update(dt, h === player ? input : {});
    updateGrab();
    world.step(dt);
    effects.update(dt);
  }

  function followCamera(dt) {
    // в рэгдолле/смерти камера едет за физическим телом
    const px = player.mode === "alive" ? player.rootX : player.pelvis.x;
    const py = player.mode === "alive" ? player.rootY : player.pelvis.y;
    const targetX = px + (player.mode === "alive" ? player.facing * 40 + player.rootVx * 0.22 : 0);
    const targetY = py - (player.mode === "alive" ? 70 : 30);
    const kx = 1 - Math.pow(0.0012, dt);
    const ky = 1 - Math.pow(0.004, dt);
    cam.x += (targetX - cam.x) * kx;
    cam.y += (targetY - cam.y) * ky;

    const halfW = viewW / (2 * cam.zoom);
    const halfH = viewH / (2 * cam.zoom);
    cam.x = Math.max(halfW - 60, Math.min(WORLD_W - halfW + 60, cam.x));
    cam.y = Math.max(halfH * 0.2, Math.min(WORLD_H - halfH * 0.1, cam.y));
  }

  function render() {
    updateCameraBounds();

    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    window.Render.drawSky(ctx, cam, viewW, viewH);

    ctx.save();
    ctx.translate(viewW / 2, viewH / 2);
    ctx.scale(cam.zoom, cam.zoom);
    ctx.translate(-cam.x, -cam.y);

    window.Render.drawBackgroundProps(ctx, cam, terrain);
    window.Render.drawTerrain(ctx, cam, terrain, WORLD_H);
    effects.drawGround(ctx, cam);

    // персонажи (сначала дальние)
    const sorted = humans.slice().sort((a, b) => a.pelvis.y - b.pelvis.y);
    for (const h of sorted) h.draw(ctx, cam);

    effects.drawParticles(ctx, cam);
    window.Render.drawForeground(ctx, cam, terrain);

    // курсор-захват
    if (world.grabbed) {
      const p = world.grabbed;
      ctx.strokeStyle = "rgba(255,255,255,0.55)";
      ctx.lineWidth = 2 / cam.zoom;
      ctx.beginPath();
      ctx.arc(p.x, p.y, 9 + Math.sin(performance.now() / 160) * 1.5, 0, Math.PI * 2);
      ctx.stroke();
    } else if (mouse.hover) {
      const p = mouse.hover.part;
      ctx.strokeStyle = "rgba(255,255,255,0.28)";
      ctx.lineWidth = 1.5 / cam.zoom;
      ctx.beginPath();
      ctx.arc(p.x, p.y, 10, 0, Math.PI * 2);
      ctx.stroke();
    }

    // отладка: границы мира
    if (player.debugSkeleton) {
      ctx.strokeStyle = "rgba(0,255,255,0.35)";
      ctx.lineWidth = 2;
      ctx.strokeRect(0, 0, WORLD_W, WORLD_H);
    }

    ctx.restore();

    // лёгкая виньетка
    const vg = ctx.createRadialGradient(
      viewW / 2,
      viewH / 2,
      Math.min(viewW, viewH) * 0.45,
      viewW / 2,
      viewH / 2,
      Math.max(viewW, viewH) * 0.8
    );
    vg.addColorStop(0, "rgba(0,0,0,0)");
    vg.addColorStop(1, "rgba(10,16,26,0.35)");
    ctx.fillStyle = vg;
    ctx.fillRect(0, 0, viewW, viewH);
  }

  function updateHud() {
    const hp = Math.max(0, player.health) / player.maxHealth;
    if (hpFill) {
      hpFill.style.width = (hp * 100).toFixed(1) + "%";
      hpFill.style.background = hp > 0.6 ? "#5ad46a" : hp > 0.3 ? "#e8c14a" : "#e2513f";
    }
    if (hpText) hpText.textContent = Math.max(0, Math.round(player.health)) + " / 100";
    if (stateText) {
      stateText.textContent =
        player.mode === "alive"
          ? player.airborne
            ? "в воздухе"
            : Math.abs(player.rootVx) > 20
            ? "идёт"
            : "стоит"
          : player.mode === "dead"
          ? "мёртв"
          : "рэгдолл";
    }
    if (coordsEl) {
      coordsEl.textContent = `x: ${Math.round(player.rootX)}  ·  y: ${Math.round(player.rootY)}`;
    }
    if (fpsEl) fpsEl.textContent = Math.round(fpsSmoothed) + " fps";
  }

  function frame(now) {
    let dt = (now - last) / 1000;
    last = now;
    if (dt > 0.25) dt = 0.25;
    fpsSmoothed += (1 / Math.max(dt, 0.0001) - fpsSmoothed) * 0.05;

    acc += dt;
    let guard = 0;
    while (acc >= FIXED && guard < 8) {
      step(FIXED);
      acc -= FIXED;
      guard++;
    }
    if (guard >= 8) acc = 0;

    followCamera(dt);
    render();
    updateHud();
    requestAnimationFrame(frame);
  }

  /* ---------------- старт ---------------- */

  window.addEventListener("resize", resize);
  resize();
  requestAnimationFrame((t) => {
    last = t;
    requestAnimationFrame(frame);
  });

  // экспорт для отладки из консоли
  window.game = { world, terrain, effects, player, humans, cam, keys };
})();
