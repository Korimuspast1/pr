import * as THREE from './three.module.min.js';
import { hash } from './textures.js';

// ============================================================
//  Мир: небо, свет, МКАД (5+5 полос), ограждения, фонари,
//  деревья, путепроводы, зелёные вывески, км-столбики.
//  Игрок едет в +Z. Наши полосы: x = 0..18.75
// ============================================================

export const LANE_W = 3.75;
export const LANES_X = [1.875, 5.625, 9.375, 13.125, 16.875];
export const X_MIN = 0.85;    // левая граница (бетон МКАД)
export const X_MAX = 19.5;    // правая граница (отбойник)
const ONCOMING_X = [-5.075, -8.825, -12.575, -16.325, -20.075];

const ROAD_LEN = 1600;
const SUN_DIR = new THREE.Vector3(0.42, 0.72, 0.38).normalize();
const UP = new THREE.Vector3(0, 1, 0);

// «лента» периодических объектов вдоль дороги
class Belt {
  constructor(period, count, offset, place) {
    this.period = period; this.count = count; this.offset = offset;
    this.place = place; this.first = null;
  }
  update(pz, behind = 80) {
    const f = Math.floor((pz - behind) / this.period);
    if (f !== this.first) {
      this.first = f;
      for (let i = 0; i < this.count; i++) this.place(i, (f + i) * this.period + this.offset);
      return true;
    }
    return false;
  }
}

const skyVert = `
varying vec3 vDir;
void main() {
  vDir = normalize(position);
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}`;
const skyFrag = `
varying vec3 vDir;
uniform vec3 topColor;
uniform vec3 horizonColor;
uniform vec3 sunColor;
uniform vec3 sunDir;
void main() {
  float h = clamp(vDir.y, 0.0, 1.0);
  vec3 col = mix(horizonColor, topColor, pow(h, 0.5));
  float s = clamp(dot(normalize(vDir), normalize(sunDir)), 0.0, 1.0);
  col += sunColor * (pow(s, 800.0) * 1.4 + pow(s, 60.0) * 0.30 + pow(s, 6.0) * 0.10);
  gl_FragColor = vec4(col, 1.0);
}`;

