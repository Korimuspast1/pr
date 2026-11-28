/*
 * Builds data/world.js from the Natural Earth 110m country topology
 * (npm: world-atlas) plus population/climate metadata (npm: country-json,
 * npm: world-countries).
 *
 * Usage:
 *   npm init -y
 *   npm i world-atlas@2 topojson-client@3 world-countries@5 country-json@2
 *   node tools/build-world.mjs
 */
import { readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { feature } from 'topojson-client';

const require = createRequire(import.meta.url);
const topo = require('world-atlas/countries-110m.json');
const countries = require('world-countries');
const popData = require('country-json/src/country-by-population.json');
const tempData = require('country-json/src/country-by-yearly-average-temperature.json');

const W = 1000;
const H = 500;
const LAT_TOP = 84;
const LAT_BOTTOM = -58;

// ---------------------------------------------------------------- projection
const miller = (latDeg) => {
  const phi = (Math.max(-85, Math.min(85, latDeg)) * Math.PI) / 180;
  return 1.25 * Math.log(Math.tan(Math.PI / 4 + 0.4 * phi));
};
const yTop = miller(LAT_TOP);
const yBottom = miller(LAT_BOTTOM);
const project = ([lon, lat]) => [
  ((lon + 180) / 360) * W,
  ((yTop - miller(lat)) / (yTop - yBottom)) * H,
];

// ---------------------------------------------------------------- simplify
const ringArea = (pts) => {
  let a = 0;
  for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
    a += pts[j][0] * pts[i][1] - pts[i][0] * pts[j][1];
  }
  return Math.abs(a / 2);
};

const perpDist = (p, a, b) => {
  const dx = b[0] - a[0];
  const dy = b[1] - a[1];
  const len = Math.hypot(dx, dy);
  if (!len) return Math.hypot(p[0] - a[0], p[1] - a[1]);
  return Math.abs(dy * p[0] - dx * p[1] + b[0] * a[1] - b[1] * a[0]) / len;
};

function rdp(points, eps) {
  if (points.length < 4) return points;
  let maxD = 0;
  let idx = 0;
  for (let i = 1; i < points.length - 1; i++) {
    const d = perpDist(points[i], points[0], points[points.length - 1]);
    if (d > maxD) {
      maxD = d;
      idx = i;
    }
  }
  if (maxD > eps) {
    const left = rdp(points.slice(0, idx + 1), eps);
    const right = rdp(points.slice(idx), eps);
    return left.slice(0, -1).concat(right);
  }
  return [points[0], points[points.length - 1]];
}

// Natural Earth rings cross the antimeridian; unwrap longitudes and clip each
// ring to the [-180, 180] window so countries are not smeared across the map.
function unwrapRing(ring) {
  const out = [ring[0]];
  for (let i = 1; i < ring.length; i++) {
    let lon = ring[i][0];
    const prev = out[out.length - 1][0];
    while (lon - prev > 180) lon -= 360;
    while (prev - lon > 180) lon += 360;
    out.push([lon, ring[i][1]]);
  }
  return out;
}

function clipLon(ring, min, max) {
  let pts = ring;
  const clipEdge = (input, inside, intersect) => {
    const res = [];
    for (let i = 0; i < input.length; i++) {
      const cur = input[i];
      const prev = input[(i + input.length - 1) % input.length];
      const curIn = inside(cur);
      const prevIn = inside(prev);
      if (curIn) {
        if (!prevIn) res.push(intersect(prev, cur));
        res.push(cur);
      } else if (prevIn) {
        res.push(intersect(prev, cur));
      }
    }
    return res;
  };
  pts = clipEdge(
    pts,
    (p) => p[0] >= min,
    (a, b) => {
      const t = (min - a[0]) / (b[0] - a[0]);
      return [min, a[1] + t * (b[1] - a[1])];
    },
  );
  if (pts.length < 3) return null;
  pts = clipEdge(
    pts,
    (p) => p[0] <= max,
    (a, b) => {
      const t = (max - a[0]) / (b[0] - a[0]);
      return [max, a[1] + t * (b[1] - a[1])];
    },
  );
  if (pts.length < 3) return null;
  return pts;
}

