// Провайдер каталога: SoundCloud.
//
// Что здесь происходит и почему именно так:
//  * ВАЖНО. SoundCloud в своих Developer Terms запрещает скачивать/стримить
//    треки в обход официального плеера. Поэтому этот модуль — ТОЛЬКО витрина
//    и поиск: обложка, исполнитель, длительность, теги, ссылка, а также
//    официальный iframe-плеер (widget.soundcloud.com), который сам
//    проигрывает трек по своим правилам и платит артистам.
//  * Если в env есть SOUNDCLOUD_CLIENT_ID — поиск идёт через официальный
//    api.soundcloud.com (эндпоинт /tracks?q=...).
//  * Если ключа нет — работаем «витриной»: ищем на самом soundcloud.com через
//    публичный поиск страниц и вытаскиваем встроенный JSON (hydration) —
//    ровно те поля, которые SoundCloud и так отдаёт любому браузеру для
//    своего плеера. Это не обход защиты, а чтение публичной страницы.
//    Никаких запросов к внутренним api-v2 эндпоинтам и никаких
//    stream/transcode URL: плеер потом открывается официальный.

import { UA, safeFetch, artworkUrl, seconds, list } from './util.js';

const CLIENT_ID = process.env.SOUNDCLOUD_CLIENT_ID || '';
const OEMBED_HOSTS = list(process.env.SOUNDCLOUD_OEMBED_HOSTS); // напр. d.bandit.example
const OEMBED_HOST = 'https://soundcloud.com/oembed';

/* ------------------------------------------------------------------ */
/* официальный API (если есть client_id)                               */
/* ------------------------------------------------------------------ */

async function apiSearch(q, limit) {
  const url = new URL('https://api.soundcloud.com/tracks');
  url.searchParams.set('q', q);
  url.searchParams.set('limit', String(limit));
  url.searchParams.set('linked_partitioning', '1');
  url.searchParams.set('client_id', CLIENT_ID);

  const res = await safeFetch(url, {
    headers: { Accept: 'application/json', 'User-Agent': UA },
  });
  if (res.status === 401 || res.status === 403) {
    const e = new Error('SOUNDCLOUD_CLIENT_ID не принят SoundCloud (401/403)');
    e.code = 'bad_client_id';
    throw e;
  }
  if (!res.ok) {
    const e = new Error(`api.soundcloud.com вернул ${res.status}`);
    e.code = 'upstream_error';
    e.status = res.status;
    throw e;
  }
  const data = await res.json();
  return (data.collection || []).map(mapApiTrack);
}

function mapApiTrack(t) {
  return {
    provider: 'soundcloud',
    id: String(t.id),
    title: t.title || 'Без названия',
    artist: (t.user && (t.user.username || t.user.full_name)) || 'Неизвестный',
    artistUrl: t.user && t.user.permalink_url ? t.user.permalink_url : null,
    durationSec: seconds(t.duration),
    artwork: artworkUrl(t.artwork_url || (t.user && t.user.avatar_url)),
    url: t.permalink_url || null,
    genre: t.genre || null,
    playable: Boolean(t.permalink_url),
    plays: Number.isFinite(t.playback_count) ? t.playback_count : null,
    likes: Number.isFinite(t.likes_count) ? t.likes_count : null,
  };
}

/* ------------------------------------------------------------------ */
/* «витрина» без ключа: публичный поиск страниц soundcloud.com          */
/* ------------------------------------------------------------------ */

const SC_HOSTS = /^(?:m\.)?(?:www\.)?soundcloud\.com$/i;

/**
 * Разбираем публичную страницу SoundCloud: проходим по ней один раз,
 * отслеживаем строки/фигурные скобки и разбираем каждый объект,
 * в котором есть permalink_url. Никаких внутренних API — только то,
 * что SoundCloud и так отдаёт браузеру для своего плеера.
 */
