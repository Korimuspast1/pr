'use strict';

const { test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const { Store } = require('../server/store');
const { createApp } = require('../server/app');
const { seedBots, growthTick, followerCap, followStarterBots } = require('../server/growth');

const PUBLIC_DIR = path.join(__dirname, '..', 'public');
const MIN = 60 * 1000;

let store;
let server;
let base;

before(async () => {
  store = new Store(null);
  seedBots(store, { rng: seededRng(42) });
  const app = createApp({ store, publicDir: PUBLIC_DIR });
  server = http.createServer(app);
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  base = `http://127.0.0.1:${server.address().port}`;
});

after(async () => {
  await new Promise((resolve) => server.close(resolve));
});

// Детерминированный генератор случайных чисел для тестов.
function seededRng(seed) {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 4294967296;
  };
}

async function api(method, pathname, { body, cookie } = {}) {
  const headers = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (cookie) headers.Cookie = cookie;
  const res = await fetch(base + pathname, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  const setCookie = res.headers.get('set-cookie');
  const text = await res.text();
  let data = null;
  try {
    data = JSON.parse(text);
  } catch {
    data = text;
  }
  const sid = setCookie && /sid=([^;]+)/.exec(setCookie);
  return { status: res.status, data, cookie: sid && sid[1] ? `sid=${sid[1]}` : null };
}

let counter = 0;
async function registerUser(prefix = 'user') {
  counter++;
  const username = `${prefix}_${counter}_${Date.now().toString(36)}`;
  const res = await api('POST', '/api/auth/register', {
    body: { username, password: 'secret123', displayName: `Имя ${counter}` },
  });
  assert.equal(res.status, 201, JSON.stringify(res.data));
  return { username, cookie: res.cookie, user: res.data.user };
}

// ---------- тесты ----------

test('боты создаются один раз и образуют сеть', () => {
  const bots = store.listBots();
  assert.ok(bots.length >= 20, 'ботов должно быть достаточно');
  assert.equal(seedBots(store), 0, 'повторный посев не создаёт ботов');
  const withFollowing = bots.filter((b) => store.followingCount(b.id) > 0);
  assert.equal(withFollowing.length, bots.length);
  assert.ok(bots.every((b) => b.passwordHash === null), 'у ботов нет пароля');
});

test('регистрация, валидация и сессия', async () => {
  const bad = await api('POST', '/api/auth/register', { body: { username: 'a', password: '123' } });
  assert.equal(bad.status, 400);

  const u = await registerUser('reg');
  assert.ok(u.cookie, 'сессия выставляется cookie');

  const dup = await api('POST', '/api/auth/register', { body: { username: u.username.toUpperCase(), password: 'secret123' } });
  assert.equal(dup.status, 409, 'логин не чувствителен к регистру');

  const me = await api('GET', '/api/me', { cookie: u.cookie });
  assert.equal(me.status, 200);
  assert.equal(me.data.user.username, u.username);

  const noAuth = await api('GET', '/api/feed');
  assert.equal(noAuth.status, 401);
});

test('новый пользователь сразу подписан на стартовых ботов, и лента не пустая', async () => {
  const u = await registerUser('feed');
  const feed = await api('GET', '/api/feed', { cookie: u.cookie });
  assert.equal(feed.status, 200);
  assert.ok(feed.data.posts.length > 0, 'в ленте есть посты ботов');
  assert.ok(store.followingCount(u.user.id) >= 3);
});

test('вход: неверный пароль отклоняется, верный работает', async () => {
  const u = await registerUser('login');
  const wrong = await api('POST', '/api/auth/login', { body: { username: u.username, password: 'nope-nope' } });
  assert.equal(wrong.status, 401);

  const ok = await api('POST', '/api/auth/login', { body: { username: u.username, password: 'secret123' } });
  assert.equal(ok.status, 200);
  assert.ok(ok.cookie);

  // Боты не могут войти.
  const bot = store.listBots()[0];
  const botLogin = await api('POST', '/api/auth/login', { body: { username: bot.username, password: 'anything' } });
  assert.equal(botLogin.status, 401);
});

test('посты, лайки и комментарии', async () => {
  const author = await registerUser('post');
  const viewer = await registerUser('view');

  const created = await api('POST', '/api/posts', { cookie: author.cookie, body: { text: '  Привет, мир!  ' } });
  assert.equal(created.status, 201);
  assert.equal(created.data.post.text, 'Привет, мир!', 'текст обрезается');
  assert.equal(created.data.post.mine, true);
  const postId = created.data.post.id;

  const empty = await api('POST', '/api/posts', { cookie: author.cookie, body: { text: '   ' } });
  assert.equal(empty.status, 400);

  const tooLong = await api('POST', '/api/posts', { cookie: author.cookie, body: { text: 'x'.repeat(501) } });
  assert.equal(tooLong.status, 400);

  // Пост виден в обзоре всем.
  const explore = await api('GET', '/api/explore', { cookie: viewer.cookie });
  assert.ok(explore.data.posts.some((p) => p.id === postId));

  // Лайк переключается.
  const like1 = await api('POST', `/api/posts/${postId}/like`, { cookie: viewer.cookie });
  assert.deepEqual([like1.data.likedByMe, like1.data.likes], [true, 1]);
  const like2 = await api('POST', `/api/posts/${postId}/like`, { cookie: viewer.cookie });
  assert.deepEqual([like2.data.likedByMe, like2.data.likes], [false, 0]);

  // Комментарии.
  const c = await api('POST', `/api/posts/${postId}/comments`, { cookie: viewer.cookie, body: { text: 'Круто!' } });
  assert.equal(c.status, 201);
  assert.equal(c.data.comments, 1);
  const list = await api('GET', `/api/posts/${postId}/comments`, { cookie: viewer.cookie });
  assert.equal(list.data.comments.length, 1);
  assert.equal(list.data.comments[0].author.username, viewer.username);

  // Чужой пост удалить нельзя, свой — можно.
  const forbidden = await api('DELETE', `/api/posts/${postId}`, { cookie: viewer.cookie });
  assert.equal(forbidden.status, 403);
  const removed = await api('DELETE', `/api/posts/${postId}`, { cookie: author.cookie });
  assert.equal(removed.status, 200);
  const gone = await api('GET', `/api/posts/${postId}/comments`, { cookie: viewer.cookie });
  assert.equal(gone.status, 404);
});

test('подписки: лента показывает посты тех, на кого подписаны', async () => {
  const me = await registerUser('follow');
  const bot = store.listBots().find((b) => !store.isFollowing(me.user.id, b.id));

  const before = await api('GET', `/api/users/${bot.username}`, { cookie: me.cookie });
  const wasFollowing = before.data.user.isFollowing;

  if (wasFollowing) {
    await api('DELETE', `/api/users/${bot.username}/follow`, { cookie: me.cookie });
  }
  const notInFeed = await api('GET', '/api/feed', { cookie: me.cookie });
  assert.ok(!notInFeed.data.posts.some((p) => p.author.username === bot.username));

  const followed = await api('POST', `/api/users/${bot.username}/follow`, { cookie: me.cookie });
  assert.equal(followed.status, 200);
  assert.equal(followed.data.user.isFollowing, true);
  const inFeed = await api('GET', '/api/feed', { cookie: me.cookie });
  assert.ok(inFeed.data.posts.some((p) => p.author.username === bot.username));

  const self = await api('POST', `/api/users/${me.username}/follow`, { cookie: me.cookie });
  assert.equal(self.status, 400);

  const missing = await api('GET', '/api/users/no_such_user_zzz', { cookie: me.cookie });
  assert.equal(missing.status, 404);
});

test('профиль и поиск показывают счётчики', async () => {
  const u = await registerUser('prof');
  const v = await registerUser('pview');
  await api('POST', '/api/posts', { cookie: u.cookie, body: { text: 'Мой первый пост' } });
  await api('POST', `/api/users/${u.username}/follow`, { cookie: v.cookie });

  const profile = await api('GET', `/api/users/${u.username}`, { cookie: v.cookie });
  assert.equal(profile.data.user.posts, 1);
  assert.equal(profile.data.user.followers, 1);
  assert.equal(profile.data.user.isFollowing, true);
  assert.equal(profile.data.posts.length, 1);

  const search = await api('GET', `/api/users?q=${u.username}`, { cookie: v.cookie });
  assert.ok(search.data.users.some((x) => x.username === u.username));
});

test('редактирование профиля', async () => {
  const u = await registerUser('edit');
  const ok = await api('PATCH', '/api/me', {
    cookie: u.cookie,
    body: { displayName: 'Новое имя', bio: 'О себе', avatarColor: '#ff0000' },
  });
  assert.equal(ok.status, 200);
  assert.equal(ok.data.user.displayName, 'Новое имя');
  assert.equal(ok.data.user.avatarColor, '#ff0000');

  const badColor = await api('PATCH', '/api/me', { cookie: u.cookie, body: { avatarColor: 'red' } });
  assert.equal(badColor.status, 400);
  const longBio = await api('PATCH', '/api/me', { cookie: u.cookie, body: { bio: 'x'.repeat(161) } });
  assert.equal(longBio.status, 400);
});

test('пагинация ленты курсором before', async () => {
  const u = await registerUser('page');
  for (let i = 0; i < 25; i++) {
    store.createPost({ authorId: u.user.id, text: `пост ${i}`, createdAt: Date.now() - (30 - i) * MIN });
  }
  const first = await api('GET', '/api/explore', { cookie: u.cookie });
  assert.equal(first.data.posts.length, 20);
  assert.ok(first.data.nextBefore);
  const second = await api('GET', `/api/explore?before=${first.data.nextBefore}`, { cookie: u.cookie });
  assert.ok(second.data.posts.length > 0);
  const ids1 = new Set(first.data.posts.map((p) => p.id));
  assert.ok(second.data.posts.every((p) => !ids1.has(p.id)), 'страницы не пересекаются');
});

test('лимит подписчиков растёт со временем и активностью', () => {
  const now = Date.now();
  const fresh = { createdAt: now };
  const old = { createdAt: now - 60 * MIN };
  assert.ok(followerCap(old, { now, postCount: 0, likesReceived: 0 }) > followerCap(fresh, { now, postCount: 0, likesReceived: 0 }));
  assert.ok(followerCap(fresh, { now, postCount: 2, likesReceived: 0 }) > followerCap(fresh, { now, postCount: 0, likesReceived: 0 }));
  assert.ok(followerCap(fresh, { now, postCount: 0, likesReceived: 10 }) > followerCap(fresh, { now, postCount: 0, likesReceived: 0 }));
  assert.ok(followerCap(old, { now, postCount: 1000, likesReceived: 1000 }) <= 500, 'есть верхняя граница');
});

test('рост: подписчики приходят по одному за тик и не превышают лимит', () => {
  const s = new Store(null);
  seedBots(s, { rng: seededRng(7) });
  const now = Date.now();
  const user = s.createUser({ username: 'grower', displayName: 'Растущий', createdAt: now - 10 * MIN });

  // Всегда «везёт» — максимальный темп роста.
  const alwaysYes = () => 0;
  let previous = s.followersCount(user.id);
  for (let tick = 0; tick < 40; tick++) {
    growthTick(s, { now, rng: alwaysYes });
    const current = s.followersCount(user.id);
    assert.ok(current - previous <= 1, `за тик не больше одного подписчика (тик ${tick})`);
    previous = current;
  }
  // Возраст 10 минут, нет постов и лайков -> лимит 2 + 10 = 12.
  assert.equal(s.followersCount(user.id), 12);

  // Пост поднимает лимит, и рост продолжается.
  s.createPost({ authorId: user.id, text: 'Первый пост', createdAt: now });
  for (let tick = 0; tick < 10; tick++) growthTick(s, { now, rng: alwaysYes });
  assert.ok(s.followersCount(user.id) > 12, 'после поста подписчики продолжают прибывать');
});

test('рост: без везения подписчиков почти нет', () => {
  const s = new Store(null);
  seedBots(s, { rng: seededRng(9) });
  const now = Date.now();
  const user = s.createUser({ username: 'unlucky', displayName: 'Тест', createdAt: now - 60 * MIN });
  const neverYes = () => 0.999;
  for (let tick = 0; tick < 20; tick++) growthTick(s, { now, rng: neverYes });
  assert.equal(s.followersCount(user.id), 0);
});

test('рост: боты ставят лайки и публикуют посты', () => {
  const s = new Store(null);
  seedBots(s, { rng: seededRng(11) });
  const now = Date.now();
  const stats = growthTick(s, { now, rng: () => 0 });
  assert.ok(stats.botPosts > 0);
  assert.ok(stats.botLikes > 0);
});

test('стартовые боты для нового пользователя выбираются случайно', () => {
  const s = new Store(null);
  seedBots(s, { rng: seededRng(3) });
  const user = s.createUser({ username: 'newbie', displayName: 'Новичок' });
  followStarterBots(s, user.id, { count: 3, rng: seededRng(5) });
  assert.equal(s.followingCount(user.id), 3);
  assert.ok([...s.followingIds(user.id)].every((id) => s.getUser(id).isBot));
});

test('сохранение и загрузка из файла', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'social-'));
  const file = path.join(dir, 'db.json');
  const s1 = new Store(file);
  const u = s1.createUser({ username: 'persist', displayName: 'Сохранённый', passwordHash: 'x', salt: 'y' });
  const p = s1.createPost({ authorId: u.id, text: 'Не потеряется' });
  s1.setLike(p.id, u.id, true);
  s1.createComment({ postId: p.id, authorId: u.id, text: 'коммент' });
  s1.flush();

  const s2 = new Store(file);
  assert.equal(s2.getUserByName('PERSIST').displayName, 'Сохранённый');
  assert.equal(s2.likeCount(p.id), 1);
  assert.equal(s2.commentCount(p.id), 1);
  assert.equal(s2.postCount(u.id), 1);
  fs.rmSync(dir, { recursive: true, force: true });
});

