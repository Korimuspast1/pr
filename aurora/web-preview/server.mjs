import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import net from 'node:net';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { WebSocketServer } from 'ws';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC = path.join(__dirname, 'public');
const RUN = path.join(__dirname, 'run');
fs.mkdirSync(RUN, { recursive: true });

const PORT = Number(process.env.PORT || 8080);
const VNC_HOST = '127.0.0.1';
const VNC_PORT = Number(process.env.VNC_PORT || 5901);
const QEMU_BIN = process.env.QEMU_BIN || 'qemu-system-x86_64';
const ISO = process.env.AURORA_ISO || path.resolve(__dirname, '../dist/aurora.iso');
const QMP_SOCK = path.join(RUN, 'qmp.sock');
const SERIAL_LOG = path.join(RUN, 'serial.log');
const QEMU_LOG = path.join(RUN, 'qemu.log');

if (!fs.existsSync(ISO)) {
  console.error(`Aurora ISO not found: ${ISO}`);
  process.exit(1);
}

const qemuArgs = [
  '-machine', 'pc', '-accel', 'tcg', '-cpu', 'qemu64', '-smp', '1', '-m', '512M',
  '-cdrom', ISO, '-boot', 'order=d', '-vga', 'std', '-display', 'none',
  '-nic', 'none',
  '-vnc', `${VNC_HOST}:${VNC_PORT - 5900}`,
  '-serial', `file:${SERIAL_LOG}`,
  '-qmp', `unix:${QMP_SOCK},server=on,wait=off`,
  '-no-shutdown',
];

let qemu = null;
let qemuState = 'stopped';
let qemuLogStream = null;
let restarting = false;

function removeStaleFiles() {
  for (const p of [QMP_SOCK]) { try { fs.unlinkSync(p); } catch {} }
}

function startQemu({ fresh = false } = {}) {
  if (qemu) return;
  removeStaleFiles();
  if (fresh) {
    try { fs.writeFileSync(SERIAL_LOG, ''); } catch {}
    try { fs.writeFileSync(QEMU_LOG, ''); } catch {}
  }
  qemuState = 'starting';
  qemuLogStream = fs.createWriteStream(QEMU_LOG, { flags: 'a' });
  const child = spawn(QEMU_BIN, qemuArgs, { stdio: ['ignore', 'pipe', 'pipe'] });
  qemu = child;
  child.stdout.pipe(qemuLogStream);
  child.stderr.pipe(qemuLogStream);
  child.on('spawn', () => { if (qemu === child) qemuState = 'running'; });
  child.on('exit', (code, signal) => {
    if (qemu === child) { qemuState = `exited:${code ?? signal}`; qemu = null; }
    try { qemuLogStream?.end(); } catch {}
  });
  child.on('error', (err) => {
    if (qemu === child) { qemuState = `error:${err.message}`; qemu = null; }
    try { qemuLogStream?.end(); } catch {}
  });
}

function stopQemu() {
  return new Promise((resolve) => {
    const child = qemu;
    if (!child) return resolve();
    const timer = setTimeout(() => { try { child.kill('SIGKILL'); } catch {} }, 2500);
    child.once('exit', () => { clearTimeout(timer); resolve(); });
    try { child.kill('SIGTERM'); } catch { clearTimeout(timer); resolve(); }
  });
}

function qmpCommand(command, args = {}) {
  return new Promise((resolve, reject) => {
    const sock = net.createConnection(QMP_SOCK);
    let buf = '';
    let stage = 0;
    let done = false;
    const finish = (err, value) => {
      if (done) return;
      done = true;
      clearTimeout(timer);
      sock.destroy();
      if (err) reject(err); else resolve(value);
    };
    const timer = setTimeout(() => finish(new Error('QMP timeout')), 4000);
    sock.on('error', (e) => finish(e));
    sock.on('close', () => finish(new Error('QMP connection closed')));
    sock.on('data', (d) => {
      buf += d.toString('utf8');
      let idx;
      while ((idx = buf.indexOf('\n')) >= 0) {
        const line = buf.slice(0, idx).trim(); buf = buf.slice(idx + 1);
        if (!line) continue;
        let msg; try { msg = JSON.parse(line); } catch { continue; }
        if (stage === 0 && msg.QMP) {
          stage = 1; sock.write(JSON.stringify({ execute: 'qmp_capabilities' }) + '\n');
        } else if (stage === 1 && msg.return) {
          stage = 2; sock.write(JSON.stringify({ execute: command, arguments: args }) + '\n');
        } else if (stage === 2 && (msg.return || msg.error)) {
          if (msg.error) finish(new Error(msg.error.desc || 'QMP error')); else finish(null, msg.return);
          return;
        }
      }
    });
  });
}

const MIME = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8', '.png': 'image/png',
  '.svg': 'image/svg+xml', '.ico': 'image/x-icon', '.txt': 'text/plain; charset=utf-8', '.wasm': 'application/wasm',
};