function simplifyRing(rawRing, eps) {
  const spanLon = Math.max(...rawRing.map((p) => p[0])) - Math.min(...rawRing.map((p) => p[0]));
  const ring = clipLon(unwrapRing(rawRing), -180, 180);
  if (!ring) return null;
  if (spanLon > 350) return null;
  const pts = ring.map(project);
  // drop duplicate consecutive points
  const clean = pts.filter((p, i) => {
    if (!i) return true;
    const q = pts[i - 1];
    return Math.hypot(p[0] - q[0], p[1] - q[1]) > 0.05;
  });
  if (clean.length < 4) return null;
  const open = clean[0][0] === clean[clean.length - 1][0] &&
    clean[0][1] === clean[clean.length - 1][1]
    ? clean
    : clean.concat([clean[0]]);
  let out = rdp(open, eps);
  if (out.length < 4) return null;
  const rounded = out.map(([x, y]) => [Math.round(x * 10) / 10, Math.round(y * 10) / 10]);
  // remove rounding duplicates
  const dedup = rounded.filter((p, i) => {
    if (!i) return true;
    const q = rounded[i - 1];
    return p[0] !== q[0] || p[1] !== q[1];
  });
  if (dedup.length < 4) return null;
  if (ringArea(dedup) < 0.9) return null;
  return dedup;
}

// ---------------------------------------------------------------- metadata
const aliasPop = {
  'DR Congo': 'The Democratic Republic of Congo',
  'Republic of the Congo': 'Congo',
  'Czechia': 'Czech Republic',
  'Fiji': 'Fiji Islands',
  'Micronesia': 'Micronesia, Federated States of',
  'Timor-Leste': 'East Timor',
  'Türkiye': 'Turkey',
  'Vatican City': 'Holy See (Vatican City State)',
  'Réunion': 'Reunion',
  'São Tomé and Príncipe': 'Sao Tome and Principe',
};
const popMap = new Map(popData.map((r) => [r.country, r.population]));
// A handful of entities country-json does not cover.
const popFallback = { TWN: 23_600_000, XKX: 1_795_000, CYN: 326_000, SOL: 5_700_000, ESH: 652_000 };
const tempMap = new Map(tempData.map((r) => [r.country, r.temperature]));

const popFor = (c) => {
  const name = c.name.common;
  return popMap.get(aliasPop[name] || name) ?? null;
};
const tempFor = (c) => {
  const name = c.name.common;
  return tempMap.get(aliasPop[name] || name) ?? null;
};

// Income groups (World Bank-ish), used only to tune healthcare / cure speed.
const highIncome = new Set(`USA CAN JPN KOR AUS NZ DEU FRA GBR ITA ESP PRT NLD BEL LUX CHE AUT DNK SWE NOR FIN ISL IRL ISR SGP HKG ARE QAT KWT SAU BHR OMN CZE POL SVN SVK EST LVA LTU GRC CYP MLT HUN HRV CHL URY TWN MLT`.split(/\s+/));
const upperMid = new Set(`RUS CHN BRA MEX ARG TUR ZAF MYS THA IDN COL PER VEN ECU CRI PAN DOM JAM SRB BGR ROU BLR KAZ AZE GEO ARM UKR IRN IRQ LBY MUS SYC PLW NRU TTO`.split(/\s+/));
const veryHigh = new Set(`USA CHE NOR IRL LUX DNK SGP ISL QAT NLD SWE AUT AUS DEU CAN FIN GBR NZL BEL ISR ARE JPN KOR FRA ITA ESP`.split(/\s+/));

const aridSet = new Set(`SAU ARE QAT KWT OMN YEM IRQ IRN JOR SYR EGY LBY DZA MAR TUN MRT MLI NER TCD SDN ERI DJI SOM ETH KEN TZA NAM BWA ZWE AUS PAK AFG UZB TKM`.split(/\s+/));

const extras = {
  XKX: { name: 'Kosovo', pop: 1795000, latlng: [42.6, 20.9], borders: ['ALB', 'MKD', 'MNE', 'SRB'], region: 'Europe' },
  CYN: { name: 'N. Cyprus', pop: 326000, latlng: [35.2, 33.3], borders: ['CYP'], region: 'Europe' },
  SOL: { name: 'Somaliland', pop: 5700000, latlng: [9.7, 46.2], borders: ['SOM', 'ETH', 'DJI'], region: 'Africa' },
};

const wcByNum = new Map(countries.map((c) => [String(Number(c.ccn3)), c]));
const wcByIso = new Map(countries.map((c) => [c.cca3, c]));
const nameToIso = new Map(countries.map((c) => [c.name.common, c.cca3]));

const geoms = topo.objects.countries.geometries;
const geo = feature(topo, topo.objects.countries);

const out = [];
const used = new Set();

