// Wave — логика интерфейса. Без сборки, только ES-модули.

const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

const state = {
  view: 'search',
  query: '',
  source: 'all',
  page: 1,
  perPage: 24,
  tracks: [],
  filtered: [],
  queue: [],
  index: -1,
  current: null,
  favorites: load('wave.favorites', []),
  history: load('wave.history', []),
  providers: [],
};

const els = {
  form: $('#search-form'),
  q: $('#q'),
  chips: $('#chips'),
  grid: $('#grid'),
  hero: $('#hero'),
  status: $('#status'),
  pagination: $('#pagination'),
  toast: $('#toast'),
  audio: $('#audio'),
  play: $('#play'),
  prev: $('#prev'),
  next: $('#next'),
  progress: $('#progress'),
  volume: $('#volume'),
  tCur: $('#t-cur'),
  tDur: $('#t-dur'),
  cover: $('#cover'),
  npTitle: $('#np-title'),
  npArtist: $('#np-artist'),
  scSlot: $('#sc-slot'),
  scOpen: $('#sc-open'),
  player: $('#player'),
  sources: $('#sources'),
  quick: $('#quick'),
  cardTpl: $('#card-tpl'),
};

/* ------------------------------ storage ------------------------------ */

function load(key, fallback) {
  try {
    const raw = localStorage.getItem(key);
    return raw ? JSON.parse(raw) : fallback;
  } catch {
    return fallback;
  }
}
function save(key, value) {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    /* приватный режим — просто не сохраняем */
  }
}

/* ------------------------------ helpers ------------------------------ */

function fmtTime(sec) {
  if (!Number.isFinite(sec) || sec <= 0) return '0:00';
  const m = Math.floor(sec / 60);
  const s = Math.floor(sec % 60);
  return `${m}:${String(s).padStart(2, '0')}`;
}

function esc(s) {
  return String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

let toastTimer;
function toast(msg, ms = 3200) {
  els.toast.textContent = msg;
  els.toast.hidden = false;
  requestAnimationFrame(() => els.toast.classList.add('show'));
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => {
    els.toast.classList.remove('show');
    setTimeout(() => (els.toast.hidden = true), 220);
  }, ms);
}

async function api(path) {
  const res = await fetch(path, { headers: { Accept: 'application/json' } });
  let data = null;
  try {
    data = await res.json();
  } catch {
    data = null;
  }
  if (!res.ok) {
    const err = new Error((data && data.error) || `HTTP ${res.status}`);
    err.status = res.status;
    err.data = data;
    throw err;
  }
  return data;
}

/* ------------------------------- поиск ------------------------------- */

function skeletons(n = 12) {
  els.grid.innerHTML = Array.from({ length: n })
    .map(
      () => `<div class="skeleton"><div class="sk-art shimmer"></div>
        <div class="sk-line shimmer" style="width:80%"></div>
        <div class="sk-line shimmer" style="width:50%"></div></div>`,
    )
    .join('');
}

let searchSeq = 0;

async function runSearch(q, { page = 1, source = state.source } = {}) {
  q = (q || '').trim();
  if (!q) {
    toast('Напиши, что искать — трек, артист или жанр');
    els.q.focus();
    return;
  }

  state.query = q;
  state.source = source;
  state.page = page;
  els.q.value = q;
  els.hero.hidden = true;
  setStatus('');
  skeletons();
  els.pagination.hidden = true;

  const seq = ++searchSeq;
  const params = new URLSearchParams({ q, limit: '40', source });
  try {
    const data = await api(`/api/search?${params}`);
    if (seq !== searchSeq) return; // пользователь уже ищет другое

    state.tracks = data.tracks || [];
    state.filtered = state.tracks;
    state.view = 'search';

    if (!state.tracks.length) {
      els.grid.innerHTML = '';
      const hint = (data.errors || []).length
        ? `Источники не ответили: ${data.errors.map((e) => `${e.provider} (${e.error})`).join('; ')}`
        : 'Ничего не нашлось. Попробуй другое написание или латиницу.';
      setStatus(`По запросу «${esc(q)}» пусто. ${esc(hint)}`);
      return;
    }

    render();
    if (data.demo) {
      setStatus(
        'Каталоги сейчас недоступны (нет сети или провайдер молчит) — показываю демо-треки, ' +
          'сгенерированные локально. Плеер и поиск работают полностью.',
      );
    } else if ((data.errors || []).length) {
      setStatus(
        `Часть каталогов не ответила: ${data.errors.map((e) => `${e.provider} (${e.error})`).join('; ')}`,
      );
    } else {
      setStatus(`Найдено ${data.count} треков по запросу «${esc(q)}»`, 'ok');
      setTimeout(() => setStatus(''), 2600);
    }
    history.replaceState(null, '', `/?q=${encodeURIComponent(q)}&source=${source}`);
  } catch (err) {
    if (seq !== searchSeq) return;
    els.grid.innerHTML = '';
    setStatus(
      err.status === 429
        ? 'Слишком много запросов подряд. Подожди минуту.'
        : `Поиск не удался: ${esc(err.message)}`,
    );
  }
}

