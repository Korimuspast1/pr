'use strict';

// Клиент соцсеть: маршрутизация по хешу (#/feed, #/u/имя и т.д.) и рендер без фреймворков.
// Весь пользовательский текст вставляется через textContent, поэтому HTML не исполняется.

const TOKEN_KEY = 'social.token';
const THEME_KEY = 'social.theme';

const root = document.getElementById('app');
let me = null;
let renderId = 0;
// Токен всегда хранится в памяти страницы. localStorage — лишь дополнительное хранилище:
// в iframe с sandbox без allow-same-origin доступ к нему запрещён и бросает исключение.
let memoryToken = null;

// Безопасное чтение и запись хранилища: при ошибке просто работаем без него.
const storage = {
  get(key) {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  },
  set(key, value) {
    try {
      if (value == null) localStorage.removeItem(key);
      else localStorage.setItem(key, value);
    } catch {
      // Хранилище недоступно — ничего страшного, токен живёт в памяти.
    }
  },
};

// ---------- тема ----------

function currentTheme() {
  return storage.get(THEME_KEY) === 'light' ? 'light' : 'dark';
}

function applyTheme(theme) {
  document.documentElement.dataset.theme = theme;
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', theme === 'light' ? '#f8f6f6' : '#161313');
}

function setTheme(theme) {
  storage.set(THEME_KEY, theme);
  applyTheme(theme);
}

applyTheme(currentTheme());

// ---------- сеть ----------

function getToken() {
  return memoryToken || storage.get(TOKEN_KEY);
}

function saveToken(token) {
  memoryToken = token || null;
  storage.set(TOKEN_KEY, token || null);
}

async function api(path, { method = 'GET', body } = {}) {
  const options = { method, headers: {}, credentials: 'same-origin' };
  const token = getToken();
  if (token) options.headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) {
    options.headers['Content-Type'] = 'application/json';
    options.body = JSON.stringify(body);
  }
  const res = await fetch(`/api${path}`, options);
  let data = null;
  try {
    data = await res.json();
  } catch {
    data = null;
  }
  // Сессия истекла: возвращаем на экран входа.
  if (res.status === 401 && !path.startsWith('/auth/') && me) {
    me = null;
    saveToken(null);
    location.hash = '#/auth';
  }
  if (!res.ok) {
    const err = new Error(data?.error || `Ошибка ${res.status}`);
    err.status = res.status;
    throw err;
  }
  return data;
}

// ---------- DOM-утилиты ----------

function h(tag, props, ...children) {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(props || {})) {
    if (value === false || value == null) continue;
    if (key === 'class') el.className = value;
    else if (key === 'style' && typeof value === 'object') Object.assign(el.style, value);
    else if (key.startsWith('on')) el.addEventListener(key.slice(2), value);
    else if (key === 'html') throw new Error('html-вставка запрещена');
    else if (value === true) el.setAttribute(key, '');
    else el.setAttribute(key, String(value));
  }
  for (const child of children.flat()) {
    if (child == null || child === false) continue;
    el.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return el;
}

function toast(message, { icon: iconName = 'circle-alert' } = {}) {
  const el = h('div', { class: 'toast', role: 'status' }, icon(iconName, { size: 16 }), message);
  document.body.append(el);
  setTimeout(() => el.remove(), 2600);
}

function plural(n, forms) {
  const abs = Math.abs(n) % 100;
  const last = abs % 10;
  if (abs > 10 && abs < 20) return forms[2];
  if (last > 1 && last < 5) return forms[1];
  if (last === 1) return forms[0];
  return forms[2];
}

function relativeTime(ts) {
  const diff = Math.max(0, Date.now() - ts) / 1000;
  if (diff < 60) return 'только что';
  if (diff < 3600) {
    const m = Math.floor(diff / 60);
    return `${m} ${plural(m, ['мин', 'мин', 'мин'])} назад`;
  }
  if (diff < 86400) {
    const hr = Math.floor(diff / 3600);
    return `${hr} ${plural(hr, ['час', 'часа', 'часов'])} назад`;
  }
  const days = Math.floor(diff / 86400);
  if (days < 7) return `${days} ${plural(days, ['день', 'дня', 'дней'])} назад`;
  return new Date(ts).toLocaleDateString('ru-RU', { day: 'numeric', month: 'short' });
}

