import http from 'node:http';
import { readFile } from 'node:fs/promises';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

function loadDotEnv() {
  try {
    const content = readFileSync(new URL('.env', import.meta.url), 'utf8');
    for (const line of content.split(/\\r?\\n/)) {
      const match = line.match(/^\\s*([A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*(.*?)\\s*$/);
      if (!match || match[1] in process.env) continue;
      process.env[match[1]] = match[2].replace(/^['\"]|['\"]$/g, '');
    }
  } catch {
    // .env is optional; production environments can inject variables directly.
  }
}

loadDotEnv();

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC_DIR = path.join(ROOT, 'public');
const PORT = Number(process.env.PORT || 4173);
const HOST = process.env.HOST || '0.0.0.0';
const API_URL = process.env.DEEPSEEK_API_URL || 'https://api.deepseek.com/chat/completions';
const MODEL = process.env.DEEPSEEK_MODEL || 'deepseek-chat';
const API_KEY = process.env.DEEPSEEK_API_KEY || '';

const mimeTypes = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.ico': 'image/x-icon',
};

function sendJson(res, status, payload) {
  const body = JSON.stringify(payload);
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Cache-Control': 'no-store',
    'Access-Control-Allow-Origin': '*',
  });
  res.end(body);
}

function demoAnswer(messages) {
  const latest = [...messages].reverse().find((message) => message.role === 'user')?.content || '';
  const excerpt = latest.length > 180 ? `${latest.slice(0, 180)}…` : latest;
  return `Это демонстрационный ответ интерфейса DeepSeek V3.\n\nЯ получил ваш запрос:\n«${excerpt}»\n\nЧтобы подключить реальную модель, добавьте DEEPSEEK_API_KEY в файл .env и перезапустите сервер. Ключ хранится только на сервере и не раскрывается браузеру.`;
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let raw = '';
    let size = 0;
    req.setEncoding('utf8');
    req.on('data', (chunk) => {
      size += Buffer.byteLength(chunk);
      if (size > 1_000_000) {
        reject(new Error('Request body is too large'));
        req.destroy();
        return;
      }
      raw += chunk;
    });
    req.on('end', () => {
      try {
        resolve(raw ? JSON.parse(raw) : {});
      } catch {
        reject(new Error('Invalid JSON'));
      }
    });
    req.on('error', reject);
  });
}

async function chat(req, res) {
  let payload;
  try {
    payload = await readBody(req);
  } catch (error) {
    return sendJson(res, 400, { error: error.message });
  }

  const incoming = Array.isArray(payload.messages) ? payload.messages : [];
  const messages = incoming
    .filter((message) => ['system', 'user', 'assistant'].includes(message?.role))
    .map((message) => ({
      role: message.role,
      content: typeof message.content === 'string' ? message.content.slice(0, 40_000) : '',
    }))
    .filter((message) => message.content)
    .slice(-40);

  if (!messages.some((message) => message.role === 'user')) {
    return sendJson(res, 400, { error: 'Добавьте сообщение пользователя.' });
  }

  if (!API_KEY) {
    return sendJson(res, 200, { answer: demoAnswer(messages), demo: true, model: MODEL });
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 120_000);
  try {
    const upstream = await fetch(API_URL, {
      method: 'POST',
      signal: controller.signal,
      headers: {
        Authorization: `Bearer ${API_KEY}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        model: typeof payload.model === 'string' && payload.model.length < 100 ? payload.model : MODEL,
        messages,
        temperature: Number.isFinite(Number(payload.temperature)) ? Math.min(2, Math.max(0, Number(payload.temperature))) : 0.7,
        max_tokens: Number.isFinite(Number(payload.max_tokens)) ? Math.min(8192, Math.max(64, Number(payload.max_tokens))) : 2048,
        stream: false,
      }),
    });

    const data = await upstream.json().catch(() => ({}));
    if (!upstream.ok) {
      const message = data?.error?.message || `DeepSeek API вернул HTTP ${upstream.status}`;
      return sendJson(res, upstream.status >= 500 ? 502 : upstream.status, { error: message });
    }

    const answer = data?.choices?.[0]?.message?.content;
    if (typeof answer !== 'string' || !answer.trim()) {
      return sendJson(res, 502, { error: 'API вернул пустой ответ.' });
    }
    return sendJson(res, 200, { answer, demo: false, model: data.model || MODEL, usage: data.usage || null });
  } catch (error) {
    const message = error.name === 'AbortError' ? 'Запрос превысил лимит ожидания.' : 'Не удалось связаться с DeepSeek API.';
    return sendJson(res, 502, { error: message });
  } finally {
    clearTimeout(timeout);
  }
}

async function serveStatic(req, res, pathname) {
  const requested = pathname === '/' ? 'index.html' : pathname.replace(/^\/+/, '');
  const filePath = path.resolve(PUBLIC_DIR, requested);
  if (!filePath.startsWith(`${PUBLIC_DIR}${path.sep}`)) {
    return sendJson(res, 403, { error: 'Forbidden' });
  }
  try {
    const content = await readFile(filePath);
    const type = mimeTypes[path.extname(filePath).toLowerCase()] || 'application/octet-stream';
    res.writeHead(200, { 'Content-Type': type, 'Cache-Control': 'no-cache' });
    res.end(content);
  } catch (error) {
    if (error.code === 'ENOENT') {
      const fallback = await readFile(path.join(PUBLIC_DIR, 'index.html'));
      res.writeHead(200, { 'Content-Type': mimeTypes['.html'], 'Cache-Control': 'no-cache' });
      return res.end(fallback);
    }
    sendJson(res, 500, { error: 'Не удалось прочитать файл.' });
  }
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  if (req.method === 'OPTIONS') {
    res.writeHead(204, {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type',
    });
    return res.end();
  }
  if (url.pathname === '/api/config' && req.method === 'GET') {
    return sendJson(res, 200, { configured: Boolean(API_KEY), model: MODEL, mode: API_KEY ? 'api' : 'demo' });
  }
  if (url.pathname === '/api/health' && req.method === 'GET') {
    return sendJson(res, 200, { ok: true, mode: API_KEY ? 'api' : 'demo' });
  }
  if (url.pathname === '/api/chat' && req.method === 'POST') {
    return chat(req, res);
  }
  if (req.method === 'GET') {
    return serveStatic(req, res, url.pathname);
  }
  sendJson(res, 404, { error: 'Not found' });
});

server.listen(PORT, HOST, () => {
  console.log(`DeepSeek V3 Studio listening on http://${HOST}:${PORT}`);
  console.log(API_KEY ? `API mode: ${MODEL}` : 'Demo mode: set DEEPSEEK_API_KEY to connect the model');
});