for (let i = 0; i < geoms.length; i++) {
  const g = geoms[i];
  const f = geo.features[i];
  if (!f.geometry) continue;
  const mapName = g.properties.name;
  if (/Antarctica|Fr. S. Antarctic Lands/.test(mapName)) continue;

  let meta = wcByNum.get(String(Number(g.id)));
  let iso = meta ? meta.cca3 : null;
  if (mapName === 'Kosovo') { meta = extras.XKX; iso = 'XKX'; }
  else if (mapName === 'N. Cyprus') { meta = extras.CYN; iso = 'CYN'; }
  else if (mapName === 'Somaliland') { meta = extras.SOL; iso = 'SOL'; }
  if (!meta || !iso) continue;
  if (used.has(iso)) continue;
  used.add(iso);

  const polys = f.geometry.type === 'Polygon' ? [f.geometry.coordinates] : f.geometry.coordinates;
  const rings = [];
  for (const poly of polys) {
    for (const ring of poly) {
      const simplified = simplifyRing(ring, 0.6);
      if (simplified) rings.push(simplified);
    }
  }
  if (!rings.length) continue;

  // centroid of largest ring
  rings.sort((a, b) => ringArea(b) - ringArea(a));
  const main = rings[0];
  let sx = 0, sy = 0, sa = 0;
  for (let j = 0, k = main.length - 1; j < main.length; k = j++) {
    const cross = main[k][0] * main[j][1] - main[j][0] * main[k][1];
    sa += cross;
    sx += (main[k][0] + main[j][0]) * cross;
    sy += (main[k][1] + main[j][1]) * cross;
  }
  sa *= 0.5;
  let cx = sa ? sx / (6 * sa) : main[0][0];
  let cy = sa ? sy / (6 * sa) : main[0][1];
  if (!Number.isFinite(cx) || !Number.isFinite(cy)) [cx, cy] = main[0];

  const d = rings
    .map((r) => 'M' + r.map(([x, y]) => `${x} ${y}`).join('L') + 'Z')
    .join('');

  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
  for (const [x, y] of main) {
    x0 = Math.min(x0, x); y0 = Math.min(y0, y);
    x1 = Math.max(x1, x); y1 = Math.max(y1, y);
  }

  const known = wcByIso.get(iso);
  const pop = (known ? popFor(known) : null) ?? popFallback[iso] ?? meta.pop ?? 1_000_000;
  const temp = known ? tempFor(known) : null;
  const area = known?.area ?? 500_000;
  const density = pop / Math.max(area, 1);
  const landlocked = known ? known.landlocked : false;

  let wealth;
  if (veryHigh.has(iso)) wealth = 0.95;
  else if (highIncome.has(iso)) wealth = 0.8;
  else if (upperMid.has(iso)) wealth = 0.5;
  else wealth = 0.24;
  if (iso === 'XKX') wealth = 0.32;

  let climate;
  if (aridSet.has(iso)) climate = 'arid';
  else if (temp == null) climate = Math.abs(known?.latlng?.[0] ?? 0) > 55 ? 'cold' : 'mild';
  else if (temp < 6) climate = 'cold';
  else if (temp < 14) climate = 'mild';
  else if (temp < 23) climate = 'warm';
  else climate = 'tropical';

  const ru = known?.translations?.rus?.common ?? meta.name;

  out.push({
    id: iso,
    name: mapName,
    ru,
    en: known?.name.common ?? meta.name,
    d,
    cx: Math.round(cx * 10) / 10,
    cy: Math.round(cy * 10) / 10,
    x0: Math.round(x0), y0: Math.round(y0),
    x1: Math.round(x1), y1: Math.round(y1),
    pop: Math.round(pop),
    density: Math.round(density * 10) / 10,
    climate,
    wealth: Math.round(wealth * 100) / 100,
    landlocked: !!landlocked,
    borders: (meta.borders || [])
      .map((b) => (b === 'UNK' ? 'XKX' : b))
      .filter((b) => b !== iso),
    region: meta.region || known?.region || 'Other',
    subregion: known?.subregion || '',
  });
}

out.sort((a, b) => b.pop - a.pop);

const serialized = out.map((c) => [
  c.id, c.name, c.ru, c.d, c.cx, c.cy, c.pop, c.density, c.climate, c.wealth,
  c.landlocked ? 1 : 0, c.borders.join(','), c.region, c.x0, c.y0, c.x1, c.y1,
]);

const js = `/* Generated by tools/build-world.mjs — do not edit by hand.
 * Natural Earth 110m borders (world-atlas), population/climate metadata.
 * Field order: id,name,ru,path,cx,cy,pop,density,climate,wealth,landlocked,borders,region,x0,y0,x1,y1
 */
window.WORLD = ${JSON.stringify({ w: W, h: H, countries: serialized })};
`;

const outPath = process.argv[2] || new URL('../data/world.js', import.meta.url);
await writeFile(outPath, js);
console.log('countries:', out.length, 'size:', (js.length / 1024).toFixed(1) + 'kb');
console.log(out.slice(0, 8).map((c) => `${c.id} ${c.pop} ${c.climate} ${c.wealth}`).join('\n'));
