// Мелкие общие утилиты: JSON-ответы, безопасный fetch с таймаутом, разбор env.

export const UA =
  process.env.WAVE_USER_AGENT ||
  'WaveMusic/1.0 (+https://github.com/Korimuspast1/pr; legal music player)';

export function sendJson(res, code, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(code, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(body),
    'Cache-Control': 'no-store',
    'Access-Control-Allow-Origin': '*',
  });
  res.end(body);
}

export function clampInt(value, fallback, min, max) {
  const n = Number.parseInt(value, 10);
  if (!Number.isFinite(n)) return fallback;
  return Math.min(max, Math.max(min, n));
}

export function list(value) {
  return String(value || '')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);
}

/** fetch с таймаутом и внятной ошибкой вместо «fetch failed». */
export async function safeFetch(url, { timeout = 9000, ...opts } = {}) {
  try {
    return await fetch(url, { ...opts, signal: AbortSignal.timeout(timeout) });
  } catch (err) {
    const e = new Error(`upstream недоступен: ${err.message || err}`);
    e.code = 'upstream_unreachable';
    throw e;
  }
}

export async function fetchJson(url, { timeout = 9000, headers = {} } = {}) {
  const res = await safeFetch(url, {
    timeout,
    headers: { Accept: 'application/json', 'User-Agent': UA, ...headers },
  });
  const text = await res.text();
  let data = null;
  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    data = null;
  }
  if (!res.ok) {
    const e = new Error(`HTTP ${res.status} от ${new URL(url).host}`);
    e.status = res.status;
    e.body = data || text.slice(0, 300);
    throw e;
  }
  return data;
}

/** SoundCloud отдаёт картинки вида ...-large.jpg — просим самый большой размер. */
export function artworkUrl(url) {
  if (!url) return null;
  if (/^https?:\/\//.test(url)) {
    return url.replace(/-t\d+x\d+\.(jpg|jpeg|png)$/i, '-t500x500.$1').replace(/-large\./, '-t500x500.');
  }
  return null;
}

export function seconds(ms) {
  const n = Number(ms);
  if (!Number.isFinite(n) || n <= 0) return 0;
  return Math.round(n / 1000);
}