function send(res, code, body, type = 'text/plain; charset=utf-8') {
  if (res.writableEnded) return;
  res.writeHead(code, { 'Content-Type': type, 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' });
  res.end(body);
}
function json(res, code, obj) { send(res, code, JSON.stringify(obj), MIME['.json']); }

async function readBody(req, limit = 4096) {
  const chunks = []; let size = 0;
  for await (const c of req) { size += c.length; if (size > limit) throw new Error('body too large'); chunks.push(c); }
  return Buffer.concat(chunks).toString('utf8');
}

function lastBootLog() {
  try {
    const data = fs.readFileSync(SERIAL_LOG, 'utf8');
    const i = data.lastIndexOf('[boot] Aurora');
    return i >= 0 ? data.slice(i) : data;
  } catch { return ''; }
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://localhost');
  try {
    if (url.pathname === '/api/status') {
      const boot = lastBootLog();
      const status = {
        qemu: qemuState,
        iso: path.basename(ISO),
        ready: boot.includes('[boot] READY'),
        firmware: /firmware=UEFI64/.test(boot) ? 'UEFI' : 'BIOS',
        resolution: (boot.match(/desktop=(\d+x\d+)/) || [])[1] || null,
      };
      try { status.qmp = await qmpCommand('query-status'); } catch (e) { status.qmpError = String(e.message || e); }
      return json(res, 200, status);
    }
    if (url.pathname === '/api/serial') {
      const lines = lastBootLog().split(/\r?\n/);
      return send(res, 200, lines.slice(-250).join('\n'));
    }

    if (url.pathname.startsWith('/api/') && req.method === 'POST') {
      if (req.headers['x-aurora-control'] !== '1') return json(res, 403, { error: 'control header required' });
      if (url.pathname === '/api/reboot') {
        await qmpCommand('system_reset');
        return json(res, 200, { ok: true });
      }
      if (url.pathname === '/api/send-key') {
        const { keys } = JSON.parse((await readBody(req)) || '{}');
        if (!Array.isArray(keys) || keys.length === 0 || keys.length > 8) return json(res, 400, { error: 'keys array required' });
        await qmpCommand('send-key', { keys: keys.map(k => typeof k === 'string' ? { type: 'qcode', data: k } : k) });
        return json(res, 200, { ok: true });
      }
      if (url.pathname === '/api/restart-qemu') {
        if (restarting) return json(res, 409, { error: 'restart already in progress' });
        restarting = true;
        try { await stopQemu(); startQemu({ fresh: true }); return json(res, 200, { ok: true }); }
        finally { restarting = false; }
      }
      return json(res, 404, { error: 'unknown API' });
    }

    let pathname;
    try { pathname = decodeURIComponent(url.pathname); } catch { return send(res, 400, 'Bad path'); }
    if (pathname === '/') pathname = '/index.html';
    const file = path.normalize(path.join(PUBLIC, pathname));
    if (file !== PUBLIC && !file.startsWith(PUBLIC + path.sep)) return send(res, 403, 'Forbidden');
    fs.readFile(file, (err, data) => {
      if (err) return send(res, 404, 'Not found');
      res.writeHead(200, { 'Content-Type': MIME[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
      res.end(data);
    });
  } catch (e) {
    json(res, 500, { error: String(e.message || e) });
  }
});

const wss = new WebSocketServer({
  noServer: true,
  perMessageDeflate: false,
  maxPayload: 1024 * 1024,
  handleProtocols: (protocols) => (protocols.has('binary') ? 'binary' : false),
});

server.on('upgrade', (req, socket, head) => {
  const { pathname } = new URL(req.url, 'http://localhost');
  if (pathname !== '/websockify') { socket.destroy(); return; }
  wss.handleUpgrade(req, socket, head, (ws) => {
    try { socket.setNoDelay(true); } catch {}
    const tcp = net.createConnection({ host: VNC_HOST, port: VNC_PORT });
    tcp.setNoDelay(true);
    let opened = false;
    let inFlight = 0;
    const HIGH_WATER = 1024 * 1024;
    const LOW_WATER = 256 * 1024;
    const pending = [];
    tcp.on('connect', () => { opened = true; for (const b of pending) tcp.write(b); pending.length = 0; });
    tcp.on('data', (d) => {
      if (ws.readyState !== ws.OPEN) return;
      inFlight += d.length;
      ws.send(d, { binary: true }, (err) => {
        inFlight = Math.max(0, inFlight - d.length);
        if (err) { try { ws.close(); } catch {} return; }
        if (tcp.isPaused() && inFlight <= LOW_WATER) tcp.resume();
      });
      // Backpressure: do not build an unbounded queue for a slow browser link.
      if (inFlight >= HIGH_WATER) tcp.pause();
    });
    tcp.on('error', () => { try { ws.close(); } catch {} });
    tcp.on('close', () => { try { ws.close(); } catch {} });
    ws.on('message', (data) => {
      const b = Buffer.isBuffer(data) ? data : Buffer.from(data);
      if (opened) tcp.write(b); else if (pending.length < 256) pending.push(b);
    });
    ws.on('close', () => tcp.destroy());
    ws.on('error', () => tcp.destroy());
  });
});

startQemu({ fresh: true });
server.listen(PORT, '0.0.0.0', () => {
  console.log(`Aurora web preview on http://0.0.0.0:${PORT}`);
  console.log(`Proxying /websockify to ${VNC_HOST}:${VNC_PORT}`);
  console.log(`QEMU: ${QEMU_BIN} ${qemuArgs.join(' ')}`);
});

let shuttingDown = false;
async function shutdown() {
  if (shuttingDown) return;
  shuttingDown = true;
  for (const client of wss.clients) { try { client.close(); } catch {} }
  await stopQemu();
  server.close(() => process.exit(0));
  setTimeout(() => process.exit(0), 1500).unref();
}
process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);