function setStatus(text, type = '') {
  if (!text) {
    els.status.hidden = true;
    els.status.textContent = '';
    return;
  }
  els.status.hidden = false;
  els.status.className = `status ${type}`;
  els.status.innerHTML = text;
}

/* ------------------------------ рендер ------------------------------- */

function render() {
  const list = state.filtered;
  const totalPages = Math.max(1, Math.ceil(list.length / state.perPage));
  state.page = Math.min(state.page, totalPages);
  const start = (state.page - 1) * state.perPage;
  const pageItems = list.slice(start, start + state.perPage);

  els.grid.innerHTML = '';
  const frag = document.createDocumentFragment();
  for (const track of pageItems) frag.appendChild(card(track));
  els.grid.appendChild(frag);

  if (totalPages > 1) {
    els.pagination.hidden = false;
    els.pagination.innerHTML = '';
    const prev = document.createElement('button');
    prev.textContent = '← Назад';
    prev.disabled = state.page === 1;
    prev.onclick = () => {
      state.page -= 1;
      render();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    };
    const info = document.createElement('button');
    info.disabled = true;
    info.textContent = `${state.page} / ${totalPages}`;
    const next = document.createElement('button');
    next.textContent = 'Вперёд →';
    next.disabled = state.page === totalPages;
    next.onclick = () => {
      state.page += 1;
      render();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    };
    els.pagination.append(prev, info, next);
  } else {
    els.pagination.hidden = true;
  }

  highlightPlaying();
}

function card(track) {
  const node = els.cardTpl.content.firstElementChild.cloneNode(true);
  node.dataset.id = `${track.provider}:${track.id}`;

  const title = $('.card-title', node);
  const artist = $('.card-artist', node);
  title.textContent = track.title;
  title.title = track.title;
  artist.textContent = track.artist;
  artist.title = track.artist;

  const art = $('.card-art', node);
  if (track.artwork) {
    const img = document.createElement('img');
    img.src = track.artwork;
    img.alt = '';
    img.onload = () => img.classList.add('loaded');
    img.onerror = () => {
      img.remove();
      art.insertAdjacentHTML('beforeend', '<div class="noart">◍</div>');
    };
    art.appendChild(img);
  } else {
    art.insertAdjacentHTML('beforeend', '<div class="noart">◍</div>');
  }

  const providerTag = $('.tag.provider', node);
  providerTag.textContent =
    { soundcloud: 'SoundCloud', jamendo: 'Jamendo', archive: 'Archive', demo: 'Wave Demo' }[track.provider] ||
    track.provider;
  providerTag.dataset.p = track.provider;

  const durTag = $('.tag.dur', node);
  if (track.durationSec) durTag.textContent = fmtTime(track.durationSec);

  const genreTag = $('.tag.genre', node);
  if (track.genre) genreTag.textContent = track.genre;

  const playBtn = $('.card-play', node);
  playBtn.onclick = (e) => {
    e.stopPropagation();
    playTrack(track);
  };
  node.addEventListener('dblclick', () => playTrack(track));

  const favBtn = $('.mini.fav', node);
  syncFav(favBtn, track);
  favBtn.onclick = (e) => {
    e.stopPropagation();
    toggleFavorite(track, favBtn);
  };

  const link = $('.mini.link', node);
  if (track.url) {
    link.href = track.url;
  } else {
    link.remove();
  }
  return node;
}

function syncFav(btn, track) {
  const on = state.favorites.some((f) => f.provider === track.provider && f.id === track.id);
  btn.classList.toggle('on', on);
  btn.textContent = on ? '♥' : '♡';
}

function highlightPlaying() {
  const id = state.current ? `${state.current.provider}:${state.current.id}` : null;
  $$('.card').forEach((c) => c.classList.toggle('playing', c.dataset.id === id));
  els.play.textContent = !els.audio.paused && state.current && state.current.kind === 'audio' ? '⏸' : state.current?.kind === 'soundcloud' ? '■' : '▶';
}

/* ---------------------------- избранное ------------------------------ */