export function extractTracksFromHtml(html) {
  const out = [];
  const seen = new Set();
  if (!html) return out;

  const stack = [];
  let inStr = false;
  let esc = false;

  for (let i = 0; i < html.length; i++) {
    const c = html[i];
    if (inStr) {
      if (esc) esc = false;
      else if (c === '\\') esc = true;
      else if (c === '"') inStr = false;
      continue;
    }
    if (c === '"') {
      inStr = true;
      continue;
    }
    if (c === '{') {
      stack.push(i);
      continue;
    }
    if (c !== '}') continue;

    const start = stack.pop();
    if (start === undefined) continue;
    const chunk = html.slice(start, i + 1);
    if (chunk.length > 200_000 || !chunk.includes('"permalink_url"')) continue;

    const track = trackFromChunk(chunk);
    if (track && !seen.has(track.id)) {
      seen.add(track.id);
      out.push(track);
    }
    if (out.length >= 300) break;
  }
  return out;
}

function trackFromChunk(chunk) {
  let obj = null;
  try {
    obj = JSON.parse(chunk);
  } catch {
    obj = null;
  }

  if (obj && typeof obj === 'object') {
    const t = normalizeScObject(obj);
    if (t) return t;
    // бывает, что трек лежит на уровень глубже: {"data": {...}}
    for (const key of ['data', 'sound', 'track', 'playlist']) {
      const t2 = obj[key] && typeof obj[key] === 'object' ? normalizeScObject(obj[key]) : null;
      if (t2) return t2;
    }
    return null;
  }

  // JSON не собрался — вытаскиваем поля регулярками из этого же объекта
  const title = str(chunk, /"title"\s*:\s*"((?:[^"\\]|\\.)*)"/);
  const permalink = str(chunk, /"permalink_url"\s*:\s*"((?:[^"\\]|\\.)*)"/);
  if (!title || !permalink) return null;
  const url = unescapeJson(permalink).replace(/\\\//g, '/');
  return {
    provider: 'soundcloud',
    id: String(num(chunk, /"id"\s*:\s*(\d+)/) || permalinkId(url) || url),
    title: unescapeJson(title),
    artist: unescapeJson(str(chunk, /"username"\s*:\s*"((?:[^"\\]|\\.)*)"/) || '') || 'SoundCloud',
    artistUrl: null,
    durationSec: seconds(num(chunk, /"duration"\s*:\s*(\d+)/)),
    artwork: artworkUrl(unescapeJson(str(chunk, /"(?:artwork_url|avatar_url)"\s*:\s*"((?:[^"\\]|\\.)*)"/) || '')),
    url: url.split('?')[0],
    genre: unescapeJson(str(chunk, /"genre"\s*:\s*"((?:[^"\\]|\\.)*)"/) || '') || null,
    playable: true,
    plays: num(chunk, /"playback_count"\s*:\s*(\d+)/),
    likes: num(chunk, /"likes_count"\s*:\s*(\d+)/),
  };
}

