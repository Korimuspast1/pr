'use strict';

const fs = require('node:fs');
const path = require('node:path');
const { hashPassword, verifyPassword, parseCookies } = require('./auth');
const { followStarterBots } = require('./growth');

const SESSION_COOKIE = 'sid';
const SESSION_MAX_AGE = 30 * 24 * 60 * 60; // секунды
const PAGE_SIZE = 20;
const BODY_LIMIT = 64 * 1024;

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
};

class HttpError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

// ---------- валидация ----------

const USERNAME_RE = /^[a-zA-Z0-9_]{3,20}$/;
const COLOR_RE = /^#[0-9a-fA-F]{6}$/;

function cleanText(value, { min, max, field }) {
  if (typeof value !== 'string') throw new HttpError(400, `Поле «${field}» должно быть текстом`);
  const text = value.trim();
  if (text.length < min) throw new HttpError(400, `Поле «${field}» не может быть пустым`);
  if (text.length > max) throw new HttpError(400, `Поле «${field}»: максимум ${max} символов`);
  return text;
}

function validateUsername(value) {
  if (typeof value !== 'string' || !USERNAME_RE.test(value)) {
    throw new HttpError(400, 'Логин: 3–20 символов, только латиница, цифры и _');
  }
  return value;
}

function validatePassword(value) {
  if (typeof value !== 'string' || value.length < 6 || value.length > 100) {
    throw new HttpError(400, 'Пароль должен быть от 6 до 100 символов');
  }
  return value;
}

// ---------- сериализация ----------

function publicUser(store, user, viewerId) {
  return {
    id: user.id,
    username: user.username,
    displayName: user.displayName,
    bio: user.bio,
    avatarColor: user.avatarColor,
    isBot: user.isBot,
    createdAt: user.createdAt,
    followers: store.followersCount(user.id),
    following: store.followingCount(user.id),
    posts: store.postCount(user.id),
    isMe: viewerId === user.id,
    isFollowing: viewerId ? store.isFollowing(viewerId, user.id) : false,
  };
}

function authorSummary(user) {
  return {
    id: user.id,
    username: user.username,
    displayName: user.displayName,
    avatarColor: user.avatarColor,
    isBot: user.isBot,
  };
}

function publicPost(store, post, viewerId) {
  const author = store.getUser(post.authorId);
  return {
    id: post.id,
    text: post.text,
    createdAt: post.createdAt,
    author: authorSummary(author),
    likes: store.likeCount(post.id),
    likedByMe: viewerId ? store.hasLiked(post.id, viewerId) : false,
    comments: store.commentCount(post.id),
    mine: post.authorId === viewerId,
  };
}

function publicComment(store, comment) {
  return {
    id: comment.id,
    text: comment.text,
    createdAt: comment.createdAt,
    author: authorSummary(store.getUser(comment.authorId)),
  };
}

// ---------- HTTP-утилиты ----------

function sendJson(res, status, body, headers = {}) {
  const payload = JSON.stringify(body);
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(payload),
    'Cache-Control': 'no-store',
    ...headers,
  });
  res.end(payload);
}

function readJson(req) {
  return new Promise((resolve, reject) => {
    // Запросы без тела (лайк, подписка, выход) разрешены.
    const length = req.headers['content-length'];
    const hasBody = (length !== undefined && length !== '0') || req.headers['transfer-encoding'] !== undefined;
    if (!hasBody) return resolve({});
    const type = req.headers['content-type'] || '';
    if (!type.startsWith('application/json')) {
      reject(new HttpError(415, 'Ожидается JSON'));
      return;
    }
    let size = 0;
    const chunks = [];
    req.on('data', (chunk) => {
      size += chunk.length;
      if (size > BODY_LIMIT) {
        reject(new HttpError(413, 'Слишком большой запрос'));
        req.destroy();
        return;
      }
      chunks.push(chunk);
    });
    req.on('end', () => {
      if (chunks.length === 0) return resolve({});
      try {
        const parsed = JSON.parse(Buffer.concat(chunks).toString('utf8'));
        resolve(parsed && typeof parsed === 'object' ? parsed : {});
      } catch {
        reject(new HttpError(400, 'Некорректный JSON'));
      }
    });
    req.on('error', reject);
  });
}

