import * as THREE from '../vendor/three.module.js';
import { buildMap, queryColliders } from './map.js';
import { rand, randInt, pick, chance, clamp, hash01, TAU, v3 } from './util3.js';

const BLOOD = [0x8a0e14, 0x6f070c, 0x9d1219, 0x5c0409, 0x7e0b11];
const MAX_DECALS = 900;
const MAX_PARTICLES = 2600;

/**
 * The arena: ground, ruins, light, sky, plus all the blood / debris systems.
 */
const HAS_DOM = typeof document !== 'undefined' && typeof document.createElement === 'function';

// Textures are generated procedurally on a 2D canvas; without a DOM (Node tests)
// the world still builds, just with flat materials.
function canvasEl(w, h) {
  if (!HAS_DOM) return null;
  const c = document.createElement('canvas');
  c.width = w;
  c.height = h;
  return c;
}

function ctx2d(c) {
  if (!c) return null;
  try {
    const g = c.getContext('2d');
    return g && typeof g.createRadialGradient === 'function' ? g : null;
  } catch {
    return null;
  }
}

export class World {
  constructor(scene, renderer, audio) {
    this.scene = scene;
    this.renderer = renderer || null;
    this.audio = audio;
    this.time = 0;

    this.buildSky();
    this.map = buildMap(scene);
    this.colliders = this.map.colliders;
    this.spawnPoints = this.map.spawnPoints;
    this.bounds = this.map.bounds;
    this.lamps = this.map.lamps;
    this.fires = this.map.fires;
    this._near = [];
    this.buildLights();
    this.buildBlood();
    this.buildTracers();
  }

  /* ------------------------------------------------------------ */
  /*  ENVIRONMENT                                                  */
  /* ------------------------------------------------------------ */

