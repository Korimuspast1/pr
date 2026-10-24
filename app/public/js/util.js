/* Small shared helpers. */

export const esc = (s) =>
  String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

export function humanSize(bytes) {
  if (bytes === 0 || bytes == null) return '0 B';
  const u = ['B', 'K', 'M', 'G', 'T'];
  let i = 0;
  let n = Number(bytes);
  while (n >= 1024 && i < u.length - 1) { n /= 1024; i++; }
  return `${n >= 10 || i === 0 ? Math.round(n) : n.toFixed(1)} ${u[i]}`;
}

export function humanUptime(sec) {
  const d = Math.floor(sec / 86400);
  const h = Math.floor((sec % 86400) / 3600);
  const m = Math.floor((sec % 3600) / 60);
  if (d) return `${d} д ${h} ч`;
  if (h) return `${h} ч ${m} мин`;
  return `${m} мин`;
}

export function fmtDate(ms) {
  if (!ms) return '—';
  const d = new Date(ms);
  const pad = (n) => String(n).padStart(2, '0');
  return `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export const shortenPath = (p) => String(p || '').replace(/^\/home\/[^/]+/, '~');

/* ── ANSI → HTML for terminal output ─────────────────────────────────── */
const ANSI = {
  30: '#484f58', 31: '#ff7b72', 32: '#7ee787', 33: '#e3b341', 34: '#79c0ff',
  35: '#d2a8ff', 36: '#56d4dd', 37: '#e6edf3', 90: '#6e7681', 91: '#ff7b72',
  92: '#7ee787', 93: '#e3b341', 94: '#79c0ff', 95: '#d2a8ff', 96: '#56d4dd', 97: '#ffffff',
};

export function ansiToHTML(str) {
  const st = { fg: null, bold: false, dim: false, underline: false };
  const wrap = (t) => {
    if (!t) return '';
    const css = [];
    if (st.fg) css.push(`color:${st.fg}`);
    if (st.bold) css.push('font-weight:700');
    if (st.dim) css.push('opacity:.62');
    if (st.underline) css.push('text-decoration:underline');
    return css.length ? `<span style="${css.join(';')}">${esc(t)}</span>` : esc(t);
  };
  let out = '';
  let last = 0;
  const re = /\x1b\[([0-9;]*)m/g;
  let m;
  while ((m = re.exec(str))) {
    out += wrap(str.slice(last, m.index));
    last = re.lastIndex;
    const codes = m[1].split(';').filter(Boolean).map(Number);
    if (!codes.length) codes.push(0);
    for (const c of codes) {
      if (c === 0) Object.assign(st, { fg: null, bold: false, dim: false, underline: false });
      else if (c === 1) st.bold = true;
      else if (c === 2) st.dim = true;
      else if (c === 4) st.underline = true;
      else if (c === 22) { st.bold = false; st.dim = false; }
      else if (c === 24) st.underline = false;
      else if (c === 39) st.fg = null;
      else if (ANSI[c]) st.fg = ANSI[c];
    }
  }
  return out + wrap(str.slice(last));
}

export function debounce(fn, ms) {
  let t;
  return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); };
}

export function el(tag, cls, html) {
  const n = document.createElement(tag);
  if (cls) n.className = cls;
  if (html != null) n.innerHTML = html;
  return n;
}