// Сопоставляет путь вида /api/users/:username с реальным путём.
function matchPath(pattern, pathname) {
  const pParts = pattern.split('/').filter(Boolean);
  const aParts = pathname.split('/').filter(Boolean);
  if (pParts.length !== aParts.length) return null;
  const params = {};
  for (let i = 0; i < pParts.length; i++) {
    if (pParts[i].startsWith(':')) {
      params[pParts[i].slice(1)] = decodeURIComponent(aParts[i]);
    } else if (pParts[i] !== aParts[i]) {
      return null;
    }
  }
  return params;
}

function serveStatic(res, publicDir, pathname) {
  const relative = pathname === '/' ? 'index.html' : pathname.replace(/^\/+/, '');
  const filePath = path.normalize(path.join(publicDir, relative));
  if (!filePath.startsWith(publicDir + path.sep)) {
    sendJson(res, 403, { error: 'Запрещено' });
    return;
  }
  // Неизвестные пути (например, #/u/name в браузере) отдаём как index.html.
  const target = fs.existsSync(filePath) && fs.statSync(filePath).isFile()
    ? filePath
    : path.join(publicDir, 'index.html');
  const ext = path.extname(target);
  res.writeHead(200, {
    'Content-Type': MIME[ext] || 'application/octet-stream',
    'Cache-Control': 'no-cache',
  });
  fs.createReadStream(target).pipe(res);
}

// ---------- приложение ----------