  buildSky() {
    const g = new THREE.BufferGeometry();
    const count = 900;
    const pos = new Float32Array(count * 3);
    for (let i = 0; i < count; i++) {
      const theta = hash01(i * 3.1) * TAU;
      const phi = Math.acos(hash01(i * 7.7) * 0.85);
      const r = 90;
      pos[i * 3] = Math.sin(phi) * Math.cos(theta) * r;
      pos[i * 3 + 1] = Math.cos(phi) * r + 6;
      pos[i * 3 + 2] = Math.sin(phi) * Math.sin(theta) * r;
    }
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    const m = new THREE.PointsMaterial({ color: 0xcfd8e6, size: 0.28, sizeAttenuation: true, transparent: true, opacity: 0.75, depthWrite: false });
    this.stars = new THREE.Points(g, m);
    this.scene.add(this.stars);

    // moon disc
    const c = canvasEl(128, 128);
    const ctx = ctx2d(c);
    if (ctx) {
      const grd = ctx.createRadialGradient(64, 64, 6, 64, 64, 64);
      grd.addColorStop(0, 'rgba(255,250,235,1)');
      grd.addColorStop(0.35, 'rgba(250,242,220,0.95)');
      grd.addColorStop(0.45, 'rgba(220,225,240,0.18)');
      grd.addColorStop(1, 'rgba(200,210,230,0)');
      ctx.fillStyle = grd;
      ctx.fillRect(0, 0, 128, 128);
      const tex = new THREE.CanvasTexture(c);
      const moon = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, transparent: true, depthWrite: false, fog: false }));
      moon.scale.set(26, 26, 1);
      moon.position.set(-45, 40, -75);
      this.scene.add(moon);
    }

    this.scene.fog = new THREE.FogExp2(0x0b111a, 0.026);
  }

  /* ---------------- collisions ---------------- */

  queryColliders(x, z, r) {
    return queryColliders(this.map.grid, x, z, r, this._near);
  }

  // true when a standing body of radius r would overlap something solid
  blocked(x, z, r = 0.32) {
    const list = queryColliders(this.map.grid, x, z, r, this._near);
    for (const c of list) {
      if (c.y0 > 1.7) continue;
      if (x + r > c.x0 && x - r < c.x1 && z + r > c.z0 && z - r < c.z1) return true;
    }
    return false;
  }

  // pushes a point out of solid geometry (walls, cars, crates…)
  resolvePoint(p, radius) {
    const list = queryColliders(this.map.grid, p.x, p.z, radius + 0.15, this._near);
    let hit = false;
    for (const c of list) {
      if (p.y - radius > c.y1 || p.y + radius < c.y0) continue;
      const x0 = c.x0 - radius, x1 = c.x1 + radius;
      const z0 = c.z0 - radius, z1 = c.z1 + radius;
      if (p.x <= x0 || p.x >= x1 || p.z <= z0 || p.z >= z1) continue;
      const dxl = p.x - x0, dxr = x1 - p.x;
      const dzl = p.z - z0, dzr = z1 - p.z;
      const m = Math.min(dxl, dxr, dzl, dzr);
      if (m === dxl) p.x = x0;
      else if (m === dxr) p.x = x1;
      else if (m === dzl) p.z = z0;
      else p.z = z1;
      hit = true;
    }
    return hit;
  }

  buildLights() {
    const hemi = new THREE.HemisphereLight(0x44536e, 0x171a21, 1.15);
    this.scene.add(hemi);
    this.scene.add(new THREE.AmbientLight(0x8090a8, 0.22));

    const key = new THREE.DirectionalLight(0xa9c2e8, 1.75);
    key.position.set(-16, 24, -12);
    key.castShadow = true;
    key.shadow.mapSize.set(2048, 2048);
    key.shadow.camera.left = -18;
    key.shadow.camera.right = 18;
    key.shadow.camera.top = 18;
    key.shadow.camera.bottom = -18;
    key.shadow.camera.near = 1;
    key.shadow.camera.far = 70;
    key.shadow.bias = -0.0015;
    key.shadow.normalBias = 0.02;
    this.scene.add(key);
    this.keyLight = key;

    const rim = new THREE.DirectionalLight(0x6b7fa8, 0.5);
    rim.position.set(14, 10, 16);
    this.scene.add(rim);
  }

  /* ------------------------------------------------------------ */
  /*  BLOOD & DEBRIS SYSTEMS                                       */
  /* ------------------------------------------------------------ */

  buildBlood() {
    // ---- particles (points with per-vertex size + colour)
    const geo = new THREE.BufferGeometry();
    this.pCount = MAX_PARTICLES;
    this.pPos = new Float32Array(this.pCount * 3);
    this.pCol = new Float32Array(this.pCount * 3);
    this.pSize = new Float32Array(this.pCount);
    this.pVel = new Float32Array(this.pCount * 3);
    this.pLife = new Float32Array(this.pCount);
    this.pMax = new Float32Array(this.pCount);
    this.pKind = new Uint8Array(this.pCount); // 0 droplet, 1 mist, 2 spark, 3 dust
    this.pNext = 0;

    geo.setAttribute('position', new THREE.BufferAttribute(this.pPos, 3));
    geo.setAttribute('aColor', new THREE.BufferAttribute(this.pCol, 3));
    geo.setAttribute('size', new THREE.BufferAttribute(this.pSize, 1));
    geo.setDrawRange(0, 0);

    const pixelRatio = this.renderer && this.renderer.getPixelRatio ? this.renderer.getPixelRatio() : 1;
    const mat = new THREE.ShaderMaterial({
      transparent: true,
      depthWrite: false,
      uniforms: { uPixelRatio: { value: pixelRatio } },
      vertexShader: `
        attribute float size;
        attribute vec3 aColor;
        varying vec3 vColor;
        uniform float uPixelRatio;
        void main() {
          vColor = aColor;
          vec4 mv = modelViewMatrix * vec4(position, 1.0);
          gl_PointSize = size * uPixelRatio * (420.0 / max(0.001, -mv.z));
          gl_Position = projectionMatrix * mv;
        }`,
      fragmentShader: `
        varying vec3 vColor;
        void main() {
          vec2 uv = gl_PointCoord - vec2(0.5);
          float d = length(uv);
          if (d > 0.5) discard;
          float a = smoothstep(0.5, 0.18, d);
          gl_FragColor = vec4(vColor, a);
        }`,
    });
    this.points = new THREE.Points(geo, mat);
    this.points.frustumCulled = false;
    this.scene.add(this.points);

    // ---- decals
    const dgeo = new THREE.CircleGeometry(1, 14);
    dgeo.rotateX(-Math.PI / 2);
    const dmat = new THREE.MeshBasicMaterial({
      color: 0x5c0409, transparent: true, opacity: 0.72, depthWrite: false,
      polygonOffset: true, polygonOffsetFactor: -4, polygonOffsetUnits: -4,
    });
    this.decals = new THREE.InstancedMesh(dgeo, dmat, MAX_DECALS);
    this.decals.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.decals.count = 0;
    this.decals.frustumCulled = false;
    this.scene.add(this.decals);
    this.decalData = [];
    this.decalNext = 0;
    this.decalCount = 0;
    this.decalDummy = new THREE.Object3D();

    // ---- flesh / bone chunks
    const cgeo = new THREE.IcosahedronGeometry(1, 0);
    const cmat = new THREE.MeshStandardMaterial({ roughness: 0.7, metalness: 0.05 });
    this.chunks = new THREE.InstancedMesh(cgeo, cmat, 320);
    this.chunks.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.chunks.count = 0;
    this.chunks.frustumCulled = false;
    this.chunks.castShadow = false;
    if (this.chunks.instanceColor === undefined) {
      this.chunks.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(320 * 3), 3);
    }
    this.scene.add(this.chunks);
    this.chunkData = [];
    this.chunkDummy = new THREE.Object3D();
    this.chunkColor = new THREE.Color();

    this.geoDirty = true;
    this.count = 0;
  }

  spawnParticle(kind, pos, vel, size, life, color) {
    let i = this.pNext;
    for (let tries = 0; tries < 12; tries++) {
      if (this.pLife[i] <= 0) break;
      i = (i + 1) % this.pCount;
    }
    this.pNext = (i + 1) % this.pCount;
    this.pPos[i * 3] = pos.x; this.pPos[i * 3 + 1] = pos.y; this.pPos[i * 3 + 2] = pos.z;
    this.pVel[i * 3] = vel.x; this.pVel[i * 3 + 1] = vel.y; this.pVel[i * 3 + 2] = vel.z;
    const c = new THREE.Color(color);
    this.pCol[i * 3] = c.r; this.pCol[i * 3 + 1] = c.g; this.pCol[i * 3 + 2] = c.b;
    this.pSize[i] = size;
    this.pLife[i] = life;
    this.pMax[i] = life;
    this.pKind[i] = kind;
    this.geoDirty = true;
  }

  // Directional spray of blood.
  spray(pos, dir, opts = {}) {
    const count = opts.count != null ? opts.count : 10;
    const speed = opts.speed != null ? opts.speed : 5;
    const spread = opts.spread != null ? opts.spread : 0.6;
    const size = opts.size != null ? opts.size : 0.035;
    const base = normVec(dir);
    for (let i = 0; i < count; i++) {
      const v = jitterDir(base, spread, speed * rand(0.3, 1.3));
      const p = addVec(pos, { x: rand(-0.02, 0.02), y: rand(-0.02, 0.02), z: rand(-0.02, 0.02) });
      this.spawnParticle(0, p, v, size * rand(0.6, 1.5), rand(0.5, 1.4), pick(BLOOD));
    }
  }

  mist(pos, dir, count = 6, spread = 0.9) {
    const base = normVec(dir);
    for (let i = 0; i < count; i++) {
      const v = jitterDir(base, spread, rand(0.6, 3.2));
      this.spawnParticle(1, pos, v, rand(0.12, 0.34), rand(0.25, 0.6), pick(BLOOD));
    }
  }

  dust(pos, n = 6, color = 0x8a8074) {
    for (let i = 0; i < n; i++) {
      this.spawnParticle(3, pos, { x: rand(-0.7, 0.7), y: rand(0.2, 1.1), z: rand(-0.7, 0.7) }, rand(0.05, 0.16), rand(0.25, 0.7), color);
    }
  }

  spark(pos, dir, n = 5) {
    const base = normVec(dir);
    for (let i = 0; i < n; i++) {
      const v = jitterDir(base, 0.7, rand(1.5, 5));
      this.spawnParticle(2, pos, v, rand(0.02, 0.05), rand(0.08, 0.2), chance(0.5) ? 0xffd27a : 0xfff3c4);
    }
  }

  chunk(pos, dir, opts = {}) {
    const kind = opts.kind || 'meat';
    const count = opts.count || 1;
    for (let i = 0; i < count; i++) {
      const v = jitterDir(normVec(dir), 1.0, rand(1, 4.5));
      const color = kind === 'bone' ? (chance(0.5) ? 0xefe7d4 : 0xd3c6ae) : (chance(0.7) ? 0x5e0a0e : 0x320507);
      this.chunkData.push({
        pos: { x: pos.x + rand(-0.03, 0.03), y: pos.y + rand(-0.03, 0.03), z: pos.z + rand(-0.03, 0.03) },
        vel: v,
        size: kind === 'bone' ? rand(0.02, 0.05) : rand(0.025, 0.07),
        rot: { x: rand(0, TAU), y: rand(0, TAU), z: rand(0, TAU) },
        spin: { x: rand(-8, 8), y: rand(-8, 8), z: rand(-8, 8) },
        color,
        kind,
        life: rand(14, 30),
        settled: false,
      });
    }
  }

  decal(x, z, r, alpha = 0.7) {
    if (this.decalCount < MAX_DECALS) {
      this.decalCount++;
    }
    const idx = this.decalNext;
    this.decalNext = (this.decalNext + 1) % MAX_DECALS;
    this.decalData[idx] = {
      x, z,
      r: clamp(r, 0.02, 3.2),
      rot: rand(0, TAU),
      y: 0.006 + (idx % 40) * 0.0006,
      color: new THREE.Color(pick(BLOOD)),
    };
    const d = this.decalData[idx];
    this.decalDummy.position.set(d.x, d.y, d.z);
    this.decalDummy.rotation.set(0, d.rot, 0);
    this.decalDummy.scale.set(d.r, 1, d.r * rand(0.55, 0.95));
    this.decalDummy.updateMatrix();
    this.decals.setMatrixAt(idx, this.decalDummy.matrix);
    this.decals.setColorAt(idx, d.color);
    this.decals.count = Math.max(this.decalCount, idx + 1);
    this.decals.instanceMatrix.needsUpdate = true;
    if (this.decals.instanceColor) this.decals.instanceColor.needsUpdate = true;
  }

  clearBlood() {
    this.decalCount = 0;
    this.decals.count = 0;
    this.decalData.length = 0;
    this.pLife.fill(0);
    this.points.geometry.setDrawRange(0, 0);
    this.chunkData.length = 0;
    this.chunks.count = 0;
  }

  /* ------------------------------------------------------------ */
  /*  TRACERS / FLASHES                                            */
  /* ------------------------------------------------------------ */

  buildTracers() {
    this.tracers = [];
    this.flashes = [];
    const geo = new THREE.CylinderGeometry(1, 1, 1, 5, 1, true);
    for (let i = 0; i < 40; i++) {
      const mat = new THREE.MeshBasicMaterial({ color: 0xffd9a0, transparent: true, opacity: 0, depthWrite: false });
      const m = new THREE.Mesh(geo, mat);
      m.visible = false;
      this.scene.add(m);
      this.tracers.push({ mesh: m, life: 0, max: 1 });
    }
    this.tracerIdx = 0;

    const flashTex = makeGlowTexture();
    for (let i = 0; i < 10; i++) {
      const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: flashTex, color: 0xffe6b0, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, opacity: 0 }));
      s.scale.set(1.2, 1.2, 1);
      s.visible = false;
      this.scene.add(s);
      this.flashes.push({ sprite: s, life: 0 });
    }
    this.flashIdx = 0;
  }

  tracer(from, to, opts = {}) {
    const t = this.tracers[this.tracerIdx];
    this.tracerIdx = (this.tracerIdx + 1) % this.tracers.length;
    const dx = to.x - from.x, dy = to.y - from.y, dz = to.z - from.z;
    const length = Math.hypot(dx, dy, dz) || 0.01;
    t.mesh.visible = true;
    t.mesh.position.set((from.x + to.x) / 2, (from.y + to.y) / 2, (from.z + to.z) / 2);
    t.mesh.scale.set(opts.radius || 0.012, length, opts.radius || 0.012);
    const q = new THREE.Quaternion();
    q.setFromUnitVectors(new THREE.Vector3(0, 1, 0), new THREE.Vector3(dx, dy, dz).normalize());
    t.mesh.quaternion.copy(q);
    t.mesh.material.color.set(opts.color || 0xffd9a0);
    t.mesh.material.opacity = 0.85;
    t.life = 0.07;
    t.max = 0.07;
    t.max = 0.07;
  }

  muzzleFlash(pos, scale = 1) {
    const f = this.flashes[this.flashIdx];
    this.flashIdx = (this.flashIdx + 1) % this.flashes.length;
    f.sprite.position.set(pos.x, pos.y, pos.z);
    f.sprite.scale.setScalar(1.5 * scale);
    f.sprite.visible = true;
    f.sprite.material.opacity = 1;
    f.life = 0.06;
  }

  /* ------------------------------------------------------------ */
  /*  UPDATE                                                       */
  /* ------------------------------------------------------------ */

  update(dt) {
    this.time += dt;

    // lamps hum, fires flicker
    if (this.lamps) {
      for (const l of this.lamps) {
        l.light.intensity = l.base * (0.94 + Math.sin(this.time * 7 + l.phase) * 0.03 + Math.random() * 0.04);
      }
    }
    if (this.fires) {
      for (const f of this.fires) {
        f.light.intensity = f.base * (0.72 + Math.sin(this.time * 9 + f.phase) * 0.18 + Math.random() * 0.16);
      }
    }

    // particles
    let visible = 0;
    for (let i = 0; i < this.pCount; i++) {
      if (this.pLife[i] <= 0) { this.pSize[i] = 0; continue; }
      this.pLife[i] -= dt;
      const i3 = i * 3;
      const kind = this.pKind[i];
      if (kind === 0) {
        this.pVel[i3 + 1] -= 9.81 * dt;
        this.pVel[i3] *= 0.999;
        this.pVel[i3 + 2] *= 0.999;
      } else if (kind === 1) {
        this.pVel[i3] *= 0.92; this.pVel[i3 + 1] *= 0.9; this.pVel[i3 + 2] *= 0.92;
        this.pSize[i] += dt * 0.16;
      } else if (kind === 2) {
        this.pVel[i3 + 1] -= 9.81 * dt;
      } else {
        this.pVel[i3 + 1] += dt * 0.4;
        this.pVel[i3] *= 0.95; this.pVel[i3 + 2] *= 0.95;
      }
      this.pPos[i3] += this.pVel[i3] * dt;
      this.pPos[i3 + 1] += this.pVel[i3 + 1] * dt;
      this.pPos[i3 + 2] += this.pVel[i3 + 2] * dt;

      if (this.pPos[i3 + 1] <= 0.006) {
        if (kind === 0) {
          this.decal(this.pPos[i3], this.pPos[i3 + 2], rand(0.035, 0.11) * (this.pSize[i] * 22), 0.75);
        }
        this.pLife[i] = 0;
        this.pSize[i] = 0;
        continue;
      }
      if (this.pLife[i] <= 0) { this.pSize[i] = 0; continue; }
      if (kind === 1) {
        this.pSize[i] *= 1 + dt * 1.2;
      }
      visible++;
    }
    this.points.geometry.setDrawRange(0, 0);
    const posAttr = this.points.geometry.getAttribute('position');
    const colAttr = this.points.geometry.getAttribute('aColor');
    const sizeAttr = this.points.geometry.getAttribute('size');
    let n = 0;
    for (let i = 0; i < this.pCount; i++) {
      if (this.pLife[i] <= 0) continue;
      posAttr.setXYZ(n, this.pPos[i * 3], this.pPos[i * 3 + 1], this.pPos[i * 3 + 2]);
      colAttr.setXYZ(n, this.pCol[i * 3], this.pCol[i * 3 + 1], this.pCol[i * 3 + 2]);
      const fade = clamp(this.pLife[i] / Math.max(0.05, this.pMax[i]), 0, 1);
      sizeAttr.setX(n, this.pSize[i] * (this.pKind[i] === 1 ? fade : 1));
      n++;
    }
    posAttr.needsUpdate = true;
    colAttr.needsUpdate = true;
    sizeAttr.needsUpdate = true;
    this.points.geometry.setDrawRange(0, n);
    this.points.geometry.attributes.position.needsUpdate = true;

    // chunks
    let ci = 0;
    for (let i = this.chunkData.length - 1; i >= 0; i--) {
      const c = this.chunkData[i];
      c.life -= dt;
      if (c.life <= 0) { this.chunkData.splice(i, 1); continue; }
      if (!c.settled) {
        c.vel.y -= 9.81 * dt;
        c.pos.x += c.vel.x * dt;
        c.pos.y += c.vel.y * dt;
        c.pos.z += c.vel.z * dt;
        c.rot.x += c.spin.x * dt;
        c.rot.y += c.spin.y * dt;
        c.rot.z += c.spin.z * dt;
        if (c.pos.y <= c.size) {
          c.pos.y = c.size;
          if (Math.abs(c.vel.y) > 0.8) {
            c.vel.y *= -0.25;
            c.vel.x *= 0.55;
            c.vel.z *= 0.55;
            this.decal(c.pos.x, c.pos.z, c.size * rand(2, 5), 0.5);
          } else {
            c.settled = true;
            c.pos.y = c.size * 0.8;
            this.decal(c.pos.x, c.pos.z, c.size * rand(1.6, 3.4), 0.55);
          }
        }
      }
    }
    // write instances (keep the newest ~300)
    const limit = Math.min(this.chunkData.length, 300);
    for (let i = this.chunkData.length - limit; i < this.chunkData.length; i++) {
      const c = this.chunkData[i];
      this.chunkDummy.position.set(c.pos.x, c.pos.y, c.pos.z);
      this.chunkDummy.rotation.set(c.rot.x, c.rot.y, c.rot.z);
      const s = c.size * (c.kind === 'bone' ? 1.25 : 1);
      this.chunkDummy.scale.set(s, s * (c.kind === 'bone' ? 1.5 : 0.8), s);
      this.chunkDummy.updateMatrix();
      this.chunks.setMatrixAt(ci, this.chunkDummy.matrix);
      this.chunkColor.set(c.color);
      this.chunks.setColorAt(ci, this.chunkColor);
      ci++;
    }
    this.chunks.count = ci;
    this.chunks.instanceMatrix.needsUpdate = true;
    if (this.chunks.instanceColor) this.chunks.instanceColor.needsUpdate = true;

    // tracers
    for (const t of this.tracers) {
      if (t.life > 0) {
        t.life -= dt;
        t.mesh.material.opacity = Math.max(0, t.life / t.max) * 0.85;
        if (t.life <= 0) t.mesh.visible = false;
      }
    }
    // flashes
    for (const f of this.flashes) {
      if (f.life > 0) {
        f.life -= dt;
        f.sprite.material.opacity = Math.max(0, f.life / 0.06);
        if (f.life <= 0) f.sprite.visible = false;
      }
    }
  }

  updateShadowFocus(target) {
    if (!this.keyLight) return;
    this.keyLight.position.set(target.x - 16, 24, target.z - 12);
    this.keyLight.target.position.set(target.x, 0, target.z);
    this.keyLight.target.updateMatrixWorld();
  }
}