function avatar(user, size = '') {
  const letter = (user.displayName || user.username || '?').trim().charAt(0).toUpperCase();
  return h('span', {
    class: `avatar ${size}`.trim(),
    style: { background: user.avatarColor || '#5b8def' },
    'aria-hidden': 'true',
  }, letter);
}

function profileHref(username) {
  return `#/u/${encodeURIComponent(username)}`;
}

function userLink(user, className = 'name') {
  return h('a', { href: profileHref(user.username), class: className }, user.displayName);
}

function botBadge(user) {
  return user.isBot ? h('span', { class: 'badge', title: 'Виртуальный аккаунт' }, 'виртуальный') : null;
}

// ---------- каркас ----------

const NAV = [
  { href: '#/feed', label: 'Лента', icon: 'house', path: '/feed' },
  { href: '#/explore', label: 'Обзор', icon: 'flame', path: '/explore' },
  { href: '#/search', label: 'Люди', icon: 'users', path: '/search' },
  { href: () => profileHref(me.username), label: 'Профиль', icon: 'user', path: 'me' },
  { href: '#/settings', label: 'Настройки', icon: 'settings', path: '/settings' },
];

function isActive(item, path) {
  if (item.path === 'me') return me && path === `/u/${me.username}`;
  return path === item.path;
}

function brand() {
  return h('a', { href: '#/feed', class: 'brand', 'aria-label': 'Соцсеть, на главную' },
    h('span', { class: 'logo-mark' }),
    h('span', null, 'соцсеть'),
    h('span', { class: 'cursor' }),
  );
}

function sidebar(path) {
  const links = NAV.map((item) => {
    const href = typeof item.href === 'function' ? item.href() : item.href;
    return h('a', { href, class: `nav-link${isActive(item, path) ? ' active' : ''}` },
      icon(item.icon, { size: 17 }), item.label);
  });
  return h('aside', { class: 'sidebar' },
    brand(),
    ...links,
    h('div', { class: 'sidebar-spacer' }),
    h('div', { class: 'row', style: { gap: '6px' } },
      me ? h('a', { href: profileHref(me.username), class: 'me-card grow' },
        avatar(me, 'sm'),
        h('div', { class: 'meta grow' },
          h('div', { class: 'name truncate' }, me.displayName),
          h('div', { class: 'handle truncate' }, `@${me.username}`)),
      ) : null,
      themeToggle(),
    ),
  );
}

function themeToggle() {
  return h('button', {
    class: 'btn btn-ghost btn-icon',
    title: 'Сменить тему',
    'aria-label': 'Сменить тему',
    onclick: () => {
      setTheme(currentTheme() === 'dark' ? 'light' : 'dark');
      render();
    },
  }, icon(currentTheme() === 'dark' ? 'sun' : 'moon', { size: 17 }));
}

function tabbar(path) {
  return h('nav', { class: 'tabbar', 'aria-label': 'Навигация' },
    ...NAV.map((item) => {
      const href = typeof item.href === 'function' ? item.href() : item.href;
      return h('a', { href, class: `tab${isActive(item, path) ? ' active' : ''}`, 'aria-label': item.label },
        icon(item.icon, { size: 20 }), h('span', null, item.label));
    }),
  );
}

function shell(path, ...children) {
  const page = h('main', { class: 'page' }, ...children);
  root.replaceChildren(
    h('div', { class: 'app' },
      sidebar(path),
      h('div', { class: 'main' },
        h('header', { class: 'mobile-top' }, brand(), themeToggle()),
        page,
      ),
      tabbar(path),
    ),
  );
  return page;
}

function pageHead(title, subtitle) {
  return h('header', { class: 'page-head' },
    h('h1', null, title),
    subtitle ? h('p', null, subtitle) : null,
  );
}

function emptyState(iconName, text, extra) {
  return h('div', { class: 'empty' }, icon(iconName, { size: 28 }), h('p', null, text), extra || null);
}

// ---------- маршрутизация ----------