function createApp({ store, publicDir, now = () => Date.now(), rng = Math.random }) {
  const routes = [];
  const route = (method, pattern, handler) => routes.push({ method, pattern, handler });

  // Токен берём из заголовка Authorization (его хранит клиент в localStorage)
  // или из cookie. Заголовок нужен, потому что интерфейс может открываться
  // внутри iframe на другом домене, и браузер тогда не отправляет cookie.
  function tokenFrom(req) {
    const auth = req.headers.authorization || '';
    const match = /^Bearer\s+([a-f0-9]{64})$/i.exec(auth);
    if (match) return match[1];
    return parseCookies(req.headers.cookie)[SESSION_COOKIE] || null;
  }

  function currentUser(req) {
    const token = tokenFrom(req);
    if (!token) return null;
    const session = store.getSession(token);
    if (!session) return null;
    return store.getUser(session.userId);
  }

  function requireUser(req) {
    const user = currentUser(req);
    if (!user) throw new HttpError(401, 'Войдите в аккаунт');
    return user;
  }

  function requireTarget(username) {
    const user = store.getUserByName(username);
    if (!user) throw new HttpError(404, 'Пользователь не найден');
    return user;
  }

  function requirePost(id) {
    const post = store.posts.get(id);
    if (!post) throw new HttpError(404, 'Пост не найден');
    return post;
  }

  function pageParams(url) {
    const limit = PAGE_SIZE;
    const beforeRaw = url.searchParams.get('before');
    const before = beforeRaw ? Number(beforeRaw) : Infinity;
    return { limit, before: Number.isFinite(before) ? before : Infinity };
  }

  // Курсорная пагинация: `before` — время создания последнего показанного поста.
  function paginate(posts, url, viewerId) {
    const { limit, before } = pageParams(url);
    const eligible = posts.filter((p) => p.createdAt < before);
    const items = eligible.slice(0, limit);
    return {
      posts: items.map((p) => publicPost(store, p, viewerId)),
      nextBefore: eligible.length > limit ? items[items.length - 1].createdAt : null,
    };
  }

  // Для HTTPS ставим SameSite=None; Secure, чтобы cookie работала и во встроенном (iframe) просмотре.
  function cookieAttrs(req, maxAge) {
    const secure = req.socket.encrypted || req.headers['x-forwarded-proto'] === 'https';
    return `Path=/; HttpOnly; ${secure ? 'SameSite=None; Secure' : 'SameSite=Lax'}; Max-Age=${maxAge}`;
  }

  function sessionHeaders(req, token) {
    return { 'Set-Cookie': `${SESSION_COOKIE}=${token}; ${cookieAttrs(req, SESSION_MAX_AGE)}` };
  }

  // ----- аутентификация -----

  route('POST', '/api/auth/register', async (req, res, body) => {
    const username = validateUsername(body.username);
    const password = validatePassword(body.password);
    const displayName = body.displayName === undefined || body.displayName === ''
      ? username
      : cleanText(body.displayName, { min: 1, max: 40, field: 'Имя' });
    if (store.getUserByName(username)) throw new HttpError(409, 'Такой логин уже занят');

    const { salt, passwordHash } = hashPassword(password);
    const user = store.createUser({ username, displayName, salt, passwordHash, createdAt: now() });
    followStarterBots(store, user.id, { rng });
    const token = store.createSession(user.id);
    sendJson(res, 201, { user: publicUser(store, user, user.id), token }, sessionHeaders(req, token));
  });

  route('POST', '/api/auth/login', async (req, res, body) => {
    const username = typeof body.username === 'string' ? body.username.trim() : '';
    const user = store.getUserByName(username);
    const ok = user && verifyPassword(String(body.password ?? ''), user.salt, user.passwordHash);
    if (!ok) throw new HttpError(401, 'Неверный логин или пароль');
    const token = store.createSession(user.id);
    sendJson(res, 200, { user: publicUser(store, user, user.id), token }, sessionHeaders(req, token));
  });

  route('POST', '/api/auth/logout', async (req, res) => {
    const token = tokenFrom(req);
    if (token) store.deleteSession(token);
    sendJson(res, 200, { ok: true }, {
      'Set-Cookie': `${SESSION_COOKIE}=; ${cookieAttrs(req, 0)}`,
    });
  });

  // ----- текущий пользователь -----

  route('GET', '/api/me', async (req, res) => {
    const me = requireUser(req);
    sendJson(res, 200, { user: publicUser(store, me, me.id) });
  });

  route('PATCH', '/api/me', async (req, res, body) => {
    const me = requireUser(req);
    const patch = {};
    if (body.displayName !== undefined) {
      patch.displayName = cleanText(body.displayName, { min: 1, max: 40, field: 'Имя' });
    }
    if (body.bio !== undefined) {
      patch.bio = cleanText(body.bio ?? '', { min: 0, max: 160, field: 'О себе' });
    }
    if (body.avatarColor !== undefined) {
      if (typeof body.avatarColor !== 'string' || !COLOR_RE.test(body.avatarColor)) {
        throw new HttpError(400, 'Некорректный цвет аватара');
      }
      patch.avatarColor = body.avatarColor;
    }
    store.updateUser(me.id, patch);
    sendJson(res, 200, { user: publicUser(store, me, me.id) });
  });

  // ----- посты и лента -----

  route('GET', '/api/feed', async (req, res, body, url) => {
    const me = requireUser(req);
    const mine = store.followingIds(me.id);
    const posts = store.sortedPosts((p) => p.authorId === me.id || mine.has(p.authorId));
    sendJson(res, 200, paginate(posts, url, me.id));
  });

  route('GET', '/api/explore', async (req, res, body, url) => {
    const me = requireUser(req);
    sendJson(res, 200, paginate(store.sortedPosts(), url, me.id));
  });

  route('POST', '/api/posts', async (req, res, body) => {
    const me = requireUser(req);
    const text = cleanText(body.text, { min: 1, max: 500, field: 'Текст поста' });
    const post = store.createPost({ authorId: me.id, text });
    sendJson(res, 201, { post: publicPost(store, post, me.id) });
  });

  route('DELETE', '/api/posts/:id', async (req, res, body, url, params) => {
    const me = requireUser(req);
    const post = requirePost(params.id);
    if (post.authorId !== me.id) throw new HttpError(403, 'Можно удалять только свои посты');
    store.deletePost(post.id);
    sendJson(res, 200, { ok: true });
  });

  route('POST', '/api/posts/:id/like', async (req, res, body, url, params) => {
    const me = requireUser(req);
    const post = requirePost(params.id);
    const on = !store.hasLiked(post.id, me.id);
    store.setLike(post.id, me.id, on);
    sendJson(res, 200, { likedByMe: on, likes: store.likeCount(post.id) });
  });

  route('GET', '/api/posts/:id/comments', async (req, res, body, url, params) => {
    requireUser(req);
    const post = requirePost(params.id);
    sendJson(res, 200, {
      comments: store.listComments(post.id).map((c) => publicComment(store, c)),
    });
  });

  route('POST', '/api/posts/:id/comments', async (req, res, body, url, params) => {
    const me = requireUser(req);
    const post = requirePost(params.id);
    const text = cleanText(body.text, { min: 1, max: 300, field: 'Комментарий' });
    const comment = store.createComment({ postId: post.id, authorId: me.id, text });
    sendJson(res, 201, {
      comment: publicComment(store, comment),
      comments: store.commentCount(post.id),
    });
  });

  // ----- пользователи -----

  route('GET', '/api/users', async (req, res, body, url) => {
    const me = requireUser(req);
    const q = (url.searchParams.get('q') || '').trim().toLowerCase();
    const users = store.listUsers()
      .filter((u) => !q || u.username.toLowerCase().includes(q) || u.displayName.toLowerCase().includes(q))
      .sort((a, b) => store.followersCount(b.id) - store.followersCount(a.id) || a.username.localeCompare(b.username))
      .slice(0, 50);
    sendJson(res, 200, { users: users.map((u) => publicUser(store, u, me.id)) });
  });

  route('GET', '/api/users/:username', async (req, res, body, url, params) => {
    const me = requireUser(req);
    const user = requireTarget(params.username);
    const posts = store.sortedPosts((p) => p.authorId === user.id).slice(0, PAGE_SIZE);
    sendJson(res, 200, {
      user: publicUser(store, user, me.id),
      posts: posts.map((p) => publicPost(store, p, me.id)),
    });
  });

  route('GET', '/api/users/:username/followers', async (req, res, body, url, params) => {
    const me = requireUser(req);
    const user = requireTarget(params.username);
    const list = [...store.followerIds(user.id)].map((id) => store.getUser(id));
    sendJson(res, 200, { users: list.map((u) => publicUser(store, u, me.id)) });
  });

  route('GET', '/api/users/:username/following', async (req, res, body, url, params) => {
    const me = requireUser(req);
    const user = requireTarget(params.username);
    const list = [...store.followingIds(user.id)].map((id) => store.getUser(id));
    sendJson(res, 200, { users: list.map((u) => publicUser(store, u, me.id)) });
  });

  route('POST', '/api/users/:username/follow', async (req, res, body, url, params) => {
    const me = requireUser(req);
    const target = requireTarget(params.username);
    if (target.id === me.id) throw new HttpError(400, 'Нельзя подписаться на себя');
    store.setFollow(me.id, target.id, true);
    sendJson(res, 200, { user: publicUser(store, target, me.id) });
  });

  route('DELETE', '/api/users/:username/follow', async (req, res, body, url, params) => {
    const me = requireUser(req);
    const target = requireTarget(params.username);
    store.setFollow(me.id, target.id, false);
    sendJson(res, 200, { user: publicUser(store, target, me.id) });
  });

  // ----- обработчик -----

  return async function handle(req, res) {
    const url = new URL(req.url, 'http://localhost');
    try {
      if (!url.pathname.startsWith('/api/')) {
        if (req.method !== 'GET' && req.method !== 'HEAD') throw new HttpError(405, 'Метод не разрешён');
        serveStatic(res, publicDir, url.pathname);
        return;
      }

      const matched = routes
        .map((r) => ({ r, params: r.method === req.method ? matchPath(r.pattern, url.pathname) : null }))
        .find((x) => x.params);

      if (!matched) {
        const pathExists = routes.some((r) => matchPath(r.pattern, url.pathname));
        throw new HttpError(pathExists ? 405 : 404, pathExists ? 'Метод не разрешён' : 'Не найдено');
      }

      const body = ['POST', 'PATCH', 'PUT'].includes(req.method) ? await readJson(req) : {};
      await matched.r.handler(req, res, body, url, matched.params);
    } catch (err) {
      if (err instanceof HttpError) {
        if (!res.headersSent) sendJson(res, err.status, { error: err.message });
      } else {
        console.error(err);
        if (!res.headersSent) sendJson(res, 500, { error: 'Внутренняя ошибка сервера' });
      }
    }
  };
}

module.exports = { createApp };
