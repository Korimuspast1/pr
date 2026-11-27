// Wave — музыкальный сервис: поиск по каталогам + законное воспроизведение.
//
// Запуск:  npm start   (по умолчанию http://0.0.0.0:3000)
// Env:     PORT, HOST, SOUNDCLOUD_CLIENT_ID, JAMENDO_CLIENT_ID,
//          ARCHIVE_COLLECTIONS, WAVE_USER_AGENT

import http from 'node:http';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { sendJson, clampInt } from './src/util.js';
import {
  searchSoundCloud,
  providerStatus as scStatus,
  oembed,
  embedFor,
  isValidSoundCloudUrl,
} from './src/soundcloud.js';
import { demoList, demoStatus, hasDemo, serveDemo } from './src/demo.js';
import {
  searchJamendo,
  searchArchive,
  streamAudio,
  jamendoStatus,
  archiveStatus,
} from './src/catalogs.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC_DIR = path.join(__dirname, 'public');
const PORT = Number(process.env.PORT || 3000);
const HOST = process.env.HOST || '0.0.0.0';

/* --------------------------- крошечный кэш --------------------------- */

class TtlCache {
  constructor(max = 300, ttlMs = 5 * 60 * 1000) {
    this.max = max;
    this.ttl = ttlMs;
    this.map = new Map();
  }
  get(key) {
    const hit = this.map.get(key);
    if (!hit) return undefined;
    if (Date.now() > hit.exp) {
      this.map.delete(key);
      return undefined;
    }
    return hit.value;
  }
  set(key, value) {
    if (this.map.size >= this.max) {
      const oldest = this.map.keys().next().value;
      this.map.delete(oldest);
    }
    this.map.set(key, { value, exp: Date.now() + this.ttl });
    return value;
  }
  async wrap(key, fn) {
    const hit = this.get(key);
    if (hit !== undefined) return hit;
    return this.set(key, await fn());
  }
}

const cache = new TtlCache(400, 5 * 60 * 1000);

/* --------------------------- rate limiting --------------------------- */

const buckets = new Map();
function rateLimited(ip, limit = 60, windowMs = 60_000) {
  const now = Date.now();
  const b = buckets.get(ip) || { count: 0, reset: now + windowMs };
  if (now > b.reset) {
    b.count = 0;
    b.reset = now + windowMs;
  }
  b.count += 1;
  buckets.set(ip, b);
  if (buckets.size > 5000) buckets.clear();
  return b.count > limit;
}

/* ---------------------------- статика ------------------------------- */

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.webp': 'image/webp',
  '.json': 'application/json; charset=utf-8',
  '.ico': 'image/x-icon',
  '.txt': 'text/plain; charset=utf-8',
};

async function serveStatic(req, res, relPath) {
  const safe = path.normalize(relPath).replace(/^(\.\.[/\\])+/, '');
  const file = path.join(PUBLIC_DIR, safe);
  // защита от выхода за пределы public/ (включая симлинки)
  let real = null;
  try {
    real = await fs.realpath(file);
  } catch {
    real = null;
  }
  const root = await fs.realpath(PUBLIC_DIR);
  if (!real || !(real === root || real.startsWith(root + path.sep))) {
    res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
    return res.end('404');
  }
  try {
    const data = await fs.readFile(file);
    const type = MIME[path.extname(file).toLowerCase()] || 'application/octet-stream';
    res.writeHead(200, {
      'Content-Type': type,
      'Content-Length': data.length,
      'Cache-Control': safe.endsWith('.html') ? 'no-cache' : 'public, max-age=600',
      'X-Content-Type-Options': 'nosniff',
    });
    res.end(data);
  } catch {
    res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
    res.end('404');
  }
}

/* ---------------------------- поиск --------------------------------- */

function normalize(track) {
  return {
    provider: track.provider,
    id: String(track.id),
    title: track.title,
    artist: track.artist,
    artwork: track.artwork || null,
    durationSec: track.durationSec || 0,
    url: track.url || null,
    genre: track.genre || null,
    playable: Boolean(track.playable),
    // audio → играем в <audio> через прокси; soundcloud → официальный iframe-плеер
    kind: track.streamUrl ? 'audio' : 'soundcloud',
    stream: track.streamUrl
      ? track.streamUrl.startsWith('/')
        ? track.streamUrl // локальное демо отдаём напрямую, без прокси
        : `/api/audio?u=${encodeURIComponent(track.streamUrl)}`
      : null,
    embed: track.provider === 'soundcloud' && track.url ? `/api/embed?url=${encodeURIComponent(track.url)}` : null,
    source: track.source || (track.provider === 'soundcloud' ? 'SoundCloud' : track.provider),
    license: track.license || null,
  };
}

