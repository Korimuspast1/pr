import WebSocket from 'ws';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const BASE = process.env.PREVIEW_URL || 'http://127.0.0.1:8080';
const SERIAL = path.resolve(__dirname, '../run/serial.log');
const wsUrl = BASE.replace(/^http/, 'ws') + '/websockify';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function status() {
  const r = await fetch(BASE + '/api/status');
  if (!r.ok) throw new Error('status HTTP ' + r.status);
  return r.json();
}

function connectRfb() {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(wsUrl, ['binary']);
    let stage = 0, buf = Buffer.alloc(0);
    const timer = setTimeout(() => reject(new Error('RFB timeout')), 8000);
    ws.on('error', reject);
    ws.on('message', (d) => {
      buf = Buffer.concat([buf, Buffer.from(d)]);
      if (stage === 0 && buf.length >= 12) { ws.send(Buffer.from('RFB 003.008\n')); buf = buf.subarray(12); stage = 1; }
      if (stage === 1 && buf.length >= 1) { const n = buf[0]; if (buf.length >= 1 + n) { ws.send(Buffer.from([1])); buf = buf.subarray(1 + n); stage = 2; } }
      if (stage === 2 && buf.length >= 4) { const l = buf.readUInt32BE(0); if (buf.length >= 4 + l) { ws.send(Buffer.from([1])); buf = buf.subarray(4 + l); stage = 3; } }
      if (stage === 3 && buf.length >= 24) { const n = buf.readUInt32BE(20); if (buf.length >= 24 + n) { clearTimeout(timer); buf = buf.subarray(24 + n); resolve({ ws, buf }); } }
    });
  });
}
function key(ws, keysym, down) { const b = Buffer.alloc(8); b[0] = 4; b[1] = down ? 1 : 0; b.writeUInt32BE(keysym, 4); ws.send(b); }
function type(ws, s) { for (const ch of s) { const k = ch === '\n' ? 0xff0d : ch.charCodeAt(0); key(ws, k, true); key(ws, k, false); } }
function ptr(ws, x, y, mask) { const b = Buffer.alloc(6); b[0] = 5; b[1] = mask; b.writeUInt16BE(x, 2); b.writeUInt16BE(y, 4); ws.send(b); }

const s = await status();
if (!s.ready) throw new Error('OS not ready: ' + JSON.stringify(s));
console.log('HTTP status PASS', s.firmware, s.resolution);

// Reboot to a known window layout before input tests.
await fetch(BASE + '/api/reboot', { method: 'POST', headers: { 'X-Aurora-Control': '1' } });
await sleep(3000);

const { ws } = await connectRfb();
console.log('RFB handshake PASS');
const enc = Buffer.alloc(8); enc[0] = 2; enc.writeUInt16BE(1, 2); enc.writeInt32BE(0, 4); ws.send(enc);
await sleep(200);
type(ws, 'help\n');
await sleep(1000);
let log = fs.readFileSync(SERIAL, 'utf8');
if (!log.includes('[shell:1] help')) throw new Error('VNC keyboard FAIL');
console.log('VNC keyboard PASS');

ptr(ws, 300, 90, 0); await sleep(150);
ptr(ws, 300, 90, 1); await sleep(100);
for (let i = 1; i <= 8; i++) { ptr(ws, 300 + i * 12, 90 + i * 8, 1); await sleep(50); }
ptr(ws, 396, 154, 0); await sleep(400);
log = fs.readFileSync(SERIAL, 'utf8');
if (!log.includes('[wm] drag id=1')) throw new Error('VNC mouse drag FAIL');
console.log('VNC mouse drag PASS');
ws.close();
console.log('ALL PASS');
