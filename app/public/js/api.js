/* Shared API transport — works through restrictive preview proxies. */

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
    throw new Error(e.name === 'AbortError' ? `таймаут ${Math.round(ms / 1000)} с` : 'сеть недоступна');
  }
  clearTimeout(timer);

  const text = await res.text();
  let data;
  try {
    data = text ? JSON.parse(text) : {};
  } catch {
    const snip = text.replace(/\s+/g, ' ').trim().slice(0, 80) || '(пустой ответ)';
    throw new Error(`HTTP ${res.status}, не JSON: ${snip}`);
  }
  if (!res.ok) throw new Error((data && data.error) || `HTTP ${res.status}`);
  return data;
}

/** call('stats') → JSON; falls back across transports, remembers the winner. */
export async function call(name, body = {}, ms = 60000) {
  const plans = transport ? [transport] : PLANS;
  const errors = [];
  for (const plan of plans) {
    try {
      let url = plan.path(name);
      if (plan.method === 'GET') url += (url.includes('?') ? '&' : '?') + 'p=' + encodeURIComponent(b64(body));
      const data = await attempt(plan.method, url, body, ms);
      const changed = transport !== plan;
      transport = plan;
      if (changed) console.info('[console] transport:', plan.method, plan.path('*'));
      return data;
    } catch (e) {
      errors.push(e.message);
      transport = null;
    }
  }
  throw new Error(errors[0] || 'нет связи с сервером');
}

export async function probeTransports() {
  const out = [];
  for (const plan of PLANS) {
    const url = plan.path('ping') + (plan.method === 'GET' ? '?p=' + encodeURIComponent(b64({})) : '');
    const t0 = performance.now();
    try {
      const d = await attempt(plan.method, url, {}, 8000);
      out.push({ ok: true, method: plan.method, url, ms: Math.round(performance.now() - t0), pid: d.pid });
    } catch (e) {
      out.push({ ok: false, method: plan.method, url, error: e.message });
    }
  }
  return out;
}

export const api = {
  stats: () => call('stats', {}, 10000),
  exec: (cmd, opts = {}) => call('exec', { cmd, root: opts.root, cwd: opts.cwd, id: opts.id || 'current' }, 95000),
  kill: (id = 'current') => call('kill', { id }),
  files: (path) => call('files', { path }),
  read: (path) => call('read', { path }),
  write: (path, text) => call('write', { path, text }),
  proc: () => call('proc'),
  killproc: (pid, opts = {}) => call('killproc', { pid, signal: opts.signal || 'TERM', root: opts.root }),
  fetchUrl: (url) => call('fetch', { url }, 25000),
  // ── real PTY session (xterm.js terminal) ─────────────────────────────
  ptyOpen: (opts = {}) => call('ptyopen', { root: !!opts.root, cols: opts.cols || 120, rows: opts.rows || 32 }, 15000),
  ptyRead: (id, from, wait = 18000) => call('ptyread', { id, from, wait }, wait + 8000),
  ptyWrite: (id, data) => call('ptywrite', { id, data }, 15000),
  ptyResize: (id, cols, rows) => call('ptyresize', { id, cols, rows }, 15000),
  ptyClose: (id) => call('ptyclose', { id }, 10000),
  downloadUrl: (path, inline = false) =>
    `/api/download?path=${encodeURIComponent(path)}${inline ? '&inline=1' : ''}`,
};