/* helpers */
function normVec(v) {
  const l = Math.hypot(v.x, v.y, v.z) || 1;
  return { x: v.x / l, y: v.y / l, z: v.z / l };
}
function addVec(a, b) { return { x: a.x + b.x, y: a.y + b.y, z: a.z + b.z }; }
function jitterDir(base, spread, speed) {
  // random direction inside a cone around base
  const up = Math.abs(base.y) < 0.9 ? { x: 0, y: 1, z: 0 } : { x: 1, y: 0, z: 0 };
  const t1 = normVec({
    x: base.y * up.z - base.z * up.y,
    y: base.z * up.x - base.x * up.z,
    z: base.x * up.y - base.y * up.x,
  });
  const t2 = {
    x: base.y * t1.z - base.z * t1.y,
    y: base.z * t1.x - base.x * t1.z,
    z: base.x * t1.y - base.y * t1.x,
  };
  const a = Math.random() * TAU;
  const r = Math.random() * spread;
  const dx = Math.cos(a) * r, dy = Math.sin(a) * r;
  const d = normVec({
    x: base.x + t1.x * dx + t2.x * dy,
    y: base.y + t1.y * dx + t2.y * dy,
    z: base.z + t1.z * dx + t2.z * dy,
  });
  return { x: d.x * speed, y: d.y * speed, z: d.z * speed };
}

function makeGlowTexture() {
  const c = canvasEl(64, 64);
  const g = ctx2d(c);
  if (!g) return null;
  const grd = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  grd.addColorStop(0, 'rgba(255,255,235,1)');
  grd.addColorStop(0.35, 'rgba(255,214,140,0.85)');
  grd.addColorStop(1, 'rgba(255,150,40,0)');
  g.fillStyle = grd;
  g.fillRect(0, 0, 64, 64);
  const t = new THREE.CanvasTexture(c);
  return t;
}
