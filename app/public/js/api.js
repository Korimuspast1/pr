/* Shared API transport.
 *
 * The preview proxy in front of the sandbox is the weak link: it answers 502
 * with an HTML page when a request takes too long, when it dislikes the method,
 * or when it is simply busy. So every call here:
 *   1. stays short (no request longer than a few seconds),
 *   2. parses defensively (HTML instead of JSON is a normal, recoverable case),
 *   3. retries other transports, then retries the same one with backoff,
 *   4. reports transport health through a tiny event bus.
 */

const b64 = (obj) => btoa(unescape(encodeURIComponent(JSON.stringify(obj || {}))));

const PLANS = [
  { method: 'POST', path: (n) => `/api/${n}` },
  { method: 'GET', path: (n) => `/api/${n}` },
  { method: 'GET', path: (n) => `/api/${n}.json` },
  { method: 'POST', path: (n) => `/x/${n}` },
  { method: 'GET', path: (n) => `/x/${n}` },
  { method: 'GET', path: (n) => `/x/${n}.json` },
];

let transport = null;
const listeners = new Set();

/** subscribe to transport health: { ok: boolean, error?: string, transport?: object } */
export function onNet(fn) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}
const emit = (payload) => { for (const fn of listeners) { try { fn(payload); } catch {} } };

class ProxyError extends Error {
  constructor(msg, { html = false, status = 0 } = {}) {
    super(msg);
    this.proxy = true;
    this.html = html;
    this.status = status;
  }
}

async function attempt(method, url, body, ms) {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), ms);
  let res;
  try {
    res = await fetch(url, {
      method,
      signal: ctrl.signal,
      cache: 'no-store',
      headers: method === 'POST' ? { 'content-type': 'application/json' } : undefined,
      body: method === 'POST' ? JSON.stringify(body || {}) : undefined,
    });
  } catch (e) {
    clearTimeout(timer);
    const msg = e.name === 'AbortError' ? `таймаут ${Math.round(ms / 1000)} с` : 'нет соединения';
    throw new ProxyError(msg);
  }
  clearTimeout(timer);

  const text = await res.text();
  let data = null;
  let isJson = false;
  try {
    data = text ? JSON.parse(text) : {};
    isJson = true;
  } catch {
    isJson = false;
  }

  if (!isJson) {
    const head = text.replace(/\s+/g, ' ').trim().slice(0, 90) || '(пустой ответ)';
    const gz = /gzip|deflate|br/i.test(head) && /[^\x20-\x7E\u0400-\u04FF]/.test(head);
    throw new ProxyError(
      res.status === 502 || res.status === 504
        ? `шлюз превью ответил ${res.status} — сервер не успел/прокси занят`
        : gz
        ? 'прокси вернул сжатый поток вместо JSON'
        : `HTTP ${res.status}, не JSON: ${head}`,
      { html: /<!doctype|<html/i.test(text), status: res.status }
    );
  }

  if (!res.ok) throw new Error((data && data.error) || `HTTP ${res.status}`);
  return data;
}

const IDEMPOTENT = new Set(['stats', 'files', 'read', 'proc', 'ping', 'health', 'ptyread']);

/**
 * call('stats') → JSON.
 * Retries across transports; for idempotent endpoints also retries the same
 * transport a couple of times with backoff, because 502s from the proxy are
 * usually transient.
 */
export async function call(name, body = {}, ms = 8000) {
  const idempotent = IDEMPOTENT.has(name);
  const rounds = idempotent ? 2 : 1;
  let lastErr = null;

  for (let round = 0; round < rounds; round++) {
    const plans = transport ? [transport, ...PLANS.filter((p) => p !== transport)] : PLANS;
    for (const plan of plans) {
      try {
        let url = plan.path(name);
        if (plan.method === 'GET') url += (url.includes('?') ? '&' : '?') + 'p=' + encodeURIComponent(b64(body));
        const data = await attempt(plan.method, url, body, ms);
        if (transport !== plan) {
          transport = plan;
          console.info('[console] transport:', plan.method, plan.path('*'));
        }
        emit({ ok: true, transport: plan });
        return data;
      } catch (e) {
        lastErr = e;
        if (!e.proxy) {
          // real application-level error (bad path, permission, …) — don't retry
          emit({ ok: false, error: e.message });
          throw e;
        }
        if (transport === plan) transport = null;
      }
    }
    if (round + 1 < rounds) await new Promise((r) => setTimeout(r, 220 + Math.random() * 260));
  }

  emit({ ok: false, error: lastErr?.message || 'нет связи' });
  throw new Error(lastErr?.message || 'нет связи с сервером');
}

export async function probeTransports() {
  const out = [];
  for (const plan of PLANS) {
    const url = plan.path('ping') + (plan.method === 'GET' ? '?p=' + encodeURIComponent(b64({})) : '');
    const t0 = performance.now();
    try {
      const d = await attempt(plan.method, url, {}, 5000);
      out.push({ ok: true, method: plan.method, url, ms: Math.round(performance.now() - t0), pid: d.pid });
    } catch (e) {
      out.push({ ok: false, method: plan.method, url, error: e.message });
    }
  }
  return out;
}

export const api = {
  stats: () => call('stats', {}, 5000),
  exec: (cmd, opts = {}) => call('exec', { cmd, root: opts.root, cwd: opts.cwd, id: opts.id || 'current' }, opts.ms || 60000),
  kill: (id = 'current') => call('kill', { id }, 6000),
  files: (path) => call('files', { path }, 6000),
  read: (path) => call('read', { path }, 6000),
  write: (path, text) => call('write', { path, text }, 8000),
  proc: () => call('proc', {}, 6000),
  killproc: (pid, opts = {}) => call('killproc', { pid, signal: opts.signal || 'TERM', root: opts.root }, 8000),
  fetchUrl: (url) => call('fetch', { url }, 11000),

  // ── real PTY session (xterm.js terminal) ─────────────────────────────
  ptyOpen: (opts = {}) => call('ptyopen', { root: !!opts.root, cols: opts.cols || 120, rows: opts.rows || 32 }, 8000),
  ptyRead: (id, from, wait = 2600) => call('ptyread', { id, from, wait }, wait + 4000),
  ptyWrite: (id, data) => call('ptywrite', { id, data }, 6000),
  ptyResize: (id, cols, rows) => call('ptyresize', { id, cols, rows }, 6000),
  ptyClose: (id) => call('ptyclose', { id }, 5000),

  downloadUrl: (path, inline = false) =>
    `/api/download?path=${encodeURIComponent(path)}${inline ? '&inline=1' : ''}`,
};