test('статика: главная страница и SPA-маршруты', async () => {
  const index = await fetch(`${base}/`);
  assert.equal(index.status, 200);
  assert.match(await index.text(), /<div id="app"/);

  const spa = await fetch(`${base}/some/route`);
  assert.equal(spa.status, 200);
  assert.match(await spa.text(), /<div id="app"/);

  const js = await fetch(`${base}/app.js`);
  assert.match(js.headers.get('content-type'), /javascript/);

  // Попытка выйти за пределы public/ не должна отдать файлы проекта.
  const traversal = await fetch(`${base}/..%2F..%2Fpackage.json`);
  assert.doesNotMatch(await traversal.text(), /"virtual-social-network"/);
});

test('некорректный JSON и неверный content-type', async () => {
  const badJson = await fetch(`${base}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: '{not json',
  });
  assert.equal(badJson.status, 400);

  const wrongType = await fetch(`${base}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'text/plain' },
    body: 'hello',
  });
  assert.equal(wrongType.status, 415);
});

test('вход по токену в заголовке Authorization (работает без cookie)', async () => {
  const u = await registerUser('bearer');
  const token = (await api('POST', '/api/auth/login', { body: { username: u.username, password: 'secret123' } })).data.token;
  assert.match(token, /^[a-f0-9]{64}$/);

  const res = await fetch(`${base}/api/feed`, { headers: { Authorization: `Bearer ${token}` } });
  assert.equal(res.status, 200);

  const bad = await fetch(`${base}/api/feed`, { headers: { Authorization: 'Bearer 0000' } });
  assert.equal(bad.status, 401);

  const out = await fetch(`${base}/api/auth/logout`, { method: 'POST', headers: { Authorization: `Bearer ${token}` } });
  assert.equal(out.status, 200);
  const after = await fetch(`${base}/api/feed`, { headers: { Authorization: `Bearer ${token}` } });
  assert.equal(after.status, 401, 'после выхода токен не работает');
});