function toggleFavorite(track, btn) {
  const idx = state.favorites.findIndex((f) => f.provider === track.provider && f.id === track.id);
  if (idx >= 0) {
    state.favorites.splice(idx, 1);
    toast('Убрано из избранного');
  } else {
    state.favorites.unshift(track);
    state.favorites = state.favorites.slice(0, 300);
    toast('Добавлено в избранное');
  }
  save('wave.favorites', state.favorites);
  if (btn) syncFav(btn, track);
  if (state.view === 'favorites') showFavorites();
}

function showFavorites() {
  state.view = 'favorites';
  els.hero.hidden = true;
  state.filtered = state.favorites;
  state.page = 1;
  setStatus('');
  if (!state.favorites.length) {
    els.grid.innerHTML = '';
    setStatus('Избранное пустое. Жми ♡ на карточке трека.');
    return;
  }
  render();
}

function showHistory() {
  state.view = 'history';
  els.hero.hidden = true;
  state.filtered = state.history;
  state.page = 1;
  setStatus('');
  if (!state.history.length) {
    els.grid.innerHTML = '';
    setStatus('История пустая — включи что-нибудь.');
    return;
  }
  render();
}

function pushHistory(track) {
  state.history = [track, ...state.history.filter((t) => !(t.provider === track.provider && t.id === track.id))].slice(0, 100);
  save('wave.history', state.history);
}

/* --------------------------- воспроизведение -------------------------- */

function setNowPlaying(track) {
  els.npTitle.textContent = track.title;
  els.npArtist.textContent = track.artist;
  els.npTitle.title = track.title;
  els.cover.innerHTML = track.artwork
    ? `<img src="${esc(track.artwork)}" alt="" onerror="this.remove()">`
    : '◍';
}

function playTrack(track, { addToQueue = true } = {}) {
  if (addToQueue) {
    const list = state.filtered.length ? state.filtered : state.tracks;
    state.queue = list;
    state.index = list.findIndex((t) => t.provider === track.provider && t.id === track.id);
  }
  state.current = track;
  setNowPlaying(track);
  pushHistory(track);
  highlightPlaying();

  if (track.kind === 'audio' && track.stream) {
    stopSoundCloud();
    els.audio.src = track.stream;
    els.audio.play().catch((err) => {
      toast(`Не удалось включить: ${err.message}. Попробуй другой трек.`);
    });
  } else if (track.provider === 'soundcloud' && track.url) {
    openSoundCloud(track);
  } else {
    toast('У этого трека нет доступного способа воспроизведения');
  }
}

/* Официальный iframe-плеер SoundCloud: трек играет SoundCloud, мы не трогаем поток. */
function openSoundCloud(track) {
  els.audio.pause();
  els.audio.removeAttribute('src');
  els.audio.load();

  const src = `https://w.soundcloud.com/player/?url=${encodeURIComponent(track.url)}` +
    '&color=%237c5cff&auto_play=true&hide_related=false&show_comments=true&show_user=true&visual=true';

  els.scSlot.hidden = false;
  els.scSlot.innerHTML = `<iframe title="SoundCloud плеер" allow="autoplay" src="${src}"></iframe>`;
  els.player.classList.add('sc-mode');
  els.scOpen.hidden = false;
  els.scOpen.onclick = () => window.open(track.url, '_blank', 'noopener');
  els.play.textContent = '■';
  els.play.disabled = false;
  els.progress.disabled = true;
  els.tCur.textContent = 'SC';
  els.tDur.textContent = fmtTime(track.durationSec);
  toast('Играет официальный плеер SoundCloud — так артист получает статистику и выплаты', 4200);
}

function stopSoundCloud() {
  if (!els.scSlot.hidden) {
    els.scSlot.hidden = true;
    els.scSlot.innerHTML = '';
    els.player.classList.remove('sc-mode');
    els.scOpen.hidden = true;
    els.progress.disabled = false;
  }
}

els.audio.addEventListener('timeupdate', () => {
  const dur = els.audio.duration || 0;
  const cur = els.audio.currentTime || 0;
  if (Number.isFinite(dur) && dur > 0) {
    els.progress.value = String(Math.round((cur / dur) * 1000));
    els.progress.style.setProperty('--p', `${(cur / dur) * 100}%`);
    els.tDur.textContent = fmtTime(dur);
  }
  els.tCur.textContent = fmtTime(cur);
});

els.audio.addEventListener('play', highlightPlaying);
els.audio.addEventListener('pause', highlightPlaying);
els.audio.addEventListener('ended', () => {
  highlightPlaying();
  next();
});
els.audio.addEventListener('loadedmetadata', () => {
  els.progress.disabled = false;
  els.tDur.textContent = fmtTime(els.audio.duration);
});
els.audio.addEventListener('error', () => {
  if (!els.audio.src) return;
  toast('Трек не открылся. Возможно, он удалён или регион ограничен — попробуй другой.', 4000);
  els.play.textContent = '▶';
});