function normalizeScObject(o) {
  if (!o || typeof o !== 'object') return null;
  const url = typeof o.permalink_url === 'string' ? o.permalink_url : '';
  if (!/^https?:\/\/(?:m\.|www\.)?soundcloud\.com\//i.test(url)) return null;
  if (!o.title && !o.permalink) return null;
  const user = o.user && typeof o.user === 'object' ? o.user : null;
  return {
    provider: 'soundcloud',
    id: String(o.id ?? url),
    title: String(o.title || o.permalink || 'Без названия'),
    artist: (user && (user.username || user.full_name)) || 'SoundCloud',
    artistUrl: user && user.permalink_url ? user.permalink_url : null,
    durationSec: seconds(o.duration),
    artwork: artworkUrl(o.artwork_url || (user && user.avatar_url)),
    url: url.split('?')[0],
    genre: o.genre || null,
    playable: true,
    plays: Number.isFinite(o.playback_count) ? o.playback_count : null,
    likes: Number.isFinite(o.likes_count) ? o.likes_count : null,
  };
}

function str(window, re) {
  const m = window.match(re);
  return m ? unescapeJson(m[1]) : null;
}
function num(window, re) {
  const m = window.match(re);
  return m ? Number(m[1]) : null;
}
function permalinkId(url) {
  const m = String(url).replace(/\\\//g, '/').match(/soundcloud\.com\/([^/?#]+)\/([^/?#]+)/);
  return m ? `${m[1]}/${m[2]}` : null;
}
function unescapeJson(s) {
  if (!s) return s;
  return String(s)
    .replace(/\\u([\da-fA-F]{4})/g, (_, h) => String.fromCharCode(parseInt(h, 16)))
    .replace(/\\\//g, '/')
    .replace(/\\"/g, '"')
    .replace(/\\\\/g, '\\');
}

const RESERVED_SEGMENTS = new Set([
  'search', 'you', 'stream', 'upload', 'settings', 'charts', 'discover', 'feed',
  'sets', 'albums', 'tracks', 'pages', 'imprint', 'terms-of-use', 'jobs', 'artists',
  'popular', 'tags', 'groups', 'people', 'playlists', 'likes', 'reposts', 'comments',
]);

/**
 * Если страница отдала только ссылки (без hydration) — собираем треки по
 * ссылкам soundcloud.com/artist/track. Название берём из текста ссылки,
 * иначе красим слаг.
 */
export function fallbackLinksFromHtml(html, limit = 20) {
  const tracks = [];
  const seen = new Set();
  if (!html) return tracks;

  const re = /soundcloud\.com\/([A-Za-z0-9_-]{2,})\/([A-Za-z0-9_-]{2,})(?=["'<>\s)&#?]|$)/g;
  let m;
  while ((m = re.exec(html)) !== null) {
    const [, artistSlug, trackSlug] = m;
    if (RESERVED_SEGMENTS.has(artistSlug.toLowerCase())) continue;
    const url = `https://soundcloud.com/${artistSlug}/${trackSlug}`;
    if (seen.has(url)) continue;

    // название: текст ближайшего якоря после ссылки, если он есть
    const tail = html.slice(m.index, m.index + 600);
    const anchor = tail.match(/^[^>]*>([^<>{}]{2,200})</);
    const title = anchor ? stripTags(anchor[1]) : pretty(trackSlug);
    if (!title) continue;

    seen.add(url);
    tracks.push({
      provider: 'soundcloud',
      id: `${artistSlug}/${trackSlug}`,
      title,
      artist: pretty(artistSlug),
      artistUrl: `https://soundcloud.com/${artistSlug}`,
      durationSec: 0,
      artwork: null,
      url,
      genre: null,
      playable: true,
      plays: null,
      likes: null,
    });
    if (tracks.length >= limit) break;
  }
  return tracks;
}

function pretty(slug) {
  return String(slug)
    .replace(/[-_]+/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase())
    .trim();
}

function stripTags(s) {
  return String(s || '')
    .replace(/<[^>]*>/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/\s+/g, ' ')
    .trim();
}

async function scrapeSearch(q, limit) {
  const url = `https://soundcloud.com/search/sounds?q=${encodeURIComponent(q)}`;
  const res = await safeFetch(url, {
    timeout: 12000,
    headers: {
      'User-Agent': UA,
      Accept: 'text/html,application/xhtml+xml',
      'Accept-Language': 'en-US,en;q=0.9,ru;q=0.8',
    },
  });
  if (!res.ok) {
    const e = new Error(`soundcloud.com/search вернул ${res.status}`);
    e.code = 'upstream_error';
    e.status = res.status;
    throw e;
  }
  const html = await res.text();
  const fromHydration = extractTracksFromHtml(html);
  return (fromHydration.length ? fromHydration : fallbackLinksFromHtml(html, limit)).slice(0, limit);
}

/* ------------------------------------------------------------------ */
/* публичный API модуля                                                */
/* ------------------------------------------------------------------ */

export function providerStatus() {
  return {
    name: 'soundcloud',
    configured: Boolean(CLIENT_ID),
    mode: CLIENT_ID ? 'official-api' : 'public-search',
    embedHosts: OEMBED_HOSTS,
    note: CLIENT_ID
      ? 'Поиск через официальный api.soundcloud.com'
      : 'Поиск по публичным страницам SoundCloud. Для официального API добавьте SOUNDCLOUD_CLIENT_ID.',
  };
}

export async function searchSoundCloud(q, { limit = 24 } = {}) {
  const query = String(q || '').trim();
  if (!query) return { provider: 'soundcloud', mode: 'empty', tracks: [] };

  if (CLIENT_ID) {
    try {
      return {
        provider: 'soundcloud',
        mode: 'official-api',
        tracks: await apiSearch(query, limit),
      };
    } catch (err) {
      // ключ сломан — не роняем поиск, падаем на публичные страницы
      if (err.code !== 'bad_client_id') throw err;
      console.warn('[soundcloud]', err.message, '— переключаюсь на публичный поиск');
    }
  }

  try {
    const tracks = await scrapeSearch(query, limit);
    if (tracks.length) return { provider: 'soundcloud', mode: 'public-search', tracks };
  } catch (err) {
    console.warn('[soundcloud] поиск не удался:', err.message);
  }

  // Последняя попытка — обычная страница поиска в другом формате.
  const res = await safeFetch(
    `https://soundcloud.com/search?q=${encodeURIComponent(query)}`,
    { timeout: 12000, headers: { 'User-Agent': UA, Accept: 'text/html' } },
  );
  if (!res.ok) {
    const e = new Error(`SoundCloud недоступен (${res.status})`);
    e.code = 'upstream_error';
    throw e;
  }
  const html = await res.text();
  const fromHydration = extractTracksFromHtml(html);
  const tracks = fromHydration.length ? fromHydration : fallbackLinksFromHtml(html, limit);
  return { provider: 'soundcloud', mode: 'public-search', tracks: tracks.slice(0, limit) };
}

/** Ссылка на официальный встраиваемый плеер для трека/плейлиста. */
export function embedFor(trackUrl) {
  const u = new URL('https://w.soundcloud.com/player/');
  u.searchParams.set('url', trackUrl);
  u.searchParams.set('color', '#7c5cff');
  u.searchParams.set('auto_play', 'false');
  u.searchParams.set('hide_related', 'false');
  u.searchParams.set('show_comments', 'true');
  u.searchParams.set('show_user', 'true');
  u.searchParams.set('visual', 'true');
  return u.toString();
}

export function isValidSoundCloudUrl(raw) {
  let u;
  try {
    u = new URL(String(raw));
  } catch {
    return false;
  }
  if (u.protocol !== 'https:') return false;
  if (!SC_HOSTS.test(u.hostname)) return false;
  // только /artist/track — без служебных страниц
  const parts = u.pathname.split('/').filter(Boolean);
  if (parts.length !== 2) return false;
  if (['search', 'you', 'stream', 'upload', 'settings', 'charts', 'discover', 'feed'].includes(parts[0])) return false;
  return true;
}

/** oEmbed SoundCloud: для Server-Side Rendering карточки (и для проверки, что трек жив). */
export async function oembed(trackUrl) {
  if (!isValidSoundCloudUrl(trackUrl)) {
    const e = new Error('нужна ссылка вида https://soundcloud.com/artist/track');
    e.code = 'bad_url';
    throw e;
  }
  const u = new URL(OEMBED_HOST);
  u.searchParams.set('format', 'json');
  u.searchParams.set('url', trackUrl);
  if (OEMBED_HOSTS.length) u.searchParams.set('maxwidth', '100%');

  const res = await safeFetch(u, { timeout: 8000, headers: { 'User-Agent': UA } });
  if (!res.ok) {
    const e = new Error(`oEmbed вернул ${res.status}`);
    e.code = 'upstream_error';
    e.status = res.status;
    throw e;
  }
  const data = await res.json();
  return {
    title: data.title || null,
    author: data.author_name || null,
    authorUrl: data.author_url || null,
    artwork: data.thumbnail_url || null,
    html: data.html || null,
    embed: embedFor(trackUrl),
    url: trackUrl,
  };
}