function currentRoute() {
  const raw = location.hash.replace(/^#/, '') || '/feed';
  const [path, query = ''] = raw.split('?');
  return { path: path.replace(/\/+$/, '') || '/', params: new URLSearchParams(query) };
}

async function render() {
  const id = ++renderId;
  const { path } = currentRoute();

  if (!me && getToken()) {
    try {
      me = (await api('/me')).user;
    } catch {
      me = null;
    }
  }
  if (id !== renderId) return;

  if (!me) {
    if (path !== '/auth') {
      location.hash = '#/auth';
      return;
    }
    renderAuth();
    return;
  }
  if (path === '/auth') {
    location.hash = '#/feed';
    return;
  }

  try {
    const userMatch = path.match(/^\/u\/([^/]+)(?:\/(followers|following))?$/);
    if (path === '/feed') await renderFeed(id, path);
    else if (path === '/explore') await renderExplore(id, path);
    else if (path === '/search') await renderSearch(id, path);
    else if (path === '/settings') renderSettings(path);
    else if (userMatch) {
      const username = decodeURIComponent(userMatch[1]);
      if (userMatch[2]) await renderUserList(id, username, userMatch[2], path);
      else await renderProfile(id, username, path);
    } else {
      shell(path, emptyState('circle-alert', 'Такой страницы нет.', h('a', { href: '#/feed', class: 'btn btn-ghost', style: { marginTop: '12px' } }, 'На главную')));
    }
  } catch (err) {
    if (id !== renderId) return;
    shell(path, emptyState('circle-alert', `Ошибка: ${err.message}`));
  }
}

window.addEventListener('hashchange', render);

// ---------- вход и регистрация ----------

function renderAuth(mode = 'login') {
  const error = h('div', { class: 'error', role: 'alert' });
  const submit = h('button', { type: 'submit', class: 'btn btn-primary btn-block', style: { height: '40px', marginTop: '6px' } },
    mode === 'login' ? 'Войти' : 'Создать аккаунт');
  const username = h('input', { type: 'text', name: 'username', autocomplete: 'username', required: true, placeholder: 'alex_01', maxlength: 20 });
  const password = h('input', { type: 'password', name: 'password', required: true, minlength: 6, maxlength: 100, autocomplete: mode === 'login' ? 'current-password' : 'new-password', placeholder: 'минимум 6 символов' });
  const displayName = h('input', { type: 'text', name: 'displayName', maxlength: 40, placeholder: 'Как вас показывать' });

  const form = h('form', {
    onsubmit: async (e) => {
      e.preventDefault();
      error.textContent = '';
      submit.disabled = true;
      try {
        const body = { username: username.value.trim(), password: password.value };
        if (mode === 'register') body.displayName = displayName.value.trim();
        const data = await api(mode === 'login' ? '/auth/login' : '/auth/register', { method: 'POST', body });
        saveToken(data.token);
        me = data.user;
        location.hash = '#/feed';
        render();
      } catch (err) {
        error.textContent = err.message;
      } finally {
        submit.disabled = false;
      }
    },
  },
    h('label', { class: 'field' }, h('span', null, 'Логин'), username),
    mode === 'register' ? h('label', { class: 'field' }, h('span', null, 'Имя'), displayName) : null,
    h('label', { class: 'field' }, h('span', null, 'Пароль'), password),
    error,
    submit,
  );

  const tabs = h('div', { class: 'tabs', role: 'tablist' },
    h('button', { type: 'button', class: mode === 'login' ? 'active' : '', role: 'tab', onclick: () => renderAuth('login') }, 'Вход'),
    h('button', { type: 'button', class: mode === 'register' ? 'active' : '', role: 'tab', onclick: () => renderAuth('register') }, 'Регистрация'),
  );

  root.replaceChildren(
    h('div', { class: 'auth-wrap' },
      h('div', { class: 'auth-card' },
        brand(),
        h('p', { class: 'lead' }, mode === 'login'
          ? 'Лента без шума: только те, кто вам интересен. Войдите, чтобы продолжить.'
          : 'Создайте аккаунт за минуту. Подписчики появятся постепенно, по мере того как вы публикуете.'),
        tabs,
        form,
        h('div', { class: 'faint small', style: { marginTop: '20px', textAlign: 'center' } },
          'Аккаунты виртуальные. Все данные хранятся на этом сервере.'),
      ),
    ),
  );
  if (mode === 'login') username.focus();
}

// ---------- посты ----------

function likeButton(post) {
  const iconEl = icon('heart', { size: 15 });
  const label = h('span', null, String(post.likes));
  const btn = h('button', { class: `act${post.likedByMe ? ' liked' : ''}`, title: 'Нравится', 'aria-label': 'Нравится' }, iconEl, label);
  const paint = () => {
    btn.className = `act${post.likedByMe ? ' liked' : ''}`;
    iconEl.setAttribute('fill', post.likedByMe ? 'currentColor' : 'none');
    label.textContent = String(post.likes);
  };
  btn.addEventListener('click', async () => {
    btn.disabled = true;
    try {
      const data = await api(`/posts/${post.id}/like`, { method: 'POST' });
      post.likedByMe = data.likedByMe;
      post.likes = data.likes;
      paint();
    } catch (err) {
      toast(err.message);
    } finally {
      btn.disabled = false;
    }
  });
  paint();
  return btn;
}

function postCard(post, { onDelete } = {}) {
  const commentsBox = h('div', { class: 'comments', hidden: true });
  let commentsLoaded = false;
  const countLabel = h('span', null, String(post.comments));
  const commentBtn = h('button', { class: 'act', title: 'Комментарии', 'aria-label': 'Комментарии' },
    icon('message-circle', { size: 15 }), countLabel);
  commentBtn.addEventListener('click', async () => {
    commentsBox.hidden = !commentsBox.hidden;
    if (!commentsBox.hidden && !commentsLoaded) {
      commentsLoaded = true;
      await loadComments(post, commentsBox, countLabel);
    }
  });

  const deleteBtn = post.mine && onDelete
    ? h('button', {
      class: 'act danger',
      title: 'Удалить пост',
      'aria-label': 'Удалить пост',
      onclick: async () => {
        if (!confirm('Удалить пост?')) return;
        try {
          await api(`/posts/${post.id}`, { method: 'DELETE' });
          onDelete();
        } catch (err) {
          toast(err.message);
        }
      },
    }, icon('trash-2', { size: 15 }))
    : null;

  return h('article', { class: 'post' },
    avatar(post.author),
    h('div', { class: 'post-body' },
      h('div', { class: 'post-head' },
        userLink(post.author),
        botBadge(post.author),
        h('a', { href: profileHref(post.author.username), class: 'handle' }, `@${post.author.username}`),
        h('span', { class: 'time' }, `· ${relativeTime(post.createdAt)}`),
      ),
      h('p', { class: 'post-text' }, post.text),
      h('div', { class: 'post-actions' }, likeButton(post), commentBtn, h('span', { class: 'act-spacer' }), deleteBtn),
      commentsBox,
    ),
  );
}

async function loadComments(post, box, countLabel) {
  box.replaceChildren(h('div', { class: 'faint small' }, 'Загрузка…'));
  try {
    const { comments } = await api(`/posts/${post.id}/comments`);
    const input = h('input', { type: 'text', placeholder: 'Ответить…', maxlength: 300, 'aria-label': 'Текст комментария' });
    const send = h('button', { type: 'submit', class: 'btn btn-icon', title: 'Отправить', 'aria-label': 'Отправить' }, icon('send', { size: 16 }));
    const form = h('form', {
      class: 'comment-form',
      onsubmit: async (e) => {
        e.preventDefault();
        const text = input.value.trim();
        if (!text) return;
        send.disabled = true;
        try {
          const data = await api(`/posts/${post.id}/comments`, { method: 'POST', body: { text } });
          input.value = '';
          post.comments = data.comments;
          countLabel.textContent = String(post.comments);
          box.insertBefore(commentRow(data.comment), form);
          box.querySelector('.comments-empty')?.remove();
        } catch (err) {
          toast(err.message);
        } finally {
          send.disabled = false;
        }
      },
    }, input, send);

    const list = comments.length
      ? comments.map(commentRow)
      : [h('div', { class: 'faint small comments-empty' }, 'Комментариев пока нет')];
    box.replaceChildren(...list, form);
  } catch (err) {
    box.replaceChildren(h('div', { class: 'error' }, err.message));
  }
}

function commentRow(comment) {
  return h('div', { class: 'comment' },
    avatar(comment.author, 'sm'),
    h('div', { class: 'comment-bubble' },
      h('div', { class: 'who' },
        h('b', null, comment.author.displayName), ' ',
        h('span', { class: 'faint mono' }, `@${comment.author.username} · ${relativeTime(comment.createdAt)}`)),
      h('p', null, comment.text),
    ),
  );
}

function composer(onPosted) {
  const MAX = 500;
  const textarea = h('textarea', { placeholder: 'Что нового?', maxlength: MAX, rows: 2, 'aria-label': 'Новый пост' });
  const counter = h('span', { class: 'counter' }, `0/${MAX}`);
  const error = h('div', { class: 'error' });
  const button = h('button', { type: 'submit', class: 'btn btn-primary' }, icon('send', { size: 15 }), 'Опубликовать');
  button.disabled = true;

  textarea.addEventListener('input', () => {
    counter.textContent = `${textarea.value.length}/${MAX}`;
    counter.classList.toggle('warn', textarea.value.length > MAX - 30);
    button.disabled = textarea.value.trim().length === 0;
  });

  return h('form', {
    class: 'composer',
    onsubmit: async (e) => {
      e.preventDefault();
      const text = textarea.value.trim();
      if (!text) return;
      button.disabled = true;
      error.textContent = '';
      try {
        await api('/posts', { method: 'POST', body: { text } });
        textarea.value = '';
        counter.textContent = `0/${MAX}`;
        onPosted();
      } catch (err) {
        error.textContent = err.message;
        button.disabled = false;
      }
    },
  }, textarea, error, h('div', { class: 'composer-foot' }, counter, button));
}

// Список постов с пагинацией. endpoint — путь API без параметров.
function postFeed(endpoint, emptyIcon, emptyText, { composerNode = null } = {}) {
  const list = h('div', { class: 'feed' });
  const more = h('button', { class: 'btn btn-block', hidden: true, style: { marginTop: '16px' } }, 'Показать ещё');
  let nextBefore = null;

  const appendPosts = (posts, { reset = false } = {}) => {
    if (reset) list.replaceChildren();
    if (reset && posts.length === 0) {
      list.append(emptyState(emptyIcon, emptyText));
    }
    for (const post of posts) {
      list.append(postCard(post, { onDelete: () => render() }));
    }
  };

  const reloadAll = async () => {
    const id = renderId;
    try {
      const data = await api(endpoint);
      if (id !== renderId) return;
      appendPosts(data.posts, { reset: true });
      nextBefore = data.nextBefore;
      more.hidden = nextBefore == null;
    } catch (err) {
      list.replaceChildren(h('div', { class: 'error' }, err.message));
    }
  };

  more.addEventListener('click', async () => {
    if (nextBefore == null) return;
    more.disabled = true;
    try {
      const sep = endpoint.includes('?') ? '&' : '?';
      const data = await api(`${endpoint}${sep}before=${nextBefore}`);
      appendPosts(data.posts);
      nextBefore = data.nextBefore;
      more.hidden = nextBefore == null;
    } catch (err) {
      toast(err.message);
    } finally {
      more.disabled = false;
    }
  });

  const node = h('div', { class: 'stack' }, composerNode, list, more);
  reloadAll();
  return node;
}

async function renderFeed(id, path) {
  const composerNode = composer(() => render());
  const feedNode = postFeed('/feed', 'house', 'Пока пусто. Подпишитесь на кого-нибудь или напишите первый пост.', { composerNode });
  if (id !== renderId) return;
  shell(path, pageHead('Лента', 'Посты тех, на кого вы подписаны'), feedNode);
}

async function renderExplore(id, path) {
  const feedNode = postFeed('/explore', 'flame', 'Постов пока нет.');
  if (id !== renderId) return;
  shell(path, pageHead('Обзор', 'Все посты соцсети, от новых к старым'), feedNode);
}

// ---------- поиск людей ----------

async function renderSearch(id, path) {
  const input = h('input', { type: 'search', placeholder: 'Поиск по имени или логину', 'aria-label': 'Поиск людей', autocomplete: 'off' });
  const results = h('div', { class: 'user-list' });

  const load = async () => {
    const q = input.value.trim();
    try {
      const { users } = await api(`/users?q=${encodeURIComponent(q)}`);
      if (id !== renderId) return;
      results.replaceChildren(...(users.length
        ? users.map((u) => userRow(u, { onChange: load }))
        : [h('div', { class: 'empty', style: { border: 'none' } }, icon('search', { size: 26 }), h('p', null, 'Никого не нашли'))]));
    } catch (err) {
      results.replaceChildren(h('div', { class: 'error', style: { padding: '16px' } }, err.message));
    }
  };

  let timer = null;
  input.addEventListener('input', () => {
    clearTimeout(timer);
    timer = setTimeout(load, 250);
  });

  shell(path,
    pageHead('Люди', 'Подписывайтесь на тех, кто пишет интересное'),
    h('div', { class: 'search-box' }, icon('search', { size: 16 }), input),
    results,
  );
  load();
}

function userRow(user, { onChange } = {}) {
  const btn = me && user.id !== me.id ? followButton(user, onChange) : null;
  return h('div', { class: 'user-row' },
    avatar(user),
    h('div', { class: 'who grow' },
      h('div', { class: 'row', style: { gap: '6px' } }, userLink(user), botBadge(user)),
      h('div', { class: 'sub' }, `@${user.username} · подписчиков ${user.followers}`),
    ),
    btn,
  );
}

function followButton(user, onChange) {
  const btn = h('button', { class: 'btn btn-follow' });
  const paint = (following) => {
    btn.className = `btn btn-follow${following ? ' on' : ' btn-primary'}`;
    btn.replaceChildren(icon(following ? 'user-check' : 'user-plus', { size: 15 }), following ? 'Подписаны' : 'Подписаться');
    btn.dataset.following = following ? '1' : '0';
  };
  paint(user.isFollowing);
  btn.addEventListener('click', async () => {
    const following = btn.dataset.following === '1';
    btn.disabled = true;
    try {
      const data = await api(`/users/${encodeURIComponent(user.username)}/follow`, { method: following ? 'DELETE' : 'POST' });
      user.isFollowing = data.user.isFollowing;
      user.followers = data.user.followers;
      paint(user.isFollowing);
      if (onChange) onChange();
    } catch (err) {
      toast(err.message);
    } finally {
      btn.disabled = false;
    }
  });
  return btn;
}

// ---------- профиль ----------

async function renderProfile(id, username, path) {
  const { user, posts } = await api(`/users/${encodeURIComponent(username)}`);
  if (id !== renderId) return;

  const action = user.isMe
    ? h('a', { href: '#/settings', class: 'btn' }, icon('pencil', { size: 15 }), 'Редактировать')
    : followButton(user, () => render());

  const postsBox = h('div', { class: 'feed' });
  if (posts.length === 0) {
    postsBox.append(emptyState('message-circle', user.isMe ? 'Вы ещё ничего не опубликовали.' : 'Постов пока нет.'));
  }
  for (const post of posts) postsBox.append(postCard(post, { onDelete: () => render() }));

  const profile = h('section', { class: 'panel' },
    h('div', { class: 'profile-top' },
      avatar(user, 'lg'),
      h('div', { class: 'info' },
        h('h2', null, user.displayName, botBadge(user)),
        h('div', { class: 'handle' }, `@${user.username}`),
        user.bio ? h('p', { class: 'bio' }, user.bio) : h('p', { class: 'bio faint' }, 'Пока без описания'),
        h('div', { class: 'stats' },
          h('span', null, h('b', null, String(user.posts)), plural(user.posts, ['пост', 'поста', 'постов'])),
          h('a', { href: `${profileHref(user.username)}/followers` }, h('b', null, String(user.followers)), plural(user.followers, ['подписчик', 'подписчика', 'подписчиков'])),
          h('a', { href: `${profileHref(user.username)}/following` }, h('b', null, String(user.following)), 'подписок'),
        ),
      ),
      h('div', { class: 'actions' }, action),
    ),
  );

  shell(path, profile, postsBox);
}

async function renderUserList(id, username, kind, path) {
  const { users } = await api(`/users/${encodeURIComponent(username)}/${kind}`);
  if (id !== renderId) return;
  const title = kind === 'followers' ? 'Подписчики' : 'Подписки';
  const list = users.length
    ? h('div', { class: 'user-list' }, users.map((u) => userRow(u, { onChange: () => render() })))
    : emptyState('users', kind === 'followers' ? 'Подписчиков пока нет' : 'Подписок пока нет');
  shell(path,
    h('div', { class: 'page-head-row' },
      h('a', { href: profileHref(username), class: 'btn btn-ghost btn-icon', 'aria-label': 'Назад' }, icon('arrow-left', { size: 18 })),
      h('h1', { style: { margin: 0, fontSize: '24px', fontWeight: 600, letterSpacing: '-.02em' } }, title),
    ),
    list,
  );
}

// ---------- настройки ----------

function renderSettings(path) {
  const error = h('div', { class: 'error' });
  const ok = h('div', { class: 'small', style: { color: 'var(--text-2)', minHeight: '18px' } });

  const displayName = h('input', { type: 'text', value: me.displayName, maxlength: 40, required: true });
  const bio = h('textarea', { maxlength: 160, rows: 3 }, me.bio || '');
  const color = h('input', { type: 'color', value: me.avatarColor, 'aria-label': 'Цвет аватара' });
  const preview = avatar(me, 'lg');
  const updatePreview = () => {
    preview.style.background = color.value;
    preview.textContent = (displayName.value.trim() || me.username).charAt(0).toUpperCase();
  };
  displayName.addEventListener('input', updatePreview);
  color.addEventListener('input', updatePreview);

  const saveBtn = h('button', { type: 'submit', class: 'btn btn-primary' }, 'Сохранить');

  const profileForm = h('form', {
    class: 'panel stack',
    onsubmit: async (e) => {
      e.preventDefault();
      error.textContent = '';
      ok.textContent = '';
      saveBtn.disabled = true;
      try {
        const data = await api('/me', {
          method: 'PATCH',
          body: { displayName: displayName.value, bio: bio.value, avatarColor: color.value },
        });
        me = data.user;
        ok.textContent = 'Изменения сохранены';
      } catch (err) {
        error.textContent = err.message;
      } finally {
        saveBtn.disabled = false;
      }
    },
  },
    h('div', { class: 'row' }, preview,
      h('div', { class: 'grow' },
        h('div', { style: { fontWeight: 600 } }, me.displayName),
        h('div', { class: 'faint mono small' }, `@${me.username}`))),
    h('label', { class: 'field' }, h('span', null, 'Имя'), displayName),
    h('label', { class: 'field' }, h('span', null, 'О себе'), bio),
    h('div', { class: 'row', style: { alignItems: 'flex-end', justifyContent: 'space-between' } },
      h('label', { class: 'field', style: { marginBottom: 0 } }, h('span', null, 'Цвет аватара'), color),
      saveBtn),
    error,
    ok,
  );

  const themeButtons = h('div', { class: 'segmented', role: 'group', 'aria-label': 'Тема' });
  const paintTheme = () => {
    themeButtons.replaceChildren(
      ...[['dark', 'moon', 'Тёмная'], ['light', 'sun', 'Светлая']].map(([value, iconName, label]) =>
        h('button', {
          type: 'button',
          class: currentTheme() === value ? 'active' : '',
          onclick: () => {
            setTheme(value);
            paintTheme();
            // Перерисовываем, чтобы обновить иконку темы в шапке.
            render();
          },
        }, icon(iconName, { size: 14 }), label)));
  };
  paintTheme();

  const logout = h('button', {
    class: 'btn btn-danger',
    onclick: async () => {
      try {
        await api('/auth/logout', { method: 'POST' });
      } catch {
        // Даже при ошибке сбрасываем локальную сессию.
      }
      saveToken(null);
      me = null;
      location.hash = '#/auth';
      render();
    },
  }, icon('log-out', { size: 15 }), 'Выйти из аккаунта');

  shell(path,
    pageHead('Настройки', 'Профиль, оформление и доступ к аккаунту'),
    profileForm,
    h('section', { class: 'panel' },
      h('div', { class: 'section-title' }, 'Оформление'),
      h('div', { class: 'setting-row' },
        h('div', null,
          h('div', { class: 'label' }, 'Тема'),
          h('div', { class: 'hint' }, 'Тёмная тема по умолчанию, как в opencode.ai')),
        themeButtons),
    ),
    h('section', { class: 'panel' },
      h('div', { class: 'section-title' }, 'Аккаунт'),
      h('div', { class: 'setting-row' },
        h('div', null,
          h('div', { class: 'label' }, 'Выход'),
          h('div', { class: 'hint' }, 'Сессия закроется на этом устройстве')),
        logout),
    ),
  );
}

// ---------- старт ----------

render();
