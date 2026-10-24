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
      '--max-time', String(Math.round((opts.timeout || 15000) / 1000)),
      '--max-filesize', String(6 * 1024 * 1024),
      '--compressed',
      '-A', 'Mozilla/5.0 (X11; Linux x86_64) sandboxOS/1.0',
      '-H', 'Accept: text/html,application/xhtml+xml,application/json;q=0.9,*/*;q=0.8',
      '-H', 'Accept-Language: ru,en;q=0.8',
      '-D', headPath,
      '-o', bodyPath,
      '-w', '%{http_code}\n%{content_type}\n%{size_download}\n%{url_effective}',
      target,
    ], { timeout: (opts.timeout || 15000) + 4000 });

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
      try {
        return json(res, await fileList(p));
      } catch (e) {
        return json(res, { error: String(e.message || e) }, 400);
      }
    }

    if (name === 'read') {
      const body = await params(req, url);
      const target = safeResolve(body.path || url.searchParams.get('path'), state.cwd);
      try {
        return json(res, await readFileSmart(target));
      } catch (e) {
        return json(res, { error: String(e.message || e) }, 400);
      }
    }

    if (name === 'write') {
      const body = await params(req, url);
      const target = safeResolve(body.path, state.cwd);
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
        return json(res, await proxyFetch(target, { timeout: Number(body.timeout) || undefined }));
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
