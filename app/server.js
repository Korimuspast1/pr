'use strict';
/**
 * Sandbox Console — a web control panel for this Linux sandbox.
 *
 * Serves a terminal-styled UI (built to match the rendered desktop shot in
 * ./screenshot) plus a tiny JSON API:
 *
 *   GET  /api/stats        live host / git / sudo state
 *   POST /api/exec         { cmd, cwd?, root?, id? } -> { out, cwd, code, root }
 *   POST /api/kill         { id } -> kill a running command
 *   GET  /api/files?path=  directory listing
 *
 * No third-party dependencies. Node >= 18.
 */

const http = require('http');
const fs = require('fs');
const fsp = require('fs/promises');
const path = require('path');
const os = require('os');
const { spawn, execFileSync } = require('child_process');

const ROOT = path.resolve(__dirname, '..');
const PUBLIC = path.join(__dirname, 'public');
const SHELL = '/usr/bin/bash';
const PORT = Number(process.env.PORT || 4173);
const HOST = process.env.HOST || '0.0.0.0';
const MAX_OUT = 512 * 1024;
const DEFAULT_TIMEOUT = Number(process.env.EXEC_TIMEOUT || 90) * 1000;

const state = {
  cwd: ROOT,
  root: false,
};

const running = new Map();

/* ------------------------------------------------------------------ utils */

