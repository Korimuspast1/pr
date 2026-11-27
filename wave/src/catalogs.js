// Бесплатные/открытые каталоги, которые разрешают стриминг и не требуют
// «лазеек». Нужны, чтобы сервис реально играл музыку прямо в приложении:
//  * Jamendo   — независимые артисты, лицензии Creative Commons, официальный API.
//  * Internet Archive — общественное достояние / CC-аудио.

import { fetchJson, safeFetch, seconds, list } from './util.js';

const JAMENDO_ID = process.env.JAMENDO_CLIENT_ID || '';
const IA_COLLECTIONS = list(process.env.ARCHIVE_COLLECTIONS).length
  ? list(process.env.ARCHIVE_COLLECTIONS)
  : ['netlabels', 'audio_music', 'opensource_audio', '78rpm'];

export function jamendoStatus() {
  return {
    name: 'jamendo',
    configured: Boolean(JAMENDO_ID),
    note: JAMENDO_ID
      ? 'Jamendo API включён (CC-музыка, играет прямо в приложении)'
      : 'Добавьте JAMENDO_CLIENT_ID, чтобы включить миллионы CC-треков Jamendo',
  };
}

export function archiveStatus() {
  return {
    name: 'archive',
    configured: true,
    collections: IA_COLLECTIONS,
    note: 'Internet Archive: общественное достояние и CC-аудио, играет прямо в приложении',
  };
}

/* ----------------------------- Jamendo ----------------------------- */

function mapJamendo(t) {
  return {
    provider: 'jamendo',
    id: String(t.id),
    title: t.name || 'Без названия',
    artist: t.artist_name || 'Неизвестный',
    artistUrl: t.artist_id ? `https://www.jamendo.com/artist/${t.artist_id}` : null,
    durationSec: Number(t.duration) || 0,
    artwork: t.image || t.album_image || null,
    url: t.shareurl || null,
    genre: t.genre || null,
    playable: Boolean(t.audio),
    streamUrl: t.audio || null,
    license: t.license_ccurl || null,
    source: 'Jamendo (Creative Commons)',
  };
}

export async function searchJamendo(q, { limit = 20 } = {}) {
  if (!JAMENDO_ID) return { provider: 'jamendo', tracks: [], disabled: true };
  const url = new URL('https://api.jamendo.com/v3.0/tracks/');
  url.searchParams.set('client_id', JAMENDO_ID);
  url.searchParams.set('format', 'json');
  url.searchParams.set('limit', String(limit));
  url.searchParams.set('search', q);
  url.searchParams.set('include', 'musicinfo');
  url.searchParams.set('audioformat', 'mp32');
  const data = await fetchJson(url);
  return {
    provider: 'jamendo',
    tracks: (data.results || []).map(mapJamendo),
  };
}

/* ------------------------- Internet Archive ------------------------ */

async function iaSearch(q, limit) {
  const url = new URL('https://archive.org/advancedsearch.php');
  url.searchParams.set('q', `(${q}) AND mediatype:(audio) AND collection:(${IA_COLLECTIONS.join(' OR ')})`);
  url.searchParams.set('fl[]', 'identifier');
  for (const f of ['title', 'creator', 'year', 'licenseurl', 'downloads']) url.searchParams.append('fl[]', f);
  url.searchParams.set('rows', String(limit));
  url.searchParams.set('page', '1');
  url.searchParams.set('output', 'json');
  const data = await fetchJson(url, { timeout: 12000 });
  const docs = (data && data.response && data.response.docs) || [];
  return docs;
}

