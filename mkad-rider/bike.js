import * as THREE from './three.module.min.js';

// ============================================================
//  Спортбайк + мотоциклист. Всё из скруглённой геометрии:
//  капсулы, торусы, сферы, экструзии с фасками.
//  Модель смотрит в +Z, размеры в метрах (~2.05 м длиной).
// ============================================================

const UP = new THREE.Vector3(0, 1, 0);

function mesh(geo, mat, x = 0, y = 0, z = 0) {
  const m = new THREE.Mesh(geo, mat);
  m.position.set(x, y, z);
  m.castShadow = true;
  return m;
}

// капсула между двумя точками
function capsuleBetween(a, b, r, mat) {
  const A = new THREE.Vector3(...a), B = new THREE.Vector3(...b);
  const d = new THREE.Vector3().subVectors(B, A);
  const len = Math.max(d.length() - 2 * r, 0.01);
  const m = new THREE.Mesh(new THREE.CapsuleGeometry(r, len, 6, 12), mat);
  m.position.copy(A).addScaledVector(d, 0.5);
  m.quaternion.setFromUnitVectors(UP, d.clone().normalize());
  m.castShadow = true;
  return m;
}

function roundedRectShape(w, h, r) {
  const s = new THREE.Shape();
  const x = -w / 2, y = -h / 2;
  s.moveTo(x + r, y);
  s.lineTo(x + w - r, y); s.quadraticCurveTo(x + w, y, x + w, y + r);
  s.lineTo(x + w, y + h - r); s.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
  s.lineTo(x + r, y + h); s.quadraticCurveTo(x, y + h, x, y + h - r);
  s.lineTo(x, y + r); s.quadraticCurveTo(x, y, x + r, y);
  return s;
}

// «скруглённый бокс» — экструзия скруглённого прямоугольника
function roundedBoxGeo(w, h, d, r = 0.03) {
  r = Math.min(r, w / 2.1, h / 2.1, d / 2.1);
  const g = new THREE.ExtrudeGeometry(
    roundedRectShape(w - 2 * r, h - 2 * r, r * 0.85),
    { depth: Math.max(d - 2 * r, 0.01), bevelEnabled: true, bevelThickness: r, bevelSize: r * 0.92, bevelSegments: 3, curveSegments: 6 }
  );
  g.center();
  return g;
}

// боковой профиль (точки в формате [z, y]) экструдится по ширине
function profileMesh(pts, width, bevel, mat) {
  const s = new THREE.Shape();
  s.moveTo(pts[0][0], pts[0][1]);
  for (let i = 1; i < pts.length; i++) s.lineTo(pts[i][0], pts[i][1]);
  s.closePath();
  const g = new THREE.ExtrudeGeometry(s, {
    depth: width, bevelEnabled: true, bevelThickness: bevel,
    bevelSize: bevel * 0.9, bevelSegments: 3, curveSegments: 5, steps: 1
  });
  g.translate(0, 0, -width / 2);
  g.rotateY(-Math.PI / 2); // профиль: x -> z (вперёд), ширина: вдоль X
  const m = new THREE.Mesh(g, mat);
  m.castShadow = true;
  return m;
}

// ---------- колесо ----------
function buildWheel(R, matRubber, matRim, matDisc, opts = {}) {
  const tube = R - 0.24; // радиус тора
  const g = new THREE.Group();
  const tire = mesh(new THREE.TorusGeometry(0.24, tube, 20, 44), matRubber);
  tire.rotation.y = Math.PI / 2;
  g.add(tire);
  const rim = mesh(new THREE.CylinderGeometry(0.165, 0.165, 0.075, 26, 1, true), matRim);
  rim.rotation.z = Math.PI / 2;
  g.add(rim);
  const hub = mesh(new THREE.CylinderGeometry(0.055, 0.055, 0.16, 14), matRim);
  hub.rotation.z = Math.PI / 2;
  g.add(hub);
  // 5 спиц
  for (let i = 0; i < 5; i++) {
    const sg = new THREE.Group();
    const spoke = mesh(new THREE.CylinderGeometry(0.013, 0.02, 0.145, 8), matRim, 0, 0.088, 0);
    sg.add(spoke);
    sg.rotation.x = (i / 5) * Math.PI * 2;
    g.add(sg);
  }
  // тормозные диски
  const discOffs = opts.singleDisc ? [0.06] : [-0.06, 0.06];
  for (const dx of discOffs) {
    const d = mesh(new THREE.CylinderGeometry(0.135, 0.135, 0.008, 30), matDisc, dx, 0, 0);
    d.rotation.z = Math.PI / 2;
    g.add(d);
  }
  // звезда (сзади слева)
  if (opts.sprocket) {
    const sp = mesh(new THREE.CylinderGeometry(0.095, 0.095, 0.012, 26), matRim, -0.075, 0, 0);
    sp.rotation.z = Math.PI / 2;
    g.add(sp);
  }
  // суппорта
  if (opts.caliper) {
    const cal = mesh(roundedBoxGeo(0.05, 0.11, 0.08, 0.02), matDisc, opts.caliper[0], 0.15, 0.03);
    g.add(cal);
  }
  return g;
}