test('атрибуты cookie: Lax по HTTP, None+Secure за HTTPS-прокси', async () => {
  const name = `cookie_${Date.now().toString(36)}`;
  const plain = await fetch(`${base}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: name, password: 'secret123' }),
  });
  assert.match(plain.headers.get('set-cookie'), /SameSite=Lax/);

  const proxied = await fetch(`${base}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Forwarded-Proto': 'https' },
    body: JSON.stringify({ username: name, password: 'secret123' }),
  });
  const cookie = proxied.headers.get('set-cookie');
  assert.match(cookie, /SameSite=None; Secure/);
  assert.match(cookie, /HttpOnly/);
});

test('эмодзи убираются у ботов, но не у реальных пользователей', () => {
  const { stripBotEmoji } = require('../server/growth');
  const s = new Store(null);
  const bot = s.createUser({ username: 'emo_bot', displayName: 'Бот', isBot: true, bio: 'Кофе ☕ и книги 📚' });
  const botPost = s.createPost({ authorId: bot.id, text: 'Привет 🌅 мир' });
  const human = s.createUser({ username: 'emo_human', displayName: 'Человек' });
  const humanPost = s.createPost({ authorId: human.id, text: 'мой пост 😀' });
  assert.ok(stripBotEmoji(s) > 0);
  assert.equal(bot.bio, 'Кофе и книги');
  assert.equal(botPost.text, 'Привет мир');
  assert.equal(humanPost.text, 'мой пост 😀', 'пост пользователя не меняется');
});

test('CORS: preflight-запрос к API отвечает без cookie', async () => {
  const res = await fetch(`${base}/api/feed`, {
    method: 'OPTIONS',
    headers: { Origin: 'null', 'Access-Control-Request-Method': 'GET', 'Access-Control-Request-Headers': 'authorization' },
  });
  assert.equal(res.status, 204);
  assert.equal(res.headers.get('access-control-allow-origin'), '*');
  assert.match(res.headers.get('access-control-allow-headers'), /Authorization/);
});