function dedupe(tracks) {
  const seen = new Set();
  return tracks.filter((t) => {
    const key = `${t.provider}:${t.id}`.toLowerCase();
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

async function handleSearch(params) {
  const q = (params.get('q') || '').trim().slice(0, 120);
  const limit = clampInt(params.get('limit'), 20, 1, 40);
  const source = (params.get('source') || 'all').toLowerCase();

  if (!q) {
    const err = new Error('нужен параметр q — что искать');
    err.status = 400;
    throw err;
  }

  const wantSc = source === 'all' || source === 'soundcloud' || source === 'sc';
  const wantJam = source === 'all' || source === 'jamendo';
  const wantIa = source === 'all' || source === 'archive' || source === 'ia';
  const wantDemo = source === 'all' || source === 'demo';

  const tasks = [
    wantSc
      ? searchSoundCloud(q, { limit }).catch((e) => ({ provider: 'soundcloud', tracks: [], error: e.message }))
      : null,
    wantJam
      ? searchJamendo(q, { limit }).catch((e) => ({ provider: 'jamendo', tracks: [], error: e.message }))
      : null,
    wantIa
      ? searchArchive(q, { limit: Math.min(limit, 12) }).catch((e) => ({ provider: 'archive', tracks: [], error: e.message }))
      : null,
  ].filter(Boolean);

  const results = await Promise.all(tasks);
  const errors = results.filter((r) => r.error).map((r) => ({ provider: r.provider, error: r.error }));
  if (source !== 'all' && results.length === 1 && errors.length) {
    const err = new Error(errors[0].error);
    err.status = 502;
    throw err;
  }

  const tracks = dedupe(results.flatMap((r) => (r.tracks || []).map(normalize)));

  // Ничего не нашли или все источники лежат — отдаём демо, чтобы интерфейс
  // и плеер оставались живыми (работает без интернета).
  const demo = tracks.length || !wantDemo ? [] : demoList().map(normalize);

  return {
    query: q,
    count: tracks.length + demo.length,
    errors,
    demo: demo.length > 0,
    tracks: [...tracks, ...demo],
  };
}

/* --------------------------- роутер --------------------------------- */

const server = http.createServer(async (req, res) => {
  const started = Date.now();
  let url;
  try {
    url = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  } catch {
    return sendJson(res, 400, { error: 'плохой запрос' });
  }
  const { pathname, searchParams } = url;

  // CORS для мобильных/внешних клиентов
  if (req.method === 'OPTIONS') {
    res.writeHead(204, {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET,OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type',
      'Access-Control-Max-Age': '86400',
    });
    return res.end();
  }

  if (req.method !== 'GET' && req.method !== 'HEAD') {
    return sendJson(res, 405, { error: 'только GET' });
  }

  const ip = (req.headers['x-forwarded-for'] || '').split(',')[0].trim() || req.socket.remoteAddress || 'anon';
  if (pathname.startsWith('/api/') && rateLimited(ip)) {
    return sendJson(res, 429, { error: 'слишком много запросов, подождите минуту' });
  }

  try {
    if (pathname === '/api/health') {
      return sendJson(res, 200, {
        ok: true,
        service: 'wave',
        providers: [scStatus(), jamendoStatus(), archiveStatus(), demoStatus()],
        uptimeSec: Math.round(process.uptime()),
      });
    }

    if (pathname === '/api/providers') {
      return sendJson(res, 200, { providers: [scStatus(), jamendoStatus(), archiveStatus(), demoStatus()] });
    }

    if (pathname.startsWith('/api/demo/')) {
      const id = pathname.slice('/api/demo/'.length).replace(/\.wav$/i, '');
      if (!hasDemo(id)) return sendJson(res, 404, { error: 'нет такого демо-трека' });
      return serveDemo(id, req, res);
    }

    if (pathname === '/api/search') {
      const key = `search:${url.search}`;
      const hit = cache.get(key);
      if (hit) return sendJson(res, 200, { ...hit, cached: true });
      const data = cache.set(key, await handleSearch(searchParams));
      console.log(`[search] ${data.query} → ${data.count} треков (${Date.now() - started}ms)`);
      return sendJson(res, 200, data);
    }

    if (pathname === '/api/embed') {
      const trackUrl = searchParams.get('url') || '';
      if (!isValidSoundCloudUrl(trackUrl)) {
        return sendJson(res, 400, {
          error: 'нужна ссылка https://soundcloud.com/artist/track',
          embed: null,
        });
      }
      try {
        const data = await cache.wrap(`embed:${trackUrl}`, () => oembed(trackUrl));
        return sendJson(res, 200, data);
      } catch (err) {
        // даже если oEmbed не ответил — отдаём официальный виджет
        return sendJson(res, 200, { url: trackUrl, embed: embedFor(trackUrl), html: null, degraded: err.message });
      }
    }

    if (pathname === '/api/audio') {
      const u = searchParams.get('u') || '';
      if (!u) return sendJson(res, 400, { error: 'нужен параметр u' });
      return await streamAudio(u, req, res);
    }

    if (pathname === '/player') {
      // «страница трека»: официальный плеер + ссылка на артиста
      const trackUrl = searchParams.get('url') || '';
      const embed = isValidSoundCloudUrl(trackUrl) ? embedFor(trackUrl) : null;
      const html = playerPage({ trackUrl, embed });
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' });
      return res.end(html);
    }

    if (pathname === '/' || pathname === '/index.html') {
      return serveStatic(req, res, 'index.html');
    }

    return serveStatic(req, res, pathname.replace(/^\/+/, ''));
  } catch (err) {
    const status = err.status || 500;
    if (status >= 500) console.error('[error]', pathname, err);
    return sendJson(res, status, {
      error: err.message || 'внутренняя ошибка',
      ...(process.env.WAVE_DEBUG ? { stack: String(err.stack) } : {}),
    });
  }
});

function playerPage({ trackUrl, embed }) {
  return `<!doctype html>
<html lang="ru"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Wave — плеер</title>
<style>
  :root{color-scheme:dark}
  body{margin:0;min-height:100vh;display:grid;place-items:center;
    background:radial-gradient(1200px 600px at 20% 0%,#241a4d,#0b0b12 60%);
    font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;color:#f2f2f7;padding:24px}
  .card{width:min(720px,100%)}
  h1{font-size:18px;font-weight:600;opacity:.85;margin:0 0 16px}
  iframe{width:100%;border:0;border-radius:16px;background:#111}
  a{color:#a78bfa}
  .note{opacity:.6;font-size:13px;margin-top:14px;line-height:1.5}
</style></head>
<body><div class="card">
<h1>Официальный плеер SoundCloud</h1>
${embed ? `<iframe height="360" allow="autoplay" src="${embed}"></iframe>
<p class="note">Трек играет сам SoundCloud — это официальный виджет, артист получает статистику и выплаты.
<a href="${trackUrl}" target="_blank" rel="noopener noreferrer">Открыть на SoundCloud →</a></p>`
      : `<p>Некорректная ссылка. Формат: <code>https://soundcloud.com/artist/track</code></p>`}
</div></body></html>`;
}

server.listen(PORT, HOST, () => {
  const sc = scStatus();
  console.log(`\n  Wave — музыкальный сервис`);
  console.log(`  http://${HOST}:${PORT}`);
  console.log(`  SoundCloud: ${sc.mode} (${sc.configured ? 'client_id есть' : 'без ключа, публичный поиск'})`);
  console.log(`  Jamendo:    ${jamendoStatus().configured ? 'включён' : 'выключен (нет JAMENDO_CLIENT_ID)'}`);
  console.log(`  Archive:    включён`);
  console.log(`  Демо:       /api/demo/night-city.wav (без интернета)\n`);
});

for (const sig of ['SIGINT', 'SIGTERM']) {
  process.on(sig, () => {
    console.log(`\n[wave] ${sig} — выключаюсь`);
    server.close(() => process.exit(0));
    setTimeout(() => process.exit(0), 3000).unref();
  });
}
