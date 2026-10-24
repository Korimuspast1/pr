'use strict';
/* Sandbox Console — front end (vanilla, no deps) */

const $ = (id) => document.getElementById(id);
const out = $('out');
const input = $('input');
const screenEl = $('screen');
const strip = document.querySelector('.statusstrip');

let rootMode = false;
let cwd = '/home/user/pr';
let cwdShort = '~/pr';
let busy = false;
const history = [];
let hIndex = 0;
let booted = false;

/* ── ANSI → HTML ─────────────────────────────────────────────────────── */
const C = {
  30: '#484f58', 31: '#ff7b72', 32: '#7ee787', 33: '#e3b341', 34: '#79c0ff',
  35: '#d2a8ff', 36: '#56d4dd', 37: '#e6edf3', 90: '#6e7681', 91: '#ff7b72',
  92: '#7ee787', 93: '#e3b341', 94: '#79c0ff', 95: '#d2a8ff', 96: '#56d4dd', 97: '#ffffff',
};
const esc = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

function ansiLine(str) {
  let html = '';
  let open = false;
  const state = { fg: null, bold: false, dim: false };
  const wrap = (t) => {
    if (!t) return '';
    const style = [];
    if (state.fg) style.push(`color:${state.fg}`);
    if (state.bold) style.push('font-weight:700');
    if (state.dim) style.push('opacity:.6');
    return style.length ? `<span style="${style.join(';')}">${esc(t)}</span>` : esc(t);
  };
  const re = /\x1b\[([0-9;]*)m/g;
  let last = 0;
  let m;
  while ((m = re.exec(str))) {
    html += wrap(str.slice(last, m.index));
    last = re.lastIndex;
    const codes = m[1].split(';').filter((x) => x !== '').map(Number);
    if (!codes.length) codes.push(0);
    for (const c of codes) {
      if (c === 0) { state.fg = null; state.bold = false; state.dim = false; }
      else if (c === 1) state.bold = true;
      else if (c === 2) state.dim = true;
      else if (c === 22) { state.bold = false; state.dim = false; }
      else if (c === 39) state.fg = null;
      else if (C[c]) state.fg = C[c];
    }
  }
  html += wrap(str.slice(last));
  open = false;
  return html;
}

function renderOutput(text) {
  const lines = String(text).replace(/\r\n/g, '\n').split('\n');
  const frag = document.createDocumentFragment();
  for (const raw of lines) {
    const line = raw.includes('\r') ? raw.slice(raw.lastIndexOf('\r') + 1) : raw;
    const div = document.createElement('div');
    div.className = 'line';
    div.innerHTML = ansiLine(line) || '&nbsp;';
    frag.appendChild(div);
  }
  out.appendChild(frag);
}

function scrollDown() {
  screenEl.scrollTop = screenEl.scrollHeight;
}

function promptHTML() {
  const user = rootMode ? 'root@e2b' : 'user@e2b';
  const sign = rootMode ? '#' : '$';
  return `<span class="u">${user}</span><span class="c">:</span><span class="p">${esc(cwdShort)}</span><span class="s">${sign}</span> `;
}

function echoCommand(cmd) {
  const div = document.createElement('div');
  div.className = 'line cmd';
  div.innerHTML = promptHTML() + `<span style="color:#e6edf3">${esc(cmd)}</span>`;
  out.appendChild(div);
  syncPrompt();
}

function setStatus(txt, cls) {
  $('stripLeft').textContent = txt;
  strip.classList.toggle('busy', cls === 'busy');
}

function syncPrompt() {
  $('pUser').textContent = rootMode ? 'root@e2b' : 'user@e2b';
  $('pSign').textContent = rootMode ? '#' : '$';
  $('pPath').textContent = cwdShort;
  $('cwdTop').textContent = cwdShort;
  $('winTitle').textContent = `${rootMode ? 'root' : 'user'}@e2b: ${cwdShort} — bash`;
  $('rootChip').textContent = rootMode ? 'root' : 'user';
  $('rootChip').classList.toggle('root', rootMode);
  $('rWho').textContent = rootMode ? 'root (via sudo)' : 'user';
  $('rWho').className = rootMode ? 'green' : '';
}

/* ── command execution ───────────────────────────────────────────────── */
async function run(cmd) {
  if (busy) return;
  const trimmed = cmd.trim();
  if (!trimmed) return;

  if (trimmed === 'clear' || trimmed === 'cls') {
    out.innerHTML = '';
    history.push(trimmed);
    hIndex = history.length;
    return;
  }

  echoCommand(trimmed);
  history.push(trimmed);
  hIndex = history.length;

  busy = true;
  input.disabled = true;
  setStatus(rootMode ? 'running as root…' : 'running…', 'busy');
  const t0 = performance.now();

  try {
    const res = await fetch('/api/exec', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ cmd: trimmed, root: rootMode, id: 'current' }),
    });
    const data = await res.json();
    if (data.out) renderOutput(data.out);
    if (typeof data.cwdShort === 'string') { cwdShort = data.cwdShort; cwd = data.cwd; }
    const code = data.code ?? 0;
    const ms = Math.round(performance.now() - t0);
    setStatus(`${code === 0 ? 'exit 0' : 'exit ' + code} · ${ms} ms${data.killed ? ' · killed' : ''}`);
    syncPrompt();
    if (cwd !== filesPath) loadFiles(cwd);
  } catch (e) {
    renderOutput(`\x1b[31m${e.message}\x1b[0m`);
    setStatus('network error');
  } finally {
    busy = false;
    input.disabled = false;
    input.focus();
    scrollDown();
  }
}