export class World {
  constructor(scene, renderer, T) {
    this.scene = scene;
    this.T = T;
    this.camera = null;

    // ---------- свет ----------
    scene.fog = new THREE.Fog(0xe4ecf3, 120, 620);

    const hemi = new THREE.HemisphereLight(0xbcd8f0, 0x6f7d5c, 0.55);
    scene.add(hemi);

    const sun = new THREE.DirectionalLight(0xfff1dc, 2.6);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    sun.shadow.camera.near = 10;
    sun.shadow.camera.far = 320;
    sun.shadow.camera.left = -60; sun.shadow.camera.right = 60;
    sun.shadow.camera.top = 70; sun.shadow.camera.bottom = -30;
    sun.shadow.bias = -0.0004;
    sun.shadow.normalBias = 0.03;
    scene.add(sun);
    scene.add(sun.target);
    this.sun = sun;

    // ---------- небо ----------
    const skyMat = new THREE.ShaderMaterial({
      side: THREE.BackSide, depthWrite: false, fog: false,
      uniforms: {
        topColor: { value: new THREE.Color(0x4d95dd) },
        horizonColor: { value: new THREE.Color(0xe4ecf3) },
        sunColor: { value: new THREE.Color(0xfff3d9) },
        sunDir: { value: SUN_DIR.clone() },
      },
      vertexShader: skyVert, fragmentShader: skyFrag,
    });
    this.sky = new THREE.Mesh(new THREE.SphereGeometry(1500, 32, 16), skyMat);
    this.sky.frustumCulled = false;
    scene.add(this.sky);

    // ---------- env map (отражения на лаке) ----------
    const pmrem = new THREE.PMREMGenerator(renderer);
    const envScene = new THREE.Scene();
    const envSky = new THREE.Mesh(new THREE.SphereGeometry(20, 24, 12), skyMat);
    envScene.add(envSky);
    const envGround = new THREE.Mesh(
      new THREE.CircleGeometry(28, 24),
      new THREE.MeshBasicMaterial({ color: 0x55703d })
    );
    envGround.rotation.x = -Math.PI / 2;
    envGround.position.y = -0.6;
    envScene.add(envGround);
    scene.environment = pmrem.fromScene(envScene, 0.03).texture;
    pmrem.dispose();

    // ---------- облака ----------
    this.clouds = [];
    const cloudMat = new THREE.SpriteMaterial({ map: T.cloud, transparent: true, opacity: 0.85, depthWrite: false, fog: false });
    for (let i = 0; i < 12; i++) {
      const sp = new THREE.Sprite(cloudMat.clone());
      sp.material.rotation = Math.random() * Math.PI;
      const s = 90 + Math.random() * 120;
      sp.scale.set(s, s * 0.42, 1);
      sp.position.set(-500 + Math.random() * 1000, 130 + Math.random() * 110, Math.random() * 1100);
      this.clouds.push(sp);
      scene.add(sp);
    }

    // ---------- ДОРОГА ----------
    const roadMat = new THREE.MeshStandardMaterial({
      map: T.road, roughnessMap: T.roadRough, normalMap: T.roadNormal,
      roughness: 1, metalness: 0, envMapIntensity: 0.35,
    });
    T.road.repeat.set(1, ROAD_LEN / 15);
    T.roadRough.repeat.set(1, ROAD_LEN / 15);
    this.road = new THREE.Mesh(new THREE.PlaneGeometry(44.4, ROAD_LEN), roadMat);
    this.road.rotation.x = -Math.PI / 2;
    this.road.position.set(-1.5, 0, 0);
    this.road.receiveShadow = true;
    scene.add(this.road);

    // ---------- трава / обочины ----------
    const grassMat = new THREE.MeshStandardMaterial({ map: T.grass, roughness: 1, metalness: 0 });
    T.grass.repeat.set(55, ROAD_LEN / 8);
    const grassR = new THREE.Mesh(new THREE.PlaneGeometry(440, ROAD_LEN), grassMat);
    grassR.rotation.x = -Math.PI / 2;
    grassR.position.set(241, -0.06, 0);
    grassR.receiveShadow = true;
    scene.add(grassR);
    const grassL = new THREE.Mesh(new THREE.PlaneGeometry(440, ROAD_LEN), grassMat);
    grassL.rotation.x = -Math.PI / 2;
    grassL.position.set(-254, -0.06, 0);
    grassL.receiveShadow = true;
    scene.add(grassL);
    this.grass = [grassR, grassL];

    // грунтовая полоса у дороги
    const dirtMat = new THREE.MeshStandardMaterial({ color: 0x6f6046, roughness: 1 });
    for (const x of [21.6, -24.2]) {
      const d = new THREE.Mesh(new THREE.PlaneGeometry(2.6, ROAD_LEN), dirtMat);
      d.rotation.x = -Math.PI / 2;
      d.position.set(x, -0.02, 0);
      d.receiveShadow = true;
      scene.add(d);
    }

    // ---------- БЕТОННОЕ ОГРАЖДЕНИЕ (разделительное) ----------
    const jerseyShape = new THREE.Shape();
    jerseyShape.moveTo(-0.42, 0);
    jerseyShape.lineTo(-0.30, 0.10);
    jerseyShape.lineTo(-0.13, 0.62);
    jerseyShape.lineTo(-0.10, 0.86);
    jerseyShape.lineTo(0.10, 0.86);
    jerseyShape.lineTo(0.13, 0.62);
    jerseyShape.lineTo(0.30, 0.10);
    jerseyShape.lineTo(0.42, 0);
    jerseyShape.closePath();
    const jerseyGeo = new THREE.ExtrudeGeometry(jerseyShape, { depth: ROAD_LEN, bevelEnabled: false });
    jerseyGeo.translate(0, 0, -ROAD_LEN / 2);
    const concMat = new THREE.MeshStandardMaterial({ map: T.concrete, roughness: 0.92, metalness: 0 });
    T.concrete.repeat.set(1 / 2.6, 1 / 2.6);
    this.median = new THREE.Mesh(jerseyGeo, concMat);
    this.median.position.set(-1.7, 0, 0);
    this.median.castShadow = true;
    this.median.receiveShadow = true;
    scene.add(this.median);

    // сетка поверх бетона
    const gridMat = new THREE.MeshStandardMaterial({
      map: T.grid, transparent: true, alphaTest: 0.4, side: THREE.DoubleSide,
      color: 0x9aa894, roughness: 0.7, metalness: 0.4,
    });
    T.grid.repeat.set(240, 2.2);
    this.medianFence = new THREE.Mesh(new THREE.BoxGeometry(0.03, 1.15, ROAD_LEN), gridMat);
    this.medianFence.position.set(-1.7, 1.42, 0);
    scene.add(this.medianFence);

    // шумозащитные экраны (прозрачные секции)
    this.screens = [];
    const screenMat = new THREE.MeshPhysicalMaterial({
      color: 0x9fc4d4, transparent: true, opacity: 0.4,
      roughness: 0.12, metalness: 0.15, side: THREE.DoubleSide, envMapIntensity: 1.2,
    });
    for (let i = 0; i < 3; i++) {
      const grp = new THREE.Group();
      const panel = new THREE.Mesh(new THREE.BoxGeometry(0.05, 2.6, 200), screenMat);
      panel.position.set(-1.7, 2.75, 0);
      grp.add(panel);
      const frameMat = new THREE.MeshStandardMaterial({ color: 0x7d858c, roughness: 0.4, metalness: 0.8 });
      for (let k = -100; k <= 100; k += 10) {
        const post = new THREE.Mesh(new THREE.BoxGeometry(0.09, 2.75, 0.13), frameMat);
        post.position.set(-1.7, 2.6, k);
        post.castShadow = true;
        grp.add(post);
      }
      grp.position.z = i * 900 + 300;
      this.screens.push(grp);
      scene.add(grp);
    }

    // ---------- ОТБОЙНИК справа ----------
    const railMat = new THREE.MeshStandardMaterial({ color: 0xb9bec4, roughness: 0.38, metalness: 0.9 });
    this.rail = new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.32, ROAD_LEN), railMat);
    this.rail.position.set(20.15, 0.62, 0);
    this.rail.castShadow = true;
    scene.add(this.rail);
    this.rail2 = new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.2, ROAD_LEN), railMat);
    this.rail2.position.set(20.15, 0.30, 0);
    scene.add(this.rail2);
    // столбики отбойника
    const postGeo = new THREE.BoxGeometry(0.1, 0.72, 0.12);
    const postMat = new THREE.MeshStandardMaterial({ color: 0x8d949b, roughness: 0.5, metalness: 0.8 });
    this.railPosts = new THREE.InstancedMesh(postGeo, postMat, 340);
    this.railPosts.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    scene.add(this.railPosts);
    const _m = new THREE.Matrix4();
    this.railBelt = new Belt(4, 340, 0, (i, z) => {
      _m.makeTranslation(20.15, 0.36, z);
      this.railPosts.setMatrixAt(i, _m);
      this.railPosts.instanceMatrix.needsUpdate = true;
    });

    // ---------- ФОНАРИ ----------
    this.lamps = [];
    const lampMat = new THREE.MeshStandardMaterial({ color: 0x9aa1a8, roughness: 0.45, metalness: 0.85 });
    const headMat = new THREE.MeshStandardMaterial({ color: 0xb9c2c9, roughness: 0.5, metalness: 0.6, emissive: 0xfff6df, emissiveIntensity: 0.12 });
    for (let i = 0; i < 28; i++) {
      const grp = new THREE.Group();
      const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.07, 0.11, 10.6, 10), lampMat);
      pole.position.y = 5.3;
      pole.castShadow = true;
      grp.add(pole);
      const arm = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, 3.4, 8), lampMat);
      arm.rotation.z = Math.PI / 2;
      arm.position.set(1.6, 10.3, 0);
      grp.add(arm);
      const head = new THREE.Mesh(new THREE.BoxGeometry(0.85, 0.16, 0.3), headMat);
      head.position.set(3.1, 10.2, 0);
      grp.add(head);
      this.lamps.push(grp);
      scene.add(grp);
    }
    // правая сторона (рука фонаря в сторону дороги)
    this.lampBelt = new Belt(50, 14, 0, (i, z) => {
      const g = this.lamps[i];
      g.position.set(21.9, 0, z);
      g.rotation.y = Math.PI;
    });
    // левая сторона
    this.lampBeltL = new Belt(50, 14, 25, (i, z) => {
      const g = this.lamps[14 + i];
      g.position.set(-24.3, 0, z);
      g.rotation.y = 0;
    });

    // ---------- ДЕРЕВЬЯ ----------
    const N = 240, HALF = N / 2;
    const trunkGeo = new THREE.CylinderGeometry(0.13, 0.22, 2.6, 7);
    trunkGeo.translate(0, 1.3, 0);
    const trunkMat = new THREE.MeshStandardMaterial({ color: 0x6b5a44, roughness: 0.95 });
    const blobGeo = new THREE.IcosahedronGeometry(1.65, 1);
    blobGeo.scale(1, 1.18, 1);
    const blobMat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.95 });
    const coneGeo = new THREE.ConeGeometry(1.5, 4.4, 9);
    coneGeo.translate(0, 3.0, 0);
    const coneMat = new THREE.MeshStandardMaterial({ color: 0x2e5d33, roughness: 0.95 });
    this.treeTrunks = new THREE.InstancedMesh(trunkGeo, trunkMat, N);
    this.treeBlobs = new THREE.InstancedMesh(blobGeo, blobMat, N);
    this.treeCones = new THREE.InstancedMesh(coneGeo, coneMat, N);
    for (const im of [this.treeTrunks, this.treeBlobs, this.treeCones]) {
      im.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      im.castShadow = true;
      im.frustumCulled = false;
      this.scene.add(im);
    }
    const _tm = new THREE.Matrix4(), _tq = new THREE.Quaternion(), _ts = new THREE.Vector3(), _tp = new THREE.Vector3();
    const _col = new THREE.Color();
    this.treeBelt = new Belt(6.5, HALF, 0, (i, z) => {
      for (const side of [0, 1]) { // 0 — справа, 1 — слева
        const j = side * HALF + i;
        const h1 = hash(j * 3.7 + 1.3), h2 = hash(j * 7.1 + 5.2), h3 = hash(j * 11.3 + 9.7), h4 = hash(j * 5.9 + 2.8);
        const skipP = side === 1 ? 0.32 : 0.20; // слева (встречка) чаще просветы
        if (h4 < skipP) { // пусто
          _tm.makeScale(0.0001, 0.0001, 0.0001);
          this.treeTrunks.setMatrixAt(j, _tm);
          this.treeBlobs.setMatrixAt(j, _tm);
          this.treeCones.setMatrixAt(j, _tm);
          continue;
        }
        const x = side === 0 ? 25 + h1 * h1 * 95 : -(31 + h1 * h1 * 100);
        const s = 0.65 + h2 * 1.15;
        const conifer = h3 < 0.32;
        _tp.set(x, 0, z + (h3 - 0.5) * 5);
        _tq.setFromAxisAngle(UP, h1 * 6.28);
        _ts.set(s, s * (0.85 + h2 * 0.4), s);
        _tm.compose(_tp, _tq, _ts);
        this.treeTrunks.setMatrixAt(j, _tm);
        this.treeCones.setMatrixAt(j, conifer ? _tm : _tm.clone().makeScale(0.0001, 0.0001, 0.0001));
        const blobT = _tm.clone();
        if (conifer) blobT.makeScale(0.0001, 0.0001, 0.0001);
        else blobT.multiply(new THREE.Matrix4().makeTranslation(0, 3.5 / s * s, 0));
        this.treeBlobs.setMatrixAt(j, blobT);
        _col.setHSL(0.26 + h2 * 0.09, 0.52, 0.30 + h1 * 0.12);
        this.treeBlobs.setColorAt(j, _col);
      }
      this.treeTrunks.instanceMatrix.needsUpdate = true;
      this.treeBlobs.instanceMatrix.needsUpdate = true;
      this.treeCones.instanceMatrix.needsUpdate = true;
      if (this.treeBlobs.instanceColor) this.treeBlobs.instanceColor.needsUpdate = true;
    });

    // ---------- ПУТЕПРОВОДЫ ----------
    this.bridges = [];
    const concMat2 = new THREE.MeshStandardMaterial({ map: T.concrete, roughness: 0.9 });
    for (let i = 0; i < 2; i++) {
      const grp = new THREE.Group();
      const deck = new THREE.Mesh(new THREE.BoxGeometry(58, 1.5, 10), concMat2);
      deck.position.y = 6.4;
      deck.castShadow = true;
      grp.add(deck);
      for (const sz of [-1, 1]) {
        const par = new THREE.Mesh(new THREE.BoxGeometry(58, 1.0, 0.28), concMat2);
        par.position.set(0, 7.6, sz * 4.85);
        grp.add(par);
        const rail = new THREE.Mesh(new THREE.BoxGeometry(58, 0.06, 0.06), railMat);
        rail.position.set(0, 8.2, sz * 4.85);
        grp.add(rail);
      }
      for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
        const pillar = new THREE.Mesh(new THREE.CylinderGeometry(0.85, 1.0, 6.0, 14), concMat2);
        pillar.position.set(sx * 26.5, 3.0, sz * 3.1);
        pillar.castShadow = true;
        grp.add(pillar);
      }
      grp.position.z = i * 1600 + 800;
      this.bridges.push(grp);
      scene.add(grp);
    }

    // ---------- ЗЕЛЁНЫЕ ВЫВЕСКИ (порталы) ----------
    this.gantries = [];
    const poleMat = new THREE.MeshStandardMaterial({ color: 0x7f8a92, roughness: 0.45, metalness: 0.85 });
    for (let i = 0; i < 2; i++) {
      const grp = new THREE.Group();
      for (const sx of [-1, 1]) {
        const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.13, 0.17, 6.6, 12), poleMat);
        pole.position.set(sx > 0 ? 19.8 : 0.9, 3.3, 0);
        pole.castShadow = true;
        grp.add(pole);
      }
      const beam = new THREE.Mesh(new THREE.BoxGeometry(19.4, 0.55, 0.35), poleMat);
      beam.position.set(10.35, 6.35, 0);
      beam.castShadow = true;
      grp.add(beam);
      const brace = new THREE.Mesh(new THREE.BoxGeometry(19.4, 0.08, 0.08), poleMat);
      brace.position.set(10.35, 5.95, 0);
      grp.add(brace);
      const signMat = new THREE.MeshStandardMaterial({ roughness: 0.55, metalness: 0.05 });
      const sign = new THREE.Mesh(new THREE.PlaneGeometry(7.6, 2.0), signMat);
      sign.position.set(12.5, 4.85, 0.2);
      sign.rotation.y = Math.PI;
      grp.add(sign);
      const sign2 = new THREE.Mesh(new THREE.PlaneGeometry(4.4, 1.7), signMat.clone());
      sign2.position.set(4.6, 4.7, 0.2);
      sign2.rotation.y = Math.PI;
      grp.add(sign2);
      // знак 110 на опоре
      const s110 = new THREE.Mesh(new THREE.PlaneGeometry(0.95, 0.95),
        new THREE.MeshStandardMaterial({ map: T.sign110, transparent: true, roughness: 0.5 }));
      s110.position.set(19.2, 2.6, -0.32);
      s110.rotation.y = Math.PI;
      grp.add(s110);
      grp.userData.sign = sign;
      grp.userData.sign2 = sign2;
      grp.position.z = i * 1400 + 350;
      sign.material.map = T.gantries[i % T.gantries.length];
      sign2.material.map = T.gantries[(i + 3) % T.gantries.length];
      this.gantries.push(grp);
      scene.add(grp);
    }
    this.gantryIdx = 0;

    // ---------- КМ-СТОЛБИКИ ----------
    this.kmPosts = [];
    for (let i = 0; i < 4; i++) {
      const grp = new THREE.Group();
      const post = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.055, 1.4, 8), poleMat);
      post.position.y = 0.7;
      grp.add(post);
      const boardMat = new THREE.MeshStandardMaterial({ roughness: 0.6 });
      const board = new THREE.Mesh(new THREE.PlaneGeometry(0.58, 0.78), boardMat);
      board.position.y = 1.75;
      board.rotation.y = Math.PI;
      grp.add(board);
      grp.userData.board = board;
      const right = i % 2 === 0;
      grp.position.set(right ? 20.9 : -24.9, 0, (i / 2) * 1000);
      this.kmPosts.push(grp);
      scene.add(grp);
    }
    this.kmBeltR = new Belt(1000, 2, 500, (i, z) => {
      const g = this.kmPosts[i * 2];
      g.position.z = z;
      const km = Math.round(z / 1000);
      g.userData.board.material.map = T.kmTex(km);
      g.userData.board.material.needsUpdate = true;
    });
    this.kmBeltL = new Belt(1000, 2, 500, (i, z) => {
      const g = this.kmPosts[i * 2 + 1];
      g.position.z = z;
      const km = Math.round(z / 1000);
      g.userData.board.material.map = T.kmTex(km);
      g.userData.board.material.needsUpdate = true;
    });
  }

  setCamera(cam) { this.camera = cam; }

  update(px, pz, dt) {
    // дорога и длинные объекты «прилипают» к игроку с шагом тайла
    const snap15 = Math.round(pz / 15) * 15;
    this.road.position.z = snap15;
    for (const g of this.grass) g.position.z = Math.round(pz / 8) * 8;
    this.median.position.z = Math.round(pz / 2.6) * 2.6;
    this.medianFence.position.z = Math.round(pz / 2.6) * 2.6;
    this.rail.position.z = Math.round(pz / 4) * 4;
    this.rail2.position.z = Math.round(pz / 4) * 4;

    // ленты
    this.railBelt.update(pz);
    this.lampBelt.update(pz);
    this.lampBeltL.update(pz);
    this.treeBelt.update(pz, 90);
    this.kmBeltR.update(pz, 200);
    this.kmBeltL.update(pz, 200);

    // шумоэкраны
    for (const s of this.screens) {
      if (s.position.z < pz - 140) s.position.z += 2700;
    }
    // путепроводы
    for (const b of this.bridges) {
      if (b.position.z < pz - 120) b.position.z += 3200;
    }
    // вывески
    for (const gN of this.gantries) {
      if (gN.position.z < pz - 60) {
        gN.position.z += 1400;
        const T = this.T;
        const gi = this.gantryIdx++ % T.gantries.length;
        gN.userData.sign.material.map = T.gantries[gi];
        gN.userData.sign.material.needsUpdate = true;
        gN.userData.sign2.material.map = T.gantries[(gi + 3) % T.gantries.length];
        gN.userData.sign2.material.needsUpdate = true;
      }
    }

    // солнце следует за игроком
    this.sun.position.set(px, 0, pz + 20).addScaledVector(SUN_DIR, 150);
    this.sun.target.position.set(px, 0, pz + 20);
    this.sun.target.updateMatrixWorld();

    // небо и облака
    if (this.camera) this.sky.position.copy(this.camera.position);
    for (const c of this.clouds) {
      c.position.x += dt * 1.2;
      if (c.position.x > 520) c.position.x = -520;
      if (c.position.z < pz - 200) c.position.z += 1300;
      else if (c.position.z > pz + 1100) c.position.z -= 1300;
    }
  }
}

export { ONCOMING_X };