const q = (s) => "'" + String(s).replace(/'/g, `'\\''`) + "'";

function sh(cmd, args, opts = {}) {
  return new Promise((resolve) => {
    const child = spawn(cmd, args, { cwd: opts.cwd || ROOT, env: process.env });
    let out = '';
    let err = '';
    let done = false;
    const timer = setTimeout(() => {
      if (!done) child.kill('SIGKILL');
    }, opts.timeout || 8000);
    child.stdout.on('data', (d) => (out += d));
    child.stderr.on('data', (d) => (err += d));
    child.on('error', (e) => {
      clearTimeout(timer);
      if (done) return;
      done = true;
      resolve({ out: '', err: String(e && e.message), code: 127 });
    });
    child.on('close', (code) => {
      clearTimeout(timer);
      if (done) return;
      done = true;
      resolve({ out, err, code });
    });
  });
}

function memInfo() {
  const txt = fs.readFileSync('/proc/meminfo', 'utf8');
  const get = (k) => {
    const m = txt.match(new RegExp(`^${k}:\\s+(\\d+) kB`, 'm'));
    return m ? Number(m[1]) * 1024 : 0;
  };
  const total = get('MemTotal');
  const available = get('MemAvailable');
  return { total, used: total - available, available, swapTotal: get('SwapTotal'), swapFree: get('SwapFree') };
}

let lastCpu = null;
function cpuSample() {
  const line = fs.readFileSync('/proc/stat', 'utf8').split('\n')[0].trim().split(/\s+/).slice(1).map(Number);
  const idle = line[3] + (line[4] || 0);
  const total = line.reduce((a, b) => a + b, 0);
  return { idle, total };
}
function cpuPercent() {
  const now = cpuSample();
  let pct = 0;
  if (lastCpu) {
    const dt = now.total - lastCpu.total;
    const di = now.idle - lastCpu.idle;
    if (dt > 0) pct = Math.max(0, Math.min(100, ((dt - di) / dt) * 100));
  }
  lastCpu = now;
  return Math.round(pct);
}

function cpuModel() {
  try {
    const txt = fs.readFileSync('/proc/cpuinfo', 'utf8');
    const m = txt.match(/^model name\s*:\s*(.+)$/m);
    return m ? m[1].trim() : 'unknown';
  } catch {
    return 'unknown';
  }
}

let sudoCache = { at: 0, passwordless: false };
function sudoPasswordless() {
  if (Date.now() - sudoCache.at < 30000) return sudoCache.passwordless;
  let passwordless = false;
  try {
    execFileSync('sudo', ['-n', 'true'], { stdio: 'ignore', timeout: 4000 });
    passwordless = true;
  } catch {
    passwordless = false;
  }
  sudoCache = { at: Date.now(), passwordless };
  return passwordless;
}

let gitCache = { at: 0, data: null };
async function gitInfo() {
  if (Date.now() - gitCache.at < 4000 && gitCache.data) return gitCache.data;
  const branch = await sh('git', ['-C', ROOT, 'rev-parse', '--abbrev-ref', 'HEAD']);
  const head = await sh('git', ['-C', ROOT, 'log', '--oneline', '-1']);
  const status = await sh('git', ['-C', ROOT, 'status', '--porcelain']);
  const data = {
    branch: branch.out.trim() || '—',
    head: head.out.trim(),
    dirty: status.out.trim().length > 0,
  };
  gitCache = { at: Date.now(), data };
  return data;
}

async function diskInfo() {
  const { out } = await sh('df', ['-B1', '/']);
  const line = out.trim().split('\n')[1] || '';
  const p = line.split(/\s+/);
  const total = Number(p[1] || 0);
  const used = Number(p[2] || 0);
  const avail = Number(p[3] || 0);
  const pct = Number(String(p[4] || '0').replace('%', ''));
  return { total, used, avail, pct, mount: p[5] || '/' };
}

function shorten(p) {
  const home = os.homedir();
  return p.startsWith(home) ? '~' + p.slice(home.length) : p;
}

/* ------------------------------------------------------------------- exec */

function execCommand(raw, { cwd, root, id } = {}) {
  return new Promise((resolve) => {
    const workdir = cwd || state.cwd;
    const runAsRoot = root === undefined ? state.root : !!root;
    const inner = runAsRoot ? `sudo -n -- ${SHELL} -c ${q(raw)}` : raw;

    const script = [
      `cd ${q(workdir)} 2>/dev/null || cd ${q(ROOT)}`,
      `{ ${inner}\n } 2>&1`,
      `__rc=$?`,
      `printf '\\036%s\\036%s\\036' "$PWD" "$__rc"`,
    ].join('\n');

    let child;
    try {
      child = spawn(SHELL, ['-c', script], {
        cwd: workdir,
        env: {
          ...process.env,
          TERM: 'xterm-256color',
          PAGER: 'cat',
          GIT_PAGER: 'cat',
          DEBIAN_FRONTEND: 'noninteractive',
          SUDO_ASKPASS: '/bin/false',
        },
        detached: true,
        stdio: ['ignore', 'pipe', 'pipe'],
      });
    } catch (e) {
      return resolve({ out: String(e), cwd: workdir, code: 1 });
    }

    if (id) running.set(id, child);

    let buf = '';
    let truncated = false;
    const timer = setTimeout(() => {
      try {
        process.kill(-child.pid, 'SIGKILL');
      } catch {}
      truncated = true;
    }, DEFAULT_TIMEOUT);

    const push = (d) => {
      if (buf.length > MAX_OUT) return;
      buf += d.toString('utf8');
      if (buf.length > MAX_OUT) {
        buf = buf.slice(0, MAX_OUT) + '\n… output truncated …\n';
      }
    };

    child.stdout.on('data', push);
    child.stderr.on('data', push);
    child.on('error', (e) => push(`\n${e.message}\n`));
    child.on('close', (code) => {
      clearTimeout(timer);
      if (id) running.delete(id);
      const parts = buf.split('\x1e');
      let newCwd = workdir;
      let rc = code == null ? 1 : code;
      if (parts.length >= 4) {
        const tail = parts.slice(-3);
        newCwd = tail[0] || workdir;
        const n = Number(tail[1]);
        if (Number.isFinite(n)) rc = n;
        buf = parts.slice(0, -3).join('\x1e');
      }
      if (newCwd && fs.existsSync(newCwd)) state.cwd = newCwd;
      resolve({ out: buf, cwd: state.cwd, code: rc, root: runAsRoot, killed: truncated });
    });
  });
}

/* ------------------------------------------------------------------- http */

function json(res, obj, code = 200) {
  const body = JSON.stringify(obj);
  res.writeHead(code, {
    'content-type': 'application/json; charset=utf-8',
    'cache-control': 'no-store',
    'content-length': Buffer.byteLength(body),
  });
  res.end(body);
}

function readBody(req) {
  return new Promise((resolve) => {
    let data = '';
    req.on('data', (c) => {
      data += c;
      if (data.length > 1e6) req.destroy();
    });
    req.on('end', () => {
      try {
        resolve(data ? JSON.parse(data) : {});
      } catch {
        resolve({});
      }
    });
  });
}

/**
 * Parameters may arrive as a JSON body (POST) or as a base64 `p` query param
 * (GET) — some preview proxies mangle JSON POSTs, so both must work.
 */
async function params(req, url) {
  const body = req.method === 'POST' ? await readBody(req) : {};
  const raw = url.searchParams.get('p');
  if (!raw) return body;
  try {
    const decoded = JSON.parse(Buffer.from(raw, 'base64').toString('utf8'));
    return { ...decoded, ...body };
  } catch {
    return body;
  }
}

/** Routes are mirrored under /api/… and /x/… so a proxy that swallows one still works. */
function routeName(pathname) {
  const m = pathname.match(/^\/(?:api|x)\/([a-z]+)(?:\.json)?\/?$/);
  return m ? m[1] : null;
}

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
};

let vendorCache = { at: 0, xtermJs: '', xtermCss: '', cmCss: '' };
function inlineVendors() {
  if (Date.now() - vendorCache.at < 5000) return vendorCache;
  const read = (rel) => {
    try { return fs.readFileSync(path.join(PUBLIC, rel), 'utf8'); } catch { return ''; }
  };
  vendorCache = {
    at: Date.now(),
    xtermJs: read('vendor/xterm/xterm.js'),
    xtermCss: read('vendor/xterm/xterm.css'),
    cmCss: read('vendor/codemirror/lib/codemirror.css'),
  };
  return vendorCache;
}

let cssCache = { at: 0, css: '' };
function inlineCss() {
  if (Date.now() - cssCache.at < 3000) return cssCache.css;
  try {
    cssCache = { at: Date.now(), css: fs.readFileSync(path.join(PUBLIC, 'style.css'), 'utf8') };
  } catch {
    cssCache = { at: Date.now(), css: '' };
  }
  return cssCache.css;
}

async function serveStatic(res, urlPath) {
  const rel = urlPath === '/' ? 'index.html' : urlPath.replace(/^\/+/, '');
  const file = path.join(PUBLIC, rel);
  if (!file.startsWith(PUBLIC)) {
    res.writeHead(403).end('forbidden');
    return;
  }
  try {
    let buf = await fsp.readFile(file);
    if (rel === 'index.html') {
      // Ship styles + the terminal engine inline: preview proxies sometimes
      // rewrite or block asset responses (CSS arrives as HTML, big JS 502s).
      const v = inlineVendors();
      const html = buf.toString('utf8')
        .replace(
          /<link rel="stylesheet" href="\/style\.css"\s*\/?>/,
          `<style id="inline-css">${inlineCss()}</style>`
        )
        .replace(
          /<link rel="stylesheet" href="\/vendor\/codemirror\/lib\/codemirror\.css"\s*\/?>/,
          `<style id="cm-css">${v.cmCss}</style>`
        )
        .replace(
          /<link rel="stylesheet" href="\/vendor\/xterm\/xterm\.css"\s*\/?>/,
          `<style id="xterm-css">${v.xtermCss}</style>`
        )
        .replace(
          /<script[^>]*src="\/vendor\/xterm\/xterm\.js"[^>]*><\/script>/,
          `<script id="inline-xterm">${v.xtermJs}</script>`
        );
      buf = Buffer.from(html, 'utf8');
    }
    res.writeHead(200, {
      'content-type': MIME[path.extname(file)] || 'application/octet-stream',
      'cache-control': rel === 'index.html' ? 'no-store, max-age=0' : 'no-cache',
      'x-content-type-options': 'nosniff',
    });
    res.end(buf);
  } catch {
    res.writeHead(404, { 'content-type': 'text/plain' }).end('not found');
  }
}

async function fileList(dir) {
  const target = path.resolve(dir || state.cwd);
  const st = await fsp.stat(target);
  if (!st.isDirectory()) throw new Error('not a directory');
  const entries = await fsp.readdir(target, { withFileTypes: true });
  const items = [];
  for (const e of entries.slice(0, 400)) {
    let meta = { size: 0, mtime: 0, mode: '' };
    try {
      const s = await fsp.lstat(path.join(target, e.name));
      meta = { size: s.size, mtime: s.mtimeMs, mode: (s.mode & 0o777).toString(8).padStart(3, '0') };
    } catch {}
    items.push({
      name: e.name,
      dir: e.isDirectory(),
      link: e.isSymbolicLink(),
      ...meta,
    });
  }
  items.sort((a, b) => (b.dir - a.dir) || a.name.localeCompare(b.name));
  return { path: target, parent: path.dirname(target), items };
}


/* ------------------------------------------------------- desktop helpers */

function safeResolve(p, base) {
  const abs = path.isAbsolute(p || '') ? p : path.resolve(base || state.cwd, p || '.');
  return abs;
}

async function readFileSmart(target) {
  const s = await fsp.stat(target);
  if (s.isDirectory()) throw new Error('это каталог');
  const LIMIT = 512 * 1024;
  const size = s.size;
  const fh = await fsp.open(target, 'r');
  try {
    const buf = Buffer.alloc(Math.min(size, LIMIT));
    await fh.read(buf, 0, buf.length, 0);
    const binary = buf.includes(0);
    return {
      path: target,
      size,
      mtime: s.mtimeMs,
      mode: (s.mode & 0o777).toString(8).padStart(3, '0'),
      binary,
      truncated: size > LIMIT,
      text: binary ? buf.toString('base64') : buf.toString('utf8'),
      encoding: binary ? 'base64' : 'utf8',
    };
  } finally {
    await fh.close();
  }
}

let procCache = { at: 0, data: null };
async function procList() {
  if (Date.now() - procCache.at < 1200 && procCache.data) return procCache.data;
  const { out } = await sh('ps', ['-eo', 'pid=,ppid=,user=,pcpu=,pmem=,rss=,stat=,etimes=,comm=,args=', '--sort=-pcpu']);
  const rows = [];
  for (const line of out.split('\n')) {
    const t = line.trim();
    if (!t) continue;
    const m = t.match(/^(\d+)\s+(\d+)\s+(\S+)\s+([\d.]+)\s+([\d.]+)\s+(\d+)\s+(\S+)\s+(\d+)\s+(\S+)\s?(.*)$/);
    if (!m) continue;
    rows.push({
      pid: Number(m[1]), ppid: Number(m[2]), user: m[3],
      cpu: Number(m[4]), mem: Number(m[5]), rss: Number(m[6]) * 1024,
      stat: m[7], etimes: Number(m[8]), comm: m[9], args: m[10] || m[9],
    });
  }
  const data = { total: rows.length, rows: rows.slice(0, 80) };
  procCache = { at: Date.now(), data };
  return data;
}

/**
 * Fetch a page on behalf of the browser app.
 * Uses curl (system CA store — the sandbox sits behind a TLS-inspecting proxy
 * that Node's bundled root certificates do not trust).
 */
async function proxyFetch(target, opts = {}) {
  const u = new URL(target);
  if (!/^https?:$/.test(u.protocol)) throw new Error('только http/https');

  const tmp = await fsp.mkdtemp(path.join(os.tmpdir(), 'wx-fetch-'));
  const bodyPath = path.join(tmp, 'body');
  const headPath = path.join(tmp, 'head');
  const LIMIT = 2 * 1024 * 1024;

  try {
    const r = await sh('curl', [
      '-sS', '-L',
      '--max-time', String(Math.max(3, Math.round((opts.timeout || 5000) / 1000))),
      '--max-filesize', String(6 * 1024 * 1024),
      '--compressed',
      '-A', 'Mozilla/5.0 (X11; Linux x86_64) sandboxOS/1.0',
      '-H', 'Accept: text/html,application/xhtml+xml,application/json;q=0.9,*/*;q=0.8',
      '-H', 'Accept-Language: ru,en;q=0.8',
      '-D', headPath,
      '-o', bodyPath,
      '-w', '%{http_code}\n%{content_type}\n%{size_download}\n%{url_effective}',
      target,
    ], { timeout: Math.max(3000, opts.timeout || 5000) + 2500 });

    if (r.code !== 0 && !fs.existsSync(bodyPath)) {
      throw new Error((r.err || '').trim() || `curl exit ${r.code}`);
    }

    const meta = r.out.trim().split('\n');
    const status = Number(meta[0] || 0);
    const rawType = (meta[1] || '').split('\n').pop();
    const bytes = Number(meta[2] || 0);
    const finalUrl = (meta[3] || target).trim();

    let buf = await fsp.readFile(bodyPath);
    const truncated = buf.length > LIMIT || bytes > LIMIT;
    if (buf.length > LIMIT) buf = buf.subarray(0, LIMIT);

    return {
      ok: status >= 200 && status < 400,
      status: status || (r.code === 63 ? 413 : 502),
      url: finalUrl,
      contentType: rawType || '',
      truncated,
      bytes,
      text: buf.toString('utf8'),
    };
  } finally {
    fsp.rm(tmp, { recursive: true, force: true }).catch(() => {});
  }
}


/* ------------------------------------------------------------ pty sessions */

const ptys = new Map();
let ptySeq = 0;
const PTY_MAX = 24;

function ptyOpen({ root = false, cols = 120, rows = 32 } = {}) {
  if (ptys.size >= PTY_MAX) throw new Error('слишком много терминалов, закрой лишние');
  for (const [id, s] of ptys) {
    if (s.dead && Date.now() - s.at > 20000) { ptys.delete(id); }
  }
  const id = 'pty' + ++ptySeq;
  const args = [path.join(__dirname, 'pty-bridge.py')];
  const cmd = root ? 'sudo' : 'python3';
  const argv = root ? ['-n', 'python3', ...args] : args;

  const child = spawn(cmd, argv, {
    cwd: state.cwd,
    env: {
      ...process.env,
      PTY_SHELL: '/bin/bash',
      PTY_COLS: String(cols),
      PTY_ROWS: String(rows),
      TERM: 'xterm-256color',
      LANG: process.env.LANG || 'C.UTF-8',
    },
    stdio: ['pipe', 'pipe', 'pipe'],
    detached: true,
  });

  const session = { id, child, buf: '', offset: 0, root: !!root, dead: false, at: Date.now(), waiters: [] };
  const LIMIT = 400 * 1024;

  const push = (d) => {
    session.buf += d.toString('binary');
    if (session.buf.length > LIMIT) {
      const cut = session.buf.length - LIMIT;
      session.buf = session.buf.slice(cut);
      session.offset += cut;
    }
    session.at = Date.now();
    const waiters = session.waiters.splice(0);
    for (const w of waiters) w();
  };

  child.stdout.on('data', push);
  child.stderr.on('data', (d) => push(d));
  child.on('close', () => {
    session.dead = true;
    session.at = Date.now();
    push(Buffer.from('\r\n\x1b[90m[сессия завершена]\x1b[0m\r\n'));
  });
  child.on('error', (e) => {
    session.dead = true;
    push(Buffer.from(`\r\n\x1b[31m${e.message}\x1b[0m\r\n`));
  });

  ptys.set(id, session);
  return { id, root: session.root, cwd: state.cwd };
}

/** Long-poll: returns everything after `from`, waiting up to `wait` ms. */
function ptyRead(id, from, wait = 3000) {
  const s = ptys.get(id);
  if (!s) return Promise.resolve({ error: 'нет такой сессии' });
  const fromIdx = Math.max(0, Number(from) - s.offset);
  const slice = () => {
    const start = Math.min(Math.max(0, fromIdx), s.buf.length);
    return {
      data: s.buf.slice(start),
      offset: s.offset + s.buf.length,
      dead: s.dead,
    };
  };
  const first = slice();
  if (first.data.length || first.dead) return Promise.resolve(first);
  return new Promise((resolve) => {
    const timer = setTimeout(() => {
      s.waiters = s.waiters.filter((w) => w !== done);
      resolve(slice());
    }, wait);
    const done = () => {
      clearTimeout(timer);
      resolve(slice());
    };
    s.waiters.push(done);
  });
}

function ptyWrite(id, data) {
  const s = ptys.get(id);
  if (!s || s.dead) return false;
  try {
    s.child.stdin.write(Buffer.from(data, 'binary'));
    return true;
  } catch {
    return false;
  }
}

function ptyResize(id, cols, rows) {
  const s = ptys.get(id);
  if (!s || s.dead) return false;
  try {
    s.child.stdin.write(Buffer.from(`\u0001R${cols},${rows}\u0001`, 'binary'));
    return true;
  } catch {
    return false;
  }
}

function ptyClose(id) {
  const s = ptys.get(id);
  if (!s) return false;
  try {
    process.kill(-s.child.pid, 'SIGKILL');
  } catch {
    try { s.child.kill('SIGKILL'); } catch {}
  }
  ptys.delete(id);
  return true;
}


/** Run argv, return { out: Buffer, err: string, code } — binary safe. */
function runCapture(argv, { input = null, timeout = 20000, cwd = ROOT } = {}) {
  return new Promise((resolve) => {
    const child = spawn(argv[0], argv.slice(1), { cwd, env: process.env });
    const chunks = [];
    let err = '';
    const timer = setTimeout(() => { try { child.kill('SIGKILL'); } catch {} }, timeout);
    child.stdout.on('data', (d) => chunks.push(d));
    child.stderr.on('data', (d) => (err += d));
    child.on('close', (code) => {
      clearTimeout(timer);
      resolve({ out: Buffer.concat(chunks), err, code });
    });
    child.on('error', (e) => {
      clearTimeout(timer);
      resolve({ out: Buffer.alloc(0), err: String(e.message), code: 127 });
    });
    if (input != null) {
      child.stdin.end(input);
    } else {
      child.stdin.end();
    }
  });
}

const sudoMissing = () => false;

/** List a directory, optionally as root (needed for /root, /etc/shadow, …). */
async function listDir(dir, asRoot) {
  if (asRoot) {
    const r = await runCapture(['sudo', '-n', 'find', dir, '-maxdepth', '1', '-mindepth', '1',
      '-printf', '%y\t%s\t%T@\t%m\t%f\n']);
    if (r.code !== 0) throw new Error(cleanSudoErr(r.err, dir));
    const items = [];
    for (const line of r.out.toString('utf8').split('\n')) {
      if (!line.trim()) continue;
      const [type, size, mtime, mode, ...rest] = line.split('\t');
      const name = rest.join('\t');
      if (!name) continue;
      items.push({
        name,
        dir: type === 'd',
        link: type === 'l',
        size: Number(size) || 0,
        mtime: (Number(mtime) || 0) * 1000,
        mode: String(mode || '').padStart(3, '0'),
      });
    }
    items.sort((a, b) => (b.dir - a.dir) || a.name.localeCompare(b.name));
    return { path: dir, parent: path.dirname(dir), items, root: true };
  }
  return { ...(await fileList(dir)), root: false };
}

function cleanSudoErr(err, target) {
  const t = String(err || '').trim();
  if (/Permission denied|Отказано/i.test(t)) {
    return `нет доступа к ${target} — включи root mode (нужны права root)`;
  }
  if (/password is required|a password is required/i.test(t)) return 'sudo требует пароль, а он недоступен';
  return t.split('\n').slice(-2).join(' ') || 'не удалось прочитать каталог';
}

async function readAsRoot(target) {
  const LIMIT = 512 * 1024;
  const st = await runCapture(['sudo', '-n', 'stat', '-c', '%s\t%Y\t%a', '--', target]);
  if (st.code !== 0) throw new Error(cleanSudoErr(st.err, target));
  const [sizeStr, mtimeStr, modeStr] = st.out.toString().trim().split('\t');
  const size = Number(sizeStr) || 0;
  const head = await runCapture(['sudo', '-n', 'head', '-c', String(Math.min(size, LIMIT)), '--', target]);
  if (head.code !== 0) throw new Error(cleanSudoErr(head.err, target));
  const buf = head.out;
  const binary = buf.includes(0);
  return {
    path: target,
    size,
    mtime: (Number(mtimeStr) || 0) * 1000,
    mode: String(modeStr || '').padStart(3, '0'),
    binary,
    truncated: size > LIMIT,
    text: binary ? buf.toString('base64') : buf.toString('utf8'),
    encoding: binary ? 'base64' : 'utf8',
    root: true,
  };
}

async function writeAsRoot(target, text) {
  await runCapture(['sudo', '-n', 'mkdir', '-p', '--', path.dirname(target)]);
  const r = await runCapture(['sudo', '-n', 'tee', '--', target], { input: Buffer.from(text, 'utf8') });
  if (r.code !== 0) throw new Error(cleanSudoErr(r.err, target));
  const st = await runCapture(['sudo', '-n', 'stat', '-c', '%s\t%Y', '--', target]);
  const [size, mtime] = st.out.toString().trim().split('\t');
  return { ok: true, path: target, size: Number(size) || 0, mtime: (Number(mtime) || 0) * 1000, root: true };
}


const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  const name = routeName(url.pathname);
  try {
    if (name === 'stats') {
      const mem = memInfo();
      const disk = await diskInfo();
      const git = await gitInfo();
      return json(res, {
        host: os.hostname(),
        user: os.userInfo().username,
        uid: process.getuid(),
        kernel: `${os.type()} ${os.release()} ${os.arch()}`,
        cpuModel: cpuModel(),
        cores: os.cpus().length,
        cpu: cpuPercent(),
        load: os.loadavg().map((n) => Number(n.toFixed(2))),
        mem,
        disk,
        uptime: os.uptime(),
        node: process.version,
        python: (await sh('python3', ['-V'])).out.trim() || (await sh('python3', ['-V'])).err.trim(),
        cwd: state.cwd,
        cwdShort: shorten(state.cwd),
        rootMode: state.root,
        sudoPasswordless: sudoPasswordless(),
        git,
        running: running.size,
        pid: process.pid,
        transport: req.method,
        home: os.homedir(),
        repo: ROOT,
        time: Date.now(),
      });
    }

    if (name === 'exec') {
      const body = await params(req, url);
      const cmd = String(body.cmd || '');
      if (!cmd.trim()) return json(res, { out: '', cwd: state.cwd, cwdShort: shorten(state.cwd), code: 0 });
      const t0 = Date.now();
      const r = await execCommand(cmd, { cwd: body.cwd, root: body.root, id: body.id || 'current' });
      return json(res, { ...r, ms: Date.now() - t0, cwdShort: shorten(r.cwd) });
    }

    if (name === 'kill') {
      const body = await params(req, url);
      const child = running.get(body.id);
      if (child) {
        try {
          process.kill(-child.pid, 'SIGKILL');
        } catch {}
        return json(res, { killed: true });
      }
      return json(res, { killed: false });
    }

    if (name === 'files') {
      const body = await params(req, url);
      const p = body.path || url.searchParams.get('path') || state.cwd;
      const asRoot = body.root === true || body.root === 'true';
      try {
        return json(res, await listDir(p, asRoot));
      } catch (e) {
        const cleaned = cleanSudoErr(String(e.message || e), p);
        const needRoot = /нет доступа|root mode|EACCES|permission denied/i.test(cleaned || '');
        return json(res, { error: cleaned, needRoot }, 400);
      }
    }

    if (name === 'asset') {
      const body = await params(req, url);
      const assetMap = {
        'codemirror': 'vendor/codemirror/lib/codemirror.js',
        'codemirror-css': 'vendor/codemirror/lib/codemirror.css',
        'mode-javascript': 'vendor/codemirror/mode/javascript/javascript.js',
        'mode-clike': 'vendor/codemirror/mode/clike/clike.js',
        'mode-python': 'vendor/codemirror/mode/python/python.js',
        'mode-shell': 'vendor/codemirror/mode/shell/shell.js',
        'mode-css': 'vendor/codemirror/mode/css/css.js',
        'mode-xml': 'vendor/codemirror/mode/xml/xml.js',
        'mode-htmlmixed': 'vendor/codemirror/mode/htmlmixed/htmlmixed.js',
        'mode-markdown': 'vendor/codemirror/mode/markdown/markdown.js',
        'mode-yaml': 'vendor/codemirror/mode/yaml/yaml.js',
        'mode-sql': 'vendor/codemirror/mode/sql/sql.js',
      };
      const name2 = body.name || url.searchParams.get('name');
      const rel = assetMap[name2];
      if (!rel) return json(res, { error: 'неизвестный ассет' }, 404);
      try {
        const code = await fsp.readFile(path.join(PUBLIC, rel), 'utf8');
        return json(res, { name: name2, code });
      } catch (e) {
        return json(res, { error: 'ассет не найден: ' + rel }, 404);
      }
    }

    if (name === 'ptyopen') {
      const body = await params(req, url);
      try {
        return json(res, ptyOpen({
          root: body.root === true || body.root === 'true',
          cols: Number(body.cols) || 120,
          rows: Number(body.rows) || 32,
        }));
      } catch (e) {
        return json(res, { error: String(e.message || e) }, 400);
      }
    }

    if (name === 'ptyread') {
      const body = await params(req, url);
      const id = body.id || url.searchParams.get('id');
      const from = body.from ?? url.searchParams.get('from') ?? 0;
      const wait = Math.min(6000, Number(body.wait ?? url.searchParams.get('wait')) || 3000);
      return json(res, await ptyRead(id, from, wait));
    }

    if (name === 'ptywrite') {
      const body = await params(req, url);
      const ok = ptyWrite(body.id || url.searchParams.get('id'), body.data || '');
      return json(res, { ok });
    }

    if (name === 'ptyresize') {
      const body = await params(req, url);
      const ok = ptyResize(body.id, Number(body.cols), Number(body.rows));
      return json(res, { ok });
    }

    if (name === 'ptyclose') {
      const body = await params(req, url);
      return json(res, { ok: ptyClose(body.id || url.searchParams.get('id')) });
    }

    if (name === 'read') {
      const body = await params(req, url);
      const target = safeResolve(body.path || url.searchParams.get('path'), state.cwd);
      const asRoot = body.root === true || body.root === 'true';
      try {
        if (asRoot) return json(res, await readAsRoot(target));
        try {
          return json(res, await readFileSmart(target));
        } catch (e) {
          if (e && (e.code === 'EACCES' || e.code === 'EPERM')) {
            return json(res, { error: `нет доступа к ${target} — включи root mode`, needRoot: true }, 403);
          }
          throw e;
        }
      } catch (e) {
        return json(res, { error: String(e.message || e) }, 400);
      }
    }

    if (name === 'write') {
      const body = await params(req, url);
      const target = safeResolve(body.path, state.cwd);
      const asRoot = body.root === true || body.root === 'true';
      if (asRoot) {
        try {
          return json(res, await writeAsRoot(target, body.text ?? ''));
        } catch (e) {
          return json(res, { error: String(e.message || e), needRoot: true }, 400);
        }
      }
      try {
        await fsp.mkdir(path.dirname(target), { recursive: true });
        await fsp.writeFile(target, body.text ?? '', 'utf8');
        const st = await fsp.stat(target);
        return json(res, { ok: true, path: target, size: st.size, mtime: st.mtimeMs });
      } catch (e) {
        return json(res, { error: String(e.message || e) }, 400);
      }
    }

    if (name === 'download') {
      const target = safeResolve(url.searchParams.get('path'), state.cwd);
      const inline = url.searchParams.get('inline') === '1';
      const asRoot = url.searchParams.get('root') === '1';
      if (asRoot) {
        const st = await runCapture(['sudo', '-n', 'stat', '-c', '%s', '--', target]);
        if (st.code !== 0) return json(res, { error: cleanSudoErr(st.err, target) }, 403);
        const size = Number(st.out.toString().trim()) || 0;
        const type = MIME[path.extname(target).toLowerCase()] || 'application/octet-stream';
        res.writeHead(200, {
          'content-type': type,
          'content-length': size,
          'cache-control': 'no-store',
          'content-disposition': `${inline ? 'inline' : 'attachment'}; filename="${encodeURIComponent(path.basename(target))}"`,
        });
        const cat = spawn('sudo', ['-n', 'cat', '--', target]);
        cat.stdout.pipe(res);
        cat.on('close', () => res.end());
        return;
      }
      let st;
      try {
        st = await fsp.stat(target);
      } catch (e) {
        return json(res, { error: 'нет такого файла' }, 404);
      }
      if (st.isDirectory()) return json(res, { error: 'это каталог' }, 400);
      const type = MIME[path.extname(target).toLowerCase()] || 'application/octet-stream';
      res.writeHead(200, {
        'content-type': type,
        'content-length': st.size,
        'cache-control': 'no-store',
        'content-disposition': `${inline ? 'inline' : 'attachment'}; filename="${encodeURIComponent(path.basename(target))}"`,
      });
      fs.createReadStream(target).pipe(res);
      return;
    }

    if (name === 'proc') {
      return json(res, await procList());
    }

    if (name === 'killproc') {
      const body = await params(req, url);
      const pid = Number(body.pid);
      if (!pid || pid === process.pid) return json(res, { error: 'недопустимый pid' }, 400);
      const sig = String(body.signal || 'TERM').replace(/[^A-Z]/g, '') || 'TERM';
      const cmd = body.root ? `sudo -n kill -${sig} ${pid}` : `kill -${sig} ${pid}`;
      const r = await sh(SHELL, ['-c', cmd]);
      return json(res, { ok: r.code === 0, code: r.code, out: (r.out + r.err).trim() });
    }

    if (name === 'fetch') {
      const body = await params(req, url);
      const target = body.url || url.searchParams.get('url');
      if (!target) return json(res, { error: 'нужен url' }, 400);
      try {
        return json(res, await proxyFetch(target, { timeout: Math.min(6000, Number(body.timeout) || 5000) }));
      } catch (e) {
        return json(res, { error: String(e.message || e) }, 502);
      }
    }

    if (name === 'ping' || name === 'health') return json(res, { ok: true, pid: process.pid });

    // plain GET hints (useful for curl debugging through a proxy)
    if (url.pathname === '/api' || url.pathname === '/x') {
      return json(res, { ok: true, endpoints: ['stats', 'exec', 'kill', 'files', 'ping'] });
    }

    return serveStatic(res, url.pathname);
  } catch (e) {
    json(res, { error: String(e && e.stack ? e.stack : e) }, 500);
  }
});

server.listen(PORT, HOST, () => {
  const iface = `http://${HOST}:${PORT}`;
  console.log(`[sandbox-console] listening on ${iface}`);
  console.log(`[sandbox-console] cwd=${state.cwd}  sudo=${sudoPasswordless() ? 'passwordless' : 'no'}`);
});