// ============================================================
export function buildBike(paintHex = 0x2fa14e, T) {

  const bike = new THREE.Group();
  bike.rotation.order = 'YXZ';

  // ---- материалы ----
  const paintMats = [];
  const paint = new THREE.MeshPhysicalMaterial({
    color: paintHex, metalness: 0.55, roughness: 0.34,
    clearcoat: 1, clearcoatRoughness: 0.06, envMapIntensity: 1.35
  });
  paintMats.push(paint);
  const paintDark = new THREE.MeshPhysicalMaterial({
    color: new THREE.Color(paintHex).multiplyScalar(0.45),
    metalness: 0.5, roughness: 0.4, clearcoat: 0.8, clearcoatRoughness: 0.15
  });
  paintMats.push(paintDark);
  const black = new THREE.MeshStandardMaterial({ color: 0x141414, roughness: 0.75, metalness: 0.2 });
  const carbon = new THREE.MeshStandardMaterial({ color: 0x1d1f22, roughness: 0.42, metalness: 0.45 });
  const darkMetal = new THREE.MeshStandardMaterial({ color: 0x3c4046, roughness: 0.42, metalness: 0.88 });
  const steel = new THREE.MeshStandardMaterial({ color: 0x9aa2ab, roughness: 0.3, metalness: 0.95 });
  const chrome = new THREE.MeshStandardMaterial({ color: 0xdfe3e8, roughness: 0.1, metalness: 1 });
  const gold = new THREE.MeshStandardMaterial({ color: 0xc09a3a, roughness: 0.26, metalness: 1 });
  const rubber = new THREE.MeshStandardMaterial({ color: 0x1b1b1e, roughness: 0.95, metalness: 0 });
  const glassSmoke = new THREE.MeshPhysicalMaterial({
    color: 0x232a2f, roughness: 0.06, metalness: 0.2, transparent: true,
    opacity: 0.55, side: THREE.DoubleSide, envMapIntensity: 1.6
  });
  const headlightMat = new THREE.MeshStandardMaterial({
    color: 0xf4faff, emissive: 0xcfe4ff, emissiveIntensity: 0.9, roughness: 0.15, metalness: 0.3
  });
  const tailMat = new THREE.MeshStandardMaterial({ color: 0x7a0e0e, emissive: 0xff1f14, emissiveIntensity: 1.1, roughness: 0.3 });
  const amberMat = new THREE.MeshStandardMaterial({ color: 0x8a5410, emissive: 0xffa018, emissiveIntensity: 0.7, roughness: 0.3 });

  // ---------- РУЛЕВАЯ ГОЛОВКА / ВИЛКА ----------
  const steer = new THREE.Group();
  steer.rotation.order = 'YXZ';
  steer.position.set(0, 0.92, 0.40);
  steer.rotation.x = -0.42; // вынос (рейк)
  bike.add(steer);

  // трубы вилки
  for (const sx of [-1, 1]) {
    steer.add(capsuleBetween([sx * 0.088, 0.02, 0], [sx * 0.088, -0.30, 0], 0.028, carbon));
    steer.add(capsuleBetween([sx * 0.088, -0.30, 0], [sx * 0.088, -0.62, 0], 0.035, gold));
  }
  // траверсы
  steer.add(mesh(roundedBoxGeo(0.235, 0.04, 0.11, 0.015), darkMetal, 0, 0.0, 0));
  steer.add(mesh(roundedBoxGeo(0.22, 0.035, 0.10, 0.015), darkMetal, 0, -0.30, 0));
  // клипоны (руль)
  for (const sx of [-1, 1]) {
    const bar = mesh(new THREE.CylinderGeometry(0.017, 0.017, 0.17, 10), darkMetal, sx * 0.17, -0.015, 0.01);
    bar.rotation.z = Math.PI / 2 - sx * 0.14;
    steer.add(bar);
    const grip = mesh(new THREE.CylinderGeometry(0.021, 0.021, 0.12, 10), rubber, sx * 0.255, -0.035, 0.028);
    grip.rotation.z = Math.PI / 2 - sx * 0.14;
    steer.add(grip);
    // зеркала на концах руля
    steer.add(capsuleBetween([sx * 0.30, -0.035, 0.03], [sx * 0.37, 0.09, 0.05], 0.008, black));
    const mir = mesh(roundedBoxGeo(0.105, 0.05, 0.026, 0.012), chrome, sx * 0.385, 0.115, 0.055);
    mir.rotation.z = -sx * 0.3;
    steer.add(mir);
  }

  // переднее колесо
  const frontWheel = buildWheel(0.325, rubber, darkMetal, steel, { caliper: [0.075] });
  frontWheel.position.set(0, -0.646, 0);
  steer.add(frontWheel);

  // переднее крыло
  const fender = mesh(
    new THREE.CylinderGeometry(0.405, 0.405, 0.24, 22, 1, true, -0.35, Math.PI + 0.7),
    paint, 0, -0.646, 0.02
  );
  fender.rotation.z = Math.PI / 2;
  fender.material = paint;
  fender.geometry.computeVertexNormals?.();
  steer.add(fender);

  // ---------- РАМА / ДВИГАТЕЛЬ ----------
  bike.add(mesh(roundedBoxGeo(0.36, 0.32, 0.52, 0.06), darkMetal, 0, 0.45, 0.0));       // картер
  bike.add(mesh(roundedBoxGeo(0.27, 0.15, 0.32, 0.05), darkMetal, 0, 0.65, 0.10));      // блок цилиндров
  for (let i = 0; i < 7; i++) {                                                          // рёбра
    bike.add(mesh(new THREE.BoxGeometry(0.30, 0.012, 0.34), black, 0, 0.585 + i * 0.022, 0.10));
  }
  const clutch = mesh(new THREE.CylinderGeometry(0.088, 0.088, 0.035, 22), gold, 0.19, 0.42, -0.05);
  clutch.rotation.z = Math.PI / 2; bike.add(clutch);
  const alt = mesh(new THREE.CylinderGeometry(0.07, 0.07, 0.03, 20), darkMetal, -0.19, 0.42, -0.05);
  alt.rotation.z = Math.PI / 2; bike.add(alt);
  // рама: главные лонжероны
  for (const sx of [-1, 1]) {
    bike.add(capsuleBetween([sx * 0.07, 0.90, 0.37], [sx * 0.095, 0.63, -0.22], 0.032, black));
    bike.add(capsuleBetween([sx * 0.095, 0.63, -0.22], [sx * 0.105, 0.5, -0.27], 0.025, black));
  }
  bike.add(capsuleBetween([0, 0.88, 0.34], [0, 0.52, 0.42], 0.026, black)); // передняя труба вниз
  // подрамник
  for (const sx of [-1, 1]) bike.add(capsuleBetween([sx * 0.06, 0.68, -0.28], [sx * 0.07, 0.62, -0.72], 0.02, black));

  // радиатор
  bike.add(mesh(roundedBoxGeo(0.27, 0.2, 0.06, 0.02), black, 0, 0.52, 0.50));
  for (let i = 0; i < 5; i++) bike.add(mesh(new THREE.BoxGeometry(0.25, 0.18, 0.008), carbon, 0, 0.52, 0.475 + i * 0.012));

  // выпуск: 4 трубы -> коллектор -> глушитель
  const pipeMat = steel;
  const headerPts = [-0.084, -0.028, 0.028, 0.084];
  for (const px of headerPts) {
    bike.add(capsuleBetween([px, 0.66, 0.18], [px * 1.3, 0.47, 0.16], 0.019, pipeMat));
    bike.add(capsuleBetween([px * 1.3, 0.47, 0.16], [0.10, 0.29, 0.08], 0.019, pipeMat));
  }
  bike.add(capsuleBetween([0.10, 0.29, 0.08], [0.145, 0.285, -0.30], 0.03, pipeMat));   // приёмная труба
  const muffler = mesh(new THREE.CapsuleGeometry(0.055, 0.34, 6, 16), pipeMat, 0.155, 0.325, -0.50);
  muffler.rotation.x = Math.PI / 2 - 0.12;
  bike.add(muffler);
  const tip = mesh(new THREE.CylinderGeometry(0.043, 0.043, 0.05, 16), black, 0.155, 0.352, -0.685);
  tip.rotation.x = Math.PI / 2 - 0.12;
  bike.add(tip);

  // ---------- МAYТ/СИДЕНЬЕ/ХВОСТ ----------
  const tank = mesh(new THREE.SphereGeometry(1, 30, 22), paint, 0, 0.925, 0.13);
  tank.scale.set(0.205, 0.165, 0.35);
  bike.add(tank);
  const tankCap = mesh(new THREE.CylinderGeometry(0.038, 0.038, 0.014, 14), chrome, 0, 1.085, 0.10);
  bike.add(tankCap);

  const seat = mesh(new THREE.SphereGeometry(1, 24, 16), black, 0, 0.86, -0.14);
  seat.scale.set(0.155, 0.055, 0.225);
  bike.add(seat);

  // хвост
  bike.add(profileMesh(
    [[-0.14, 0.90], [-0.40, 1.015], [-0.66, 1.03], [-0.755, 0.955], [-0.52, 0.845], [-0.20, 0.815]],
    0.185, 0.05, paint
  ));
  const tailLight = mesh(roundedBoxGeo(0.13, 0.045, 0.02, 0.01), tailMat, 0, 0.965, -0.755);
  bike.add(tailLight);
  // поворотники
  for (const sx of [-1, 1]) {
    const t = mesh(new THREE.CapsuleGeometry(0.017, 0.025, 4, 10), amberMat, sx * 0.13, 0.93, -0.71);
    t.rotation.x = Math.PI / 2;
    bike.add(t);
    const tf = mesh(new THREE.CapsuleGeometry(0.016, 0.022, 4, 10), amberMat, sx * 0.165, 0.99, 0.60);
    tf.rotation.x = Math.PI / 2;
    bike.add(tf);
  }
  // номер
  const plate = mesh(new THREE.PlaneGeometry(0.155, 0.105), new THREE.MeshStandardMaterial({ map: T.plate, roughness: 0.6 }), 0, 0.87, -0.78);
  plate.rotation.y = Math.PI;
  plate.rotation.x = 0.12;
  plate.castShadow = false;
  bike.add(plate);

  // ---------- ОБОЙКА/ПЛАСТИК ----------
  // носовая обтекатель
  bike.add(profileMesh(
    [[0.32, 0.76], [0.56, 0.85], [0.73, 0.97], [0.71, 1.10], [0.50, 1.17], [0.30, 1.09], [0.28, 0.92]],
    0.27, 0.05, paint
  ));
  // боковые панели
  const sidePts = [[0.68, 0.52], [0.65, 0.72], [0.48, 0.93], [0.13, 1.0], [-0.16, 0.945], [-0.33, 0.75], [-0.31, 0.5], [-0.05, 0.335], [0.3, 0.33], [0.53, 0.41]];
  const sideL = profileMesh(sidePts, 0.035, 0.03, paint);
  sideL.position.x = 0.155;
  bike.add(sideL);
  const sideR = profileMesh(sidePts, 0.035, 0.03, paint);
  sideR.position.x = -0.155;
  bike.add(sideR);
  // нижняя панель
  bike.add(profileMesh([[0.52, 0.35], [0.34, 0.19], [-0.12, 0.17], [-0.37, 0.28], [-0.33, 0.47], [0.2, 0.47]], 0.29, 0.045, paintDark));
  // полоска-акцент на боках
  const stripeL = profileMesh([[0.62, 0.62], [0.5, 0.86], [0.15, 0.94], [-0.1, 0.9], [-0.2, 0.78], [0.1, 0.66], [0.45, 0.56]], 0.015, 0.012, chrome);
  stripeL.position.x = 0.203;
  bike.add(stripeL);
  const stripeR = stripeL.clone();
  stripeR.position.x = -0.203;
  bike.add(stripeR);

  // фары
  for (const sx of [-1, 1]) {
    const hl = mesh(new THREE.CylinderGeometry(0.047, 0.047, 0.05, 18), headlightMat, sx * 0.068, 1.0, 0.705);
    hl.rotation.x = Math.PI / 2;
    bike.add(hl);
    const ring = mesh(new THREE.TorusGeometry(0.05, 0.009, 10, 22), black, sx * 0.068, 1.0, 0.728);
    bike.add(ring);
  }
  // лобовое стекло
  const screen = mesh(new THREE.CylinderGeometry(0.235, 0.30, 0.29, 18, 1, true, -0.85, 1.7), glassSmoke, 0, 1.115, 0.40);
  screen.rotation.x = -0.55;
  screen.castShadow = false;
  bike.add(screen);

  // ---------- ЗАДНЯЯ ПОДВЕСКА ----------
  for (const sx of [-1, 1]) {
    bike.add(capsuleBetween([sx * 0.105, 0.47, -0.28], [sx * 0.105, 0.34, -0.66], 0.03, darkMetal));
  }
  const pivot = mesh(new THREE.CylinderGeometry(0.035, 0.035, 0.24, 12), black, 0, 0.47, -0.27);
  pivot.rotation.z = Math.PI / 2;
  bike.add(pivot);
  // моноамортизатор + пружина
  const shockA = [0, 0.78, -0.30], shockB = [0, 0.43, -0.50];
  bike.add(capsuleBetween(shockA, shockB, 0.028, gold));
  const springMat = new THREE.MeshStandardMaterial({ color: 0xc21f1f, roughness: 0.35, metalness: 0.6 });
  const shockDir = new THREE.Vector3(...shockB).sub(new THREE.Vector3(...shockA)).normalize();
  const springQ = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 0, 1), shockDir);
  for (let i = 0; i < 6; i++) {
    const t = 0.12 + (i / 5) * 0.72;
    const p = new THREE.Vector3(...shockA).lerp(new THREE.Vector3(...shockB), t);
    const ring = mesh(new THREE.TorusGeometry(0.048, 0.009, 8, 20), springMat, p.x, p.y, p.z);
    ring.quaternion.copy(springQ);
    bike.add(ring);
  }
  // цепь
  bike.add(mesh(new THREE.BoxGeometry(0.016, 0.018, 0.40), black, -0.125, 0.415, -0.475));
  bike.add(mesh(new THREE.BoxGeometry(0.016, 0.018, 0.36), black, -0.125, 0.315, -0.48));
  // заднее колесо
  const rearWheel = buildWheel(0.325, rubber, darkMetal, steel, { singleDisc: true, sprocket: true, caliper: [0.075] });
  rearWheel.position.set(0, 0.33, -0.68);
  bike.add(rearWheel);

  // подножки
  for (const sx of [-1, 1]) {
    const peg = mesh(new THREE.CylinderGeometry(0.016, 0.016, 0.09, 10), steel, sx * 0.17, 0.37, -0.05);
    peg.rotation.z = Math.PI / 2;
    bike.add(peg);
  }

  // ---------- МОТОЦИКЛИСТ ----------
  const rider = new THREE.Group();
  bike.add(rider);
  const suit = new THREE.MeshPhysicalMaterial({ color: 0x17181c, roughness: 0.48, metalness: 0.1, clearcoat: 0.5, clearcoatRoughness: 0.3 });
  const suitAccent = new THREE.MeshPhysicalMaterial({ color: paintHex, roughness: 0.42, metalness: 0.3, clearcoat: 0.7, clearcoatRoughness: 0.2 });
  paintMats.push(suitAccent);
  const helmetMat = new THREE.MeshPhysicalMaterial({ color: 0x101114, roughness: 0.22, metalness: 0.4, clearcoat: 1, clearcoatRoughness: 0.08 });
  const helmetAccent = new THREE.MeshPhysicalMaterial({ color: paintHex, roughness: 0.2, metalness: 0.5, clearcoat: 1, clearcoatRoughness: 0.08 });
  paintMats.push(helmetAccent);
  const visorMat = new THREE.MeshPhysicalMaterial({ color: 0x0c0f13, roughness: 0.05, metalness: 0.6, envMapIntensity: 1.8 });

  const hip = [0, 0.875, -0.10];
  const chest = [0, 1.135, 0.24];
  // торс
  rider.add(capsuleBetween(hip, chest, 0.128, suit));
  // горб аэродинамический
  const hump = mesh(new THREE.SphereGeometry(1, 20, 14), suitAccent, 0, 1.16, 0.13);
  hump.scale.set(0.105, 0.07, 0.14);
  rider.add(hump);
  // бёдра
  rider.add(capsuleBetween([-0.095, 0.86, -0.12], [0.095, 0.86, -0.12], 0.075, suit));
  // ноги
  for (const sx of [-1, 1]) {
    rider.add(capsuleBetween([sx * 0.09, 0.85, -0.10], [sx * 0.21, 0.64, 0.16], 0.055, suit));      // бедро
    rider.add(capsuleBetween([sx * 0.21, 0.64, 0.16], [sx * 0.185, 0.40, -0.03], 0.045, suit));     // голень
    const boot = mesh(roundedBoxGeo(0.09, 0.085, 0.25, 0.03), black, sx * 0.185, 0.37, -0.02);
    rider.add(boot);
    const knee = mesh(new THREE.SphereGeometry(1, 14, 10), suitAccent, sx * 0.225, 0.635, 0.155);
    knee.scale.set(0.045, 0.05, 0.05);
    rider.add(knee);
  }
  // руки
  for (const sx of [-1, 1]) {
    rider.add(capsuleBetween([sx * 0.155, 1.125, 0.24], [sx * 0.30, 1.00, 0.33], 0.042, suit));   // плечо
    rider.add(capsuleBetween([sx * 0.30, 1.00, 0.33], [sx * 0.245, 0.90, 0.46], 0.036, suit));     // предплечье
    rider.add(mesh(new THREE.SphereGeometry(0.052, 14, 10), black, sx * 0.245, 0.90, 0.47));       // перчатка
    const shoulder = mesh(new THREE.SphereGeometry(1, 14, 10), suitAccent, sx * 0.16, 1.135, 0.22);
    shoulder.scale.set(0.05, 0.06, 0.07);
    rider.add(shoulder);
  }
  // голова + шлем
  const head = new THREE.Group();
  head.position.set(0, 1.27, 0.335);
  const helmet = mesh(new THREE.SphereGeometry(0.135, 26, 20), helmetMat);
  head.add(helmet);
  const helmetStripe = mesh(new THREE.TorusGeometry(0.1352, 0.02, 10, 34), helmetAccent);
  helmetStripe.rotation.x = Math.PI / 2 - 0.15;
  head.add(helmetStripe);
  const visor = mesh(new THREE.SphereGeometry(0.1385, 22, 10, Math.PI / 2 - 0.85, 1.7, Math.PI / 2 - 0.42, 0.84), visorMat);
  head.add(visor);
  const chin = mesh(new THREE.SphereGeometry(0.115, 18, 12, Math.PI / 2 - 0.8, 1.6, 0.4, 1.15), helmetMat, 0, -0.02, 0.01);
  head.add(chin);
  rider.add(head);

  // ---------- тени ----------
  bike.traverse(o => { if (o.isMesh) o.castShadow = true; });

  // ---------- анимация ----------
  const api = {
    group: bike,
    rider, head,
    frontWheel, rearWheel, steer,
    setPaint(hex) {
      for (const m of paintMats) {
        if (m === paintDark) m.color.set(hex).multiplyScalar(0.45);
        else m.color.set(hex);
      }
    },
    animate(dt, s) {
      const w = s.v / 0.325 * dt;
      frontWheel.rotation.x += w;
      rearWheel.rotation.x += w;
      steer.rotation.y = (s.steerVis || 0);
      // лёгкий наклон головы в поворот (частично выпрямляется)
      head.rotation.y = (s.steer || 0) * 0.35;
      head.rotation.z = (s.lean || 0) * 0.30;
      // посадка прижимается с ростом скорости
      rider.rotation.x = Math.min((s.v || 0) * 0.0035, 0.20);
    }
  };
  return api;
}
