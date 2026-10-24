'use strict';

// Клиент соцсети: маршрутизация по хешу (#/feed, #/u/имя и т.д.) и рендер на чистом JS.
// Весь пользовательский текст вставляется через textContent, поэтому HTML не исполняется.

const root = document.getElementById('app');
let me = null;
let renderId = 0;

// ---------- утилиты ----------

async function api(path, { method = 'GET', body } = {}) {
  const options = { method, headers: {}, credentials: 'same-origin' };
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
  // Сессия истекла на сервере: возвращаем на экран входа.
  if (res.status === 401 && !path.startsWith('/auth/') && me) {
    me = null;
    location.hash = '#/auth';
  }
  if (!res.ok) {
    const err = new Error(data?.error || `Ошибка ${res.status}`);
    err.status = res.status;
    throw err;
  }
  return data;
}

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

function toast(message) {
  const el = h('div', { class: 'toast' }, message);
  document.body.append(el);
  setTimeout(() => el.remove(), 2400);
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
    return `${m} ${plural(m, ['минуту', 'минуты', 'минут'])} назад`;
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

function userLink(user, className = 'name') {
  return h('a', { href: `#/u/${encodeURIComponent(user.username)}`, class: className }, user.displayName);
}

function botBadge(user) {
  return user.isBot ? h('span', { class: 'badge', title: 'Виртуальный аккаунт' }, 'виртуальный') : null;
}

function setPage(...children) {
  const page = h('main', { class: 'page' }, ...children);
  root.replaceChildren(topbar(), page);
  return page;
}

function navLink(href, label, icon, active) {
  return h('a', { href, class: active ? 'active' : '' }, `${icon} `, h('span', { class: 'nav-label' }, label));
}

function topbar() {
  const route = currentRoute().path;
  const profileHref = me ? `#/u/${encodeURIComponent(me.username)}` : '#/auth';
  return h('header', { class: 'topbar' },
    h('div', { class: 'topbar-inner' },
      h('a', { href: '#/feed', class: 'brand' }, '🌐', h('span', { class: 'text' }, 'Соцсеть')),
      h('nav', { class: 'nav' },
        navLink('#/feed', 'Лента', '🏠', route === '/feed'),
        navLink('#/explore', 'Обзор', '🔥', route === '/explore'),
        navLink('#/search', 'Люди', '🔍', route === '/search'),
        navLink(profileHref, 'Профиль', '👤', route.startsWith('/u/') && me && route === `/u/${me.username}`),
        navLink('#/settings', 'Настройки', '⚙️', route === '/settings'),
      ),
    ),
  );
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

  if (!me) {
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
    if (path === '/feed') await renderFeed(id);
    else if (path === '/explore') await renderExplore(id);
    else if (path === '/search') await renderSearch(id);
    else if (path === '/settings') renderSettings();
    else if (userMatch) {
      const username = decodeURIComponent(userMatch[1]);
      if (userMatch[2]) await renderUserList(id, username, userMatch[2]);
      else await renderProfile(id, username);
    } else {
      setPage(h('div', { class: 'card empty' }, 'Такой страницы нет. ', h('a', { href: '#/feed' }, 'На главную')));
    }
  } catch (err) {
    if (id !== renderId) return;
    setPage(h('div', { class: 'card empty' }, `Ошибка: ${err.message}`));
  }
}

window.addEventListener('hashchange', render);

// ---------- авторизация ----------

function renderAuth(mode = 'login') {
  const error = h('div', { class: 'error', role: 'alert' });
  const submit = h('button', { type: 'submit', class: 'block' }, mode === 'login' ? 'Войти' : 'Создать аккаунт');
  const username = h('input', { name: 'username', autocomplete: 'username', required: true, placeholder: 'например, alex_01' });
  const password = h('input', { name: 'password', type: 'password', required: true, minlength: 6, autocomplete: mode === 'login' ? 'current-password' : 'new-password' });
  const displayName = h('input', { name: 'displayName', maxlength: 40, placeholder: 'Как вас показывать' });

  const form = h('form', {
    class: 'card',
    onsubmit: async (e) => {
      e.preventDefault();
      error.textContent = '';
      submit.disabled = true;
      try {
        const body = { username: username.value.trim(), password: password.value };
        if (mode === 'register') body.displayName = displayName.value.trim();
        const data = await api(mode === 'login' ? '/auth/login' : '/auth/register', { method: 'POST', body });
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
    h('div', { class: 'tabs' },
      h('button', { type: 'button', class: mode === 'login' ? 'active' : '', onclick: () => renderAuth('login') }, 'Вход'),
      h('button', { type: 'button', class: mode === 'register' ? 'active' : '', onclick: () => renderAuth('register') }, 'Регистрация'),
    ),
    h('label', null, 'Логин', username),
    mode === 'register' ? h('label', null, 'Имя', displayName) : null,
    h('label', null, 'Пароль', password),
    error,
    submit,
  );

  root.replaceChildren(
    h('div', { class: 'auth-wrap' },
      h('div', { class: 'auth-card' },
        h('div', { class: 'brand', style: { marginBottom: '8px', fontSize: '28px' } }, '🌐 Соцсеть'),
        h('p', { class: 'muted', style: { margin: 0 } }, mode === 'login'
          ? 'Войдите, чтобы читать ленту и публиковать посты.'
          : 'Создайте аккаунт за минуту. Подписчики появятся постепенно.'),
        form,
      ),
    ),
  );
  if (mode === 'login') username.focus();
}

// ---------- посты ----------

function postCard(post, { onDelete } = {}) {
  const likeBtn = h('button', {
    class: `act${post.likedByMe ? ' liked' : ''}`,
    title: 'Нравится',
    onclick: async () => {
      try {
        const data = await api(`/posts/${post.id}/like`, { method: 'POST' });
        post.likedByMe = data.likedByMe;
        post.likes = data.likes;
        likeBtn.className = `act${post.likedByMe ? ' liked' : ''}`;
        likeBtn.textContent = `${post.likedByMe ? '♥' : '♡'} ${post.likes}`;
      } catch (err) {
        toast(err.message);
      }
    },
  }, `${post.likedByMe ? '♥' : '♡'} ${post.likes}`);

  const commentsBox = h('div', { class: 'comments', hidden: true });
  let commentsLoaded = false;
  const commentBtn = h('button', { class: 'act', title: 'Комментарии' }, `💬 ${post.comments}`);
  commentBtn.addEventListener('click', async () => {
    commentsBox.hidden = !commentsBox.hidden;
    if (!commentsBox.hidden && !commentsLoaded) {
      commentsLoaded = true;
      await loadComments(post, commentsBox, commentBtn);
    }
  });

  const deleteBtn = post.mine && onDelete
    ? h('button', {
      class: 'act danger',
      onclick: async () => {
        if (!confirm('Удалить пост?')) return;
        try {
          await api(`/posts/${post.id}`, { method: 'DELETE' });
          onDelete();
        } catch (err) {
          toast(err.message);
        }
      },
    }, 'Удалить')
    : null;

  return h('article', { class: 'card post' },
    h('div', { class: 'post-head' },
      avatar(post.author),
      h('div', { class: 'grow' },
        h('div', null, userLink(post.author), ' ', botBadge(post.author)),
        h('div', { class: 'meta' },
          h('a', { href: `#/u/${encodeURIComponent(post.author.username)}`, class: 'meta' }, `@${post.author.username}`),
          ' · ', relativeTime(post.createdAt)),
      ),
    ),
    h('p', { class: 'post-text' }, post.text),
    h('div', { class: 'post-actions' }, likeBtn, commentBtn, deleteBtn),
    commentsBox,
  );
}

async function loadComments(post, box, countBtn) {
  box.replaceChildren(h('div', { class: 'muted small' }, 'Загрузка…'));
  try {
    const { comments } = await api(`/posts/${post.id}/comments`);
    const input = h('input', { placeholder: 'Написать комментарий…', maxlength: 300 });
    const send = h('button', { type: 'submit' }, 'Отправить');
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
          countBtn.textContent = `💬 ${post.comments}`;
          box.insertBefore(commentRow(data.comment), form);
          const empty = box.querySelector('.empty-comments');
          if (empty) empty.remove();
        } catch (err) {
          toast(err.message);
        } finally {
          send.disabled = false;
        }
      },
    }, input, send);

    const list = comments.length
      ? comments.map(commentRow)
      : [h('div', { class: 'muted small empty-comments' }, 'Комментариев пока нет')];
    box.replaceChildren(...list, form);
  } catch (err) {
    box.replaceChildren(h('div', { class: 'error' }, err.message));
  }
}

