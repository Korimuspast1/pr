import * as THREE from './three.module.min.js';

// ============================================================
//  «Выпечка» статичной геометрии: все неменяющиеся меши
//  объединяются по материалу в один меш → в десятки раз
//  меньше draw calls (главная причина лагов фона).
//  Анимированные узлы (userData.dynamic) остаются как есть,
//  их внутренности печкются относительно них самих.
// ============================================================

const _m = new THREE.Matrix4();

function mergeGeos(parts) {
  // parts: [{ geo, matrix }]
  let total = 0;
  const prepared = parts.map(p => {
    const g = p.geo.index ? p.geo.toNonIndexed() : p.geo.clone();
    if (p.matrix) g.applyMatrix4(p.matrix);
    total += g.attributes.position.count;
    return g;
  });
  const hasUV = prepared.every(g => g.attributes.uv && g.attributes.uv.itemSize === 2);
  const hasNor = prepared.every(g => g.attributes.normal);
  const pos = new Float32Array(total * 3);
  const nor = hasNor ? new Float32Array(total * 3) : null;
  const uv = hasUV ? new Float32Array(total * 2) : null;
  let o = 0;
  for (const g of prepared) {
    pos.set(g.attributes.position.array, o * 3);
    if (nor) nor.set(g.attributes.normal.array, o * 3);
    if (uv) uv.set(g.attributes.uv.array, o * 2);
    o += g.attributes.position.count;
    g.dispose();
  }
  const out = new THREE.BufferGeometry();
  out.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  if (nor) out.setAttribute('normal', new THREE.BufferAttribute(nor, 3));
  if (uv) out.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
  out.computeBoundingSphere();
  return out;
}

// печёт поддерево node: статичные меши → по одному мешу на материал
function bakeNode(node, isDynamic) {
  const buckets = new Map();   // material → { parts, cast }
  const doomed = [];
  const nodeInv = new THREE.Matrix4().copy(node.matrixWorld).invert(); // локальная: рекурсия не затрёт
  (function walk(n) {
    for (const c of [...n.children]) {
      if (isDynamic(c)) { bakeNode(c, isDynamic); continue; }
      if (c.isMesh && c.visible !== false) {
        const mat = Array.isArray(c.material) ? c.material[0] : c.material;
        let b = buckets.get(mat);
        if (!b) { b = { parts: [], cast: false }; buckets.set(mat, b); }
        _m.copy(nodeInv).multiply(c.matrixWorld);
        b.parts.push({ geo: c.geometry, matrix: _m.clone() });
        b.cast = b.cast || c.castShadow;
        doomed.push(c);
      } else if (c.children && c.children.length) {
        walk(c);
      }
    }
  })(node);
  for (const mesh of doomed) mesh.parent && mesh.parent.remove(mesh);
  // убрать опустевшие группы
  (function prune(n) {
    for (const c of [...n.children]) {
      if (!c.isMesh && !isDynamic(c) && c.children && c.children.length === 0) n.remove(c);
      else if (!c.isMesh && !isDynamic(c)) prune(c);
    }
  })(node);
  for (const [mat, b] of buckets) {
    const merged = new THREE.Mesh(mergeGeos(b.parts), mat);
    merged.castShadow = b.cast;
    merged.frustumCulled = true;
    node.add(merged);
  }
}

// главная функция: bake(root, node => node.userData.dynamic)
export function bake(root, isDynamic = n => n.userData && n.userData.dynamic) {
  root.updateMatrixWorld(true);
  bakeNode(root, isDynamic);
}

export { mergeGeos };