async function iaMetadata(identifier) {
  const data = await fetchJson(`https://archive.org/metadata/${encodeURIComponent(identifier)}`, {
    timeout: 12000,
  });
  const files = (data && data.files) || [];
  const audio = files
    .filter((f) => /\.(mp3|ogg|flac|m4a|opus)$/i.test(f.name || ''))
    .filter((f) => !/\.(png|jpg|jpeg|gif|webp)$/i.test(f.name || ''))
    .sort((a, b) => Number(b.size || 0) - Number(a.size || 0))[0];
  if (!audio) return null;
  const server = data.server || 'archive.org';
  const dir = data.dir || '';
  return {
    streamUrl: `https://${server}/download/${encodeURIComponent(identifier)}/${audio.name.split('/').map(encodeURIComponent).join('/')}`,
    fileTitle: String(audio.name || '').replace(/\.[^.]+$/, '').replace(/_/g, ' '),
    durationSec: Number(audio.length) ? Math.round(Number(audio.length)) : 0,
  };
}

export async function searchArchive(q, { limit = 12 } = {}) {
  const docs = await iaSearch(q, limit);
  const items = await Promise.all(
    docs.slice(0, Math.min(limit, 6)).map(async (doc) => {
      let file = null;
      try {
        file = await iaMetadata(doc.identifier);
      } catch {
        file = null;
      }
      if (!file) return null;
      return {
        provider: 'archive',
        id: String(doc.identifier),
        title: file.fileTitle || String(doc.title || doc.identifier),
        artist: Array.isArray(doc.creator) ? doc.creator[0] : String(doc.creator || 'Internet Archive'),
        artistUrl: `https://archive.org/details/${encodeURIComponent(doc.identifier)}`,
        durationSec: file.durationSec,
        artwork: `https://archive.org/services/img/${encodeURIComponent(doc.identifier)}`,
        url: `https://archive.org/details/${encodeURIComponent(doc.identifier)}`,
        genre: null,
        playable: true,
        streamUrl: file.streamUrl,
        license: doc.licenseurl || null,
        source: 'Internet Archive',
      };
    }),
  );
  return { provider: 'archive', tracks: items.filter(Boolean) };
}

/* ------------------- проксирование аудиопотока ---------------------- */

const ALLOWED_STREAM_HOSTS = [
  /(^|\.)jamendo\.com$/i,
  /(^|\.)jamendo\.net$/i,
  /(^|\.)archive\.org$/i,
  /(^|\.)us\.archive\.org$/i,
  /(^|\.)ia\d+\.us\.archive\.org$/i,
  /(^|\.)storage\.archive\.org$/i,
];

function hostAllowed(hostname) {
  return ALLOWED_STREAM_HOSTS.some((re) => re.test(hostname));
}

/**
 * Прокси для CC/PD-аудио: браузеру не мешают CORS и редиректы,
 * а сервер отдаёт Range, чтобы работал seek.
 */
export async function streamAudio(rawUrl, req, res) {
  let target;
  try {
    target = new URL(rawUrl);
  } catch {
    res.writeHead(400, { 'Content-Type': 'text/plain; charset=utf-8' });
    return res.end('плохой url');
  }
  if (target.protocol !== 'https:' || !hostAllowed(target.hostname)) {
    res.writeHead(403, { 'Content-Type': 'text/plain; charset=utf-8' });
    return res.end('поток с этого домена не разрешён');
  }

  const headers = { 'User-Agent': 'WaveMusic/1.0' };
  if (req.headers.range) headers.Range = req.headers.range;

  const upstream = await safeFetch(target, { timeout: 20000, headers });
  const outHeaders = {
    'Content-Type': upstream.headers.get('content-type') || 'audio/mpeg',
    'Accept-Ranges': 'bytes',
    'Cache-Control': 'public, max-age=3600',
    'Access-Control-Allow-Origin': '*',
  };
  const len = upstream.headers.get('content-length');
  if (len) outHeaders['Content-Length'] = len;
  const range = upstream.headers.get('content-range');
  if (range) outHeaders['Content-Range'] = range;

  res.writeHead(upstream.status, outHeaders);
  if (!upstream.body) return res.end();
  const reader = upstream.body.getReader();
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    if (!res.write(Buffer.from(value))) {
      await new Promise((r) => res.once('drain', r));
    }
  }
  res.end();
}

export { seconds };
