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
const DEFAULT_TIMEOUT = 180 * 1000;

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
        env: { ...process.env, TERM: 'xterm-256color', PAGER: 'cat', GIT_PAGER: 'cat' },
        detached: true,
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

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
};

async function serveStatic(res, urlPath) {
  const rel = urlPath === '/' ? 'index.html' : urlPath.replace(/^\/+/, '');
  const file = path.join(PUBLIC, rel);
  if (!file.startsWith(PUBLIC)) {
    res.writeHead(403).end('forbidden');
    return;
  }
  try {
    const buf = await fsp.readFile(file);
    res.writeHead(200, {
      'content-type': MIME[path.extname(file)] || 'application/octet-stream',
      'cache-control': 'no-cache',
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

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  try {
    if (url.pathname === '/api/stats' && req.method === 'GET') {
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
        python: (await sh('python3', ['-V'])).err.trim() || (await sh('python3', ['-V'])).out.trim(),
        cwd: state.cwd,
        cwdShort: shorten(state.cwd),
        rootMode: state.root,
        sudoPasswordless: sudoPasswordless(),
        git,
        running: running.size,
        time: Date.now(),
      });
    }

    if (url.pathname === '/api/exec' && req.method === 'POST') {
      const body = await readBody(req);
      const cmd = String(body.cmd || '');
      if (!cmd.trim()) return json(res, { out: '', cwd: state.cwd, code: 0 });
      const t0 = Date.now();
      const r = await execCommand(cmd, { cwd: body.cwd, root: body.root, id: body.id || 'current' });
      return json(res, { ...r, ms: Date.now() - t0, cwdShort: shorten(r.cwd) });
    }

    if (url.pathname === '/api/kill' && req.method === 'POST') {
      const body = await readBody(req);
      const child = running.get(body.id);
      if (child) {
        try {
          process.kill(-child.pid, 'SIGKILL');
        } catch {}
        return json(res, { killed: true });
      }
      return json(res, { killed: false });
    }

    if (url.pathname === '/api/files' && req.method === 'GET') {
      const p = url.searchParams.get('path') || state.cwd;
      try {
        return json(res, await fileList(p));
      } catch (e) {
        return json(res, { error: String(e.message || e) }, 400);
      }
    }

    if (url.pathname === '/api/health') return json(res, { ok: true });

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