els.play.onclick = () => {
  const cur = state.current;
  if (!cur) {
    if (state.filtered.length) playTrack(state.filtered[0]);
    return;
  }
  if (cur.kind === 'soundcloud') {
    if (!els.scSlot.hidden) {
      stopSoundCloud();
      els.play.textContent = '▶';
    } else {
      openSoundCloud(cur);
    }
    return;
  }
  if (els.audio.paused) {
    els.audio.play().catch(() => toast('Браузер заблокировал автозапуск — нажми ещё раз'));
  } else {
    els.audio.pause();
  }
};

function step(delta) {
  if (!state.queue.length) return;
  let i = state.index;
  i = i < 0 ? 0 : (i + delta + state.queue.length) % state.queue.length;
  state.index = i;
  playTrack(state.queue[i], { addToQueue: false });
}
els.prev.onclick = () => step(-1);
els.next.onclick = next;
function next() {
  step(1);
}

els.progress.oninput = () => {
  const dur = els.audio.duration || 0;
  if (dur > 0) {
    els.audio.currentTime = (Number(els.progress.value) / 1000) * dur;
    els.progress.style.setProperty('--p', `${(Number(els.progress.value) / 1000) * 100}%`);
  }
};

els.volume.oninput = () => {
  els.audio.volume = Number(els.volume.value);
  els.volume.style.setProperty('--p', `${Number(els.volume.value) * 100}%`);
};
els.audio.volume = Number(els.volume.value);
els.volume.style.setProperty('--p', '90%');

document.addEventListener('keydown', (e) => {
  if (e.target.matches('input, textarea')) return;
  if (e.code === 'Space') {
    e.preventDefault();
    els.play.click();
  }
  if (e.code === 'ArrowRight' && e.shiftKey) next();
  if (e.code === 'ArrowLeft' && e.shiftKey) step(-1);
});

/* ------------------------------ источники ---------------------------- */

async function loadProviders() {
  try {
    const data = await api('/api/providers');
    state.providers = data.providers || [];
    els.sources.innerHTML = state.providers
      .map((p) => {
        const on = p.name === 'archive' || p.configured;
        const cls = on ? 'on' : 'warn';
        const label = { soundcloud: 'SoundCloud', jamendo: 'Jamendo', archive: 'Internet Archive' }[p.name] || p.name;
        return `<div class="source-row"><span class="dot ${cls}"></span>${esc(label)}
          <span style="opacity:.6">· ${on ? 'вкл' : 'нет ключа'}</span></div>`;
      })
      .join('');
  } catch {
    els.sources.innerHTML = '<div class="source-row"><span class="dot warn"></span>статус недоступен</div>';
  }
}

/* ------------------------------- события ----------------------------- */

els.form.addEventListener('submit', (e) => {
  e.preventDefault();
  runSearch(els.q.value, { page: 1 });
});

els.chips.addEventListener('click', (e) => {
  const btn = e.target.closest('.chip');
  if (!btn) return;
  $$('.chip').forEach((c) => c.classList.toggle('active', c === btn));
  state.source = btn.dataset.source;
  if (state.query) runSearch(state.query, { page: 1, source: state.source });
});

els.quick.addEventListener('click', (e) => {
  const btn = e.target.closest('button');
  if (!btn) return;
  runSearch(btn.dataset.q, { page: 1 });
});

$$('.nav-item').forEach((btn) => {
  btn.addEventListener('click', () => {
    $$('.nav-item').forEach((b) => b.classList.toggle('active', b === btn));
    const view = btn.dataset.view;
    if (view === 'search') {
      if (state.query) {
        runSearch(state.query, { page: 1 });
      } else {
        els.hero.hidden = false;
        els.grid.innerHTML = '';
        setStatus('');
        els.pagination.hidden = true;
        history.replaceState(null, '', '/');
      }
    }
    if (view === 'favorites') showFavorites();
    if (view === 'history') showHistory();
  });
});

/* -------------------------------- старт ------------------------------- */

const params = new URLSearchParams(location.search);
const startQ = params.get('q');
const startSource = params.get('source');
if (startSource) {
  state.source = startSource;
  $$('.chip').forEach((c) => c.classList.toggle('active', c.dataset.source === startSource));
}
loadProviders();
if (startQ) {
  state.query = startQ;
  els.q.value = startQ;
  runSearch(startQ, { page: 1, source: state.source });
} else {
  els.q.focus({ preventScroll: true });
}