/* ── stats ───────────────────────────────────────────────────────────── */
function human(bytes) {
  const u = ['B', 'K', 'M', 'G', 'T'];
  let i = 0;
  let n = bytes;
  while (n >= 1024 && i < u.length - 1) { n /= 1024; i++; }
  return `${n >= 10 || i === 0 ? Math.round(n) : n.toFixed(1)}${u[i]}`;
}
function uptime(sec) {
  const d = Math.floor(sec / 86400);
  const h = Math.floor((sec % 86400) / 3600);
  const m = Math.floor((sec % 3600) / 60);
  return d ? `${d}d ${h}h` : h ? `${h}h ${m}m` : `${m}m`;
}

function setBar(id, pct, valId, label) {
  const el = $(id);
  el.style.width = Math.max(1, Math.min(100, pct)) + '%';
  el.classList.toggle('hot', pct > 80);
  if (valId) $(valId).textContent = label;
}

let lastStats = null;

async function pollStats() {
  try {
    const s = await (await fetch('/api/stats')).json();
    lastStats = s;
    $('hostname').textContent = s.host;
    $('sHost').textContent = s.host;
    $('sKernel').textContent = s.kernel.replace('Linux ', 'Linux ');
    $('sCpu').textContent = `${s.cores}× ${s.cpuModel.replace(/\(R\)|\(TM\)|\sCPU|@.*/g, '').trim()}`;
    setBar('mCpu', s.cpu, 'mCpuVal', `${s.cpu}% · load ${s.load[0]}`);
    const memPct = (s.mem.used / s.mem.total) * 100;
    setBar('mMem', memPct, 'mMemVal', `${human(s.mem.used)} / ${human(s.mem.total)}`);
    setBar('mDisk', s.disk.pct, 'mDiskVal', `${human(s.disk.used)} / ${human(s.disk.total)} (${s.disk.pct}%)`);
    $('sUptime').textContent = uptime(s.uptime);
    $('sRuntimes').textContent = `${s.node} · ${(s.python || '').replace('Python ', 'py ')}`;
    $('branch').textContent = s.git.branch;
    $('gitState').textContent = s.git.dirty ? 'git dirty' : 'git clean';
    $('gitState').style.color = s.git.dirty ? 'var(--yellow)' : '';
    $('rWho').textContent = rootMode ? 'root (via sudo)' : s.user;
    $('rSudo').textContent = s.sudoPasswordless ? 'passwordless ✓' : 'password required';
    $('rSudo').className = s.sudoPasswordless ? 'green' : 'red';
    if (!cwd || cwd === '/home/user/pr') { cwd = s.cwd; cwdShort = s.cwdShort; }
    if (booted) syncPrompt();
  } catch {}
}

/* ── files ───────────────────────────────────────────────────────────── */
let filesPath = '/home/user/pr';
let filesParent = '/home/user';

async function loadFiles(path) {
  try {
    const res = await fetch('/api/files?path=' + encodeURIComponent(path));
    const data = await res.json();
    if (data.error) return;
    filesPath = data.path;
    filesParent = data.parent;
    $('fPath').textContent = data.path.replace(/^\/home\/user/, '~');
    const list = $('fList');
    list.innerHTML = '';
    for (const it of data.items) {
      const row = document.createElement('div');
      row.className = 'row' + (it.dir ? ' dir' : '');
      const nm = document.createElement('span');
      nm.textContent = it.name + (it.dir ? '/' : '');
      const sz = document.createElement('span');
      sz.className = 'size';
      sz.textContent = it.dir ? 'dir' : human(it.size);
      row.append(nm, sz);
      row.onclick = () => {
        if (it.dir) {
          loadFiles(filesPath.replace(/\/$/, '') + '/' + it.name);
        } else {
          const full = filesPath.replace(/\/$/, '') + '/' + it.name;
          input.value = `cat '${full}'`;
          input.focus();
        }
      };
      list.appendChild(row);
    }
  } catch {}
}

$('fUp').onclick = () => loadFiles(filesParent);