function commentRow(comment) {
  return h('div', { class: 'comment' },
    avatar(comment.author, 'sm'),
    h('div', { class: 'bubble' },
      h('div', null, userLink(comment.author), ' ', h('span', { class: 'muted small' }, relativeTime(comment.createdAt))),
      h('p', null, comment.text),
    ),
  );
}

function composer(onPosted) {
  const MAX = 500;
  const textarea = h('textarea', { placeholder: 'Что нового?', maxlength: MAX, rows: 2 });
  const counter = h('span', { class: 'counter' }, `0/${MAX}`);
  const error = h('div', { class: 'error' });
  const button = h('button', { type: 'submit', disabled: true }, 'Опубликовать');

  textarea.addEventListener('input', () => {
    counter.textContent = `${textarea.value.length}/${MAX}`;
    button.disabled = textarea.value.trim().length === 0;
  });

  return h('form', {
    class: 'card composer',
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
  }, textarea, error, h('div', { class: 'composer-footer' }, counter, button));
}

// Список постов с кнопкой «Показать ещё». endpoint — путь API без параметров.
function postFeed(endpoint, emptyText, { composerNode = null } = {}) {
  const list = h('div', { class: 'feed-list' });
  const more = h('button', { class: 'outline block', hidden: true }, 'Показать ещё');
  let nextBefore = null;

  const appendPosts = (posts, { reset = false } = {}) => {
    if (reset) list.replaceChildren();
    if (reset && posts.length === 0) {
      list.append(h('div', { class: 'card empty' }, emptyText));
    }
    for (const post of posts) {
      list.append(postCard(post, { onDelete: () => reloadAll() }));
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
      list.replaceChildren(h('div', { class: 'card error' }, err.message));
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

  const node = h('div', { class: 'page-list' }, composerNode, list, more);
  reloadAll();
  return node;
}

async function renderFeed(id) {
  const composerNode = composer(() => {
    // Перезагружаем ленту после публикации.
    render();
  });
  const feedNode = postFeed('/feed', 'Пока пусто. Подпишитесь на кого-нибудь или напишите первый пост!', { composerNode });
  if (id !== renderId) return;
  setPage(h('h1', { class: 'page-title' }, 'Лента'), feedNode);
}

async function renderExplore(id) {
  const feedNode = postFeed('/explore', 'Постов пока нет.');
  if (id !== renderId) return;
  setPage(h('h1', { class: 'page-title' }, 'Обзор'), h('p', { class: 'muted', style: { margin: 0 } }, 'Все посты соцсети, от новых к старым.'), feedNode);
}

// ---------- поиск людей ----------

async function renderSearch(id) {
  const input = h('input', { type: 'search', placeholder: 'Поиск по имени или логину', autofocus: true });
  const results = h('div', { class: 'card user-list' });

  const load = async () => {
    const q = input.value.trim();
    try {
      const { users } = await api(`/users?q=${encodeURIComponent(q)}`);
      if (id !== renderId) return;
      results.replaceChildren(...(users.length
        ? users.map((u) => userRow(u, { onChange: load }))
        : [h('div', { class: 'empty' }, 'Никого не нашли')]));
    } catch (err) {
      results.replaceChildren(h('div', { class: 'error', style: { padding: '16px' } }, err.message));
    }
  };

  let timer = null;
  input.addEventListener('input', () => {
    clearTimeout(timer);
    timer = setTimeout(load, 250);
  });

  setPage(h('h1', { class: 'page-title' }, 'Люди'), input, results);
  load();
}

function userRow(user, { onChange } = {}) {
  const btn = me && user.id !== me.id ? followButton(user, onChange) : null;
  return h('div', { class: 'user-row' },
    avatar(user),
    h('div', { class: 'grow' },
      h('div', null, userLink(user), ' ', botBadge(user)),
      h('div', { class: 'muted small' }, `@${user.username} · подписчиков: ${user.followers}`),
    ),
    btn,
  );
}

function followButton(user, onChange) {
  const btn = h('button', { class: 'outline' });
  const paint = (following) => {
    btn.className = `outline${following ? ' on' : ''}`;
    btn.textContent = following ? 'Вы подписаны' : 'Подписаться';
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

async function renderProfile(id, username) {
  const { user, posts } = await api(`/users/${encodeURIComponent(username)}`);
  if (id !== renderId) return;

  const followBtn = user.isMe
    ? h('a', { href: '#/settings', class: 'button-link' }, h('button', { class: 'outline' }, 'Редактировать'))
    : followButton(user, () => render());

  const postsBox = h('div', { class: 'page-list' });
  if (posts.length === 0) {
    postsBox.append(h('div', { class: 'card empty' }, user.isMe ? 'Вы ещё ничего не опубликовали.' : 'Постов пока нет.'));
  }
  for (const post of posts) postsBox.append(postCard(post, { onDelete: () => render() }));

  const profileCard = h('section', { class: 'card' },
    h('div', { class: 'profile-head' },
      avatar(user, 'lg'),
      h('div', { class: 'profile-info' },
        h('h2', null, user.displayName, ' ', botBadge(user)),
        h('div', { class: 'handle' }, `@${user.username}`),
        user.bio ? h('p', { class: 'bio' }, user.bio) : h('p', { class: 'bio muted' }, 'Пока без описания'),
        h('div', { class: 'stats' },
          h('span', null, h('b', null, String(user.posts)), h('span', { class: 'label' }, plural(user.posts, ['пост', 'поста', 'постов']))),
          h('a', { href: `#/u/${encodeURIComponent(user.username)}/followers` },
            h('b', null, String(user.followers)), h('span', { class: 'label' }, plural(user.followers, ['подписчик', 'подписчика', 'подписчиков']))),
          h('a', { href: `#/u/${encodeURIComponent(user.username)}/following` },
            h('b', null, String(user.following)), h('span', { class: 'label' }, 'подписок')),
        ),
      ),
      h('div', null, followBtn),
    ),
  );

  setPage(profileCard, postsBox);
}

async function renderUserList(id, username, kind) {
  const { users } = await api(`/users/${encodeURIComponent(username)}/${kind}`);
  if (id !== renderId) return;
  const title = kind === 'followers' ? 'Подписчики' : 'Подписки';
  const list = h('div', { class: 'card user-list' },
    users.length
      ? users.map((u) => userRow(u, { onChange: () => render() }))
      : [h('div', { class: 'empty' }, kind === 'followers' ? 'Подписчиков пока нет' : 'Подписок пока нет')],
  );
  setPage(
    h('div', { class: 'row' },
      h('a', { href: `#/u/${encodeURIComponent(username)}`, class: 'ghost button-link' }, '← Назад'),
      h('h1', { class: 'page-title' }, title),
    ),
    list,
  );
}

// ---------- настройки ----------

function renderSettings() {
  const error = h('div', { class: 'error' });
  const ok = h('div', { class: 'small', style: { color: '#2a9d5b', minHeight: '18px' } });

  const displayName = h('input', { value: me.displayName, maxlength: 40, required: true });
  const bio = h('textarea', { maxlength: 160, rows: 3 }, me.bio || '');
  const color = h('input', { type: 'color', value: me.avatarColor });
  const preview = avatar(me, 'lg');
  const updatePreview = () => {
    preview.style.background = color.value;
    preview.textContent = (displayName.value.trim() || me.username).charAt(0).toUpperCase();
  };
  displayName.addEventListener('input', updatePreview);
  color.addEventListener('input', updatePreview);

  const saveBtn = h('button', { type: 'submit' }, 'Сохранить');

  const form = h('form', {
    class: 'card',
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
        ok.textContent = 'Сохранено';
      } catch (err) {
        error.textContent = err.message;
      } finally {
        saveBtn.disabled = false;
      }
    },
  },
    h('div', { class: 'row', style: { marginBottom: '16px' } }, preview,
      h('div', { class: 'grow' }, h('div', { class: 'name' }, me.displayName), h('div', { class: 'muted small' }, `@${me.username}`))),
    h('label', null, 'Имя', displayName),
    h('label', null, 'О себе', bio),
    h('label', null, 'Цвет аватара', color),
    error,
    ok,
    h('div', { class: 'row' }, saveBtn,
      h('a', { href: `#/u/${encodeURIComponent(me.username)}`, class: 'button-link' }, h('button', { type: 'button', class: 'outline' }, 'Открыть профиль'))),
  );

  const logout = h('button', {
    class: 'danger block',
    onclick: async () => {
      try {
        await api('/auth/logout', { method: 'POST' });
      } catch {
        // Даже при ошибке сбрасываем локальную сессию.
      }
      me = null;
      location.hash = '#/auth';
      render();
    },
  }, 'Выйти из аккаунта');

  setPage(h('h1', { class: 'page-title' }, 'Настройки'), form, h('div', { class: 'card' }, logout));
}

// ---------- старт ----------

render();