/* ── boot banner ─────────────────────────────────────────────────────── */
function banner(s, files) {
  const dirs = files.items.filter((i) => i.dir).map((i) => `<span style="color:#79c0ff;font-weight:700">${esc(i.name)}</span>`);
  const regs = files.items.filter((i) => !i.dir).map((i) => `<span style="color:#56d4dd">${esc(i.name)}</span>`);
  const lines = [
    `<span style="color:#7ee787;font-weight:700">sandbox console</span> <span style="color:#6e7681">— web control panel · root-ready</span>`,
    `<span style="color:#6e7681">kernel </span><span style="color:#56d4dd">${esc(s.kernel)}</span>`,
    `<span style="color:#6e7681">cpu    </span><span style="color:#56d4dd">${s.cores} vCPU · ${esc(s.cpuModel)}</span>`,
    `<span style="color:#6e7681">memory </span><span style="color:#56d4dd">${human(s.mem.used)} / ${human(s.mem.total)}</span><span style="color:#6e7681"> · disk </span><span style="color:#56d4dd">${human(s.disk.used)} / ${human(s.disk.total)} (${s.disk.pct}%)</span>`,
    `<span style="color:#6e7681">runtimes </span><span style="color:#56d4dd">node ${esc(s.node)} · ${esc(s.python)}</span>`,
    `<span style="color:#6e7681">git    </span><span style="color:#e3b341;font-weight:700">${esc(s.git.head.slice(0, 7))}</span> <span style="color:#8b949e">${esc(s.git.head.slice(8))}</span>`,
    `<span style="color:#6e7681">branch </span><span style="color:#7ee787;font-weight:700">${esc(s.git.branch)}</span><span style="color:#6e7681">${s.git.dirty ? ' · dirty' : ' · clean'}</span>`,
    `<span style="color:#6e7681">files  </span>${[...regs, ...dirs].join('   ')}`,
    '',
    `<span style="color:#6e7681">подсказка: </span><span style="color:#e6edf3">включи root mode справа или пиши </span><span style="color:#79c0ff">sudo -n …</span>`,
  ];
  const frag = document.createDocumentFragment();
  for (const l of lines) {
    const d = document.createElement('div');
    d.className = 'line';
    d.innerHTML = l || '&nbsp;';
    frag.appendChild(d);
  }
  out.appendChild(frag);
  out.appendChild(Object.assign(document.createElement('div'), { className: 'gap' }));
}

async function boot() {
  try {
    const [s, files] = await Promise.all([
      (await fetch('/api/stats')).json(),
      (await fetch('/api/files?path=/home/user/pr')).json(),
    ]);
    banner(s, files);
    cwd = s.cwd;
    cwdShort = s.cwdShort;
    filesPath = files.path;
    $('branch').textContent = s.git.branch;
    booted = true;
    syncPrompt();
    loadFiles(files.path);
    setStatus('ready');
    input.focus();
  } catch (e) {
    renderOutput('не удалось связаться с сервером: ' + e.message);
  }
}

/* ── input wiring ────────────────────────────────────────────────────── */
input.addEventListener('keydown', async (e) => {
  if (e.key === 'Enter') {
    const cmd = input.value;
    input.value = '';
    await run(cmd);
  } else if (e.key === 'ArrowUp') {
    e.preventDefault();
    if (!history.length) return;
    hIndex = Math.max(0, hIndex - 1);
    input.value = history[hIndex] || '';
    input.setSelectionRange(input.value.length, input.value.length);
  } else if (e.key === 'ArrowDown') {
    e.preventDefault();
    if (!history.length) return;
    hIndex = Math.min(history.length, hIndex + 1);
    input.value = history[hIndex] || '';
  } else if (e.key === 'l' && e.ctrlKey) {
    e.preventDefault();
    out.innerHTML = '';
  } else if (e.key === 'c' && e.ctrlKey) {
    e.preventDefault();
    if (busy) {
      fetch('/api/kill', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ id: 'current' }) });
      setStatus('interrupted');
    } else {
      echoCommand('^C');
      input.value = '';
    }
  }
});

document.addEventListener('click', (e) => {
  if (e.target.closest('.quick button')) {
    const cmd = e.target.closest('button').dataset.cmd;
    if (cmd) run(cmd);
  }
  if (window.getSelection().toString()) return;
  setTimeout(() => { if (!window.getSelection().toString()) input.focus(); }, 0);
});

screenEl.addEventListener('click', () => input.focus());

$('rootToggle').addEventListener('change', (e) => {
  rootMode = e.target.checked;
  syncPrompt();
  renderOutput(
    rootMode
      ? '\x1b[33mroot mode ON\x1b[0m — команды пойдут через \x1b[36msudo -n\x1b[0m'
      : '\x1b[90mroot mode OFF\x1b[0m — снова обычный пользователь'
  );
  scrollDown();
  input.focus();
});

/* ── clock ───────────────────────────────────────────────────────────── */
function tickClock() {
  const d = new Date();
  const day = d.toLocaleDateString('en-GB', { weekday: 'short' });
  const rest = d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short' });
  const time = d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  $('clock').textContent = `${day} ${rest}  ${time}`;
}
tickClock();
setInterval(tickClock, 10000);

pollStats();
setInterval(pollStats, 2000);
boot();
