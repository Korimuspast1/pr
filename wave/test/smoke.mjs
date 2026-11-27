// Смоук-тесты без внешней сети: парсеры SoundCloud, валидация ссылок, API-роуты.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { once } from 'node:events';

import { extractTracksFromHtml, fallbackLinksFromHtml, isValidSoundCloudUrl, embedFor } from '../src/soundcloud.js';

const HOST = '127.0.0.1';
const PORT = 38001;

let passed = 0;
function test(name, fn) {
  try {
    fn();
    passed++;
    console.log(`  ok  ${name}`);
  } catch (err) {
    console.error(`  FAIL ${name}\n       ${err.message}`);
    process.exitCode = 1;
  }
}

console.log('\nпарсеры');

const hydration = `
<script>window.__sc_hydration = [{"hydratable":"sound","data":{
  "id":123456789,"kind":"track","title":"Midnight Drive",
  "permalink_url":"https://soundcloud.com/someartist/midnight-drive",
  "duration":245000,"genre":"Electronic","playback_count":1000,"likes_count":42,
  "artwork_url":"https://i1.sndcdn.com/artworks-abc-large.jpg",
  "user":{"username":"Some Artist","permalink_url":"https://soundcloud.com/someartist"}
}},{"hydratable":"sound","data":{
  "id":987654321,"title":"Улетай","permalink_url":"https://soundcloud.com/dj-test/uletay",
  "duration":180500,"genre":"Phonk","artwork_url":"https://i1.sndcdn.com/artworks-xyz-t200x200.jpg",
  "user":{"username":"DJ Test"}
}}];</script>`;

test('hydration → треки с полями', () => {
  const tracks = extractTracksFromHtml(hydration);
  assert.equal(tracks.length, 2);
  const [a, b] = tracks;
  assert.equal(a.id, '123456789');
  assert.equal(a.title, 'Midnight Drive');
  assert.equal(a.artist, 'Some Artist');
  assert.equal(a.durationSec, 245);
  assert.equal(a.genre, 'Electronic');
  assert.equal(a.url, 'https://soundcloud.com/someartist/midnight-drive');
  assert.match(a.artwork, /-t500x500\.jpg$/);
  assert.equal(b.title, 'Улетай'); // юникод не ломается
  assert.equal(b.durationSec, 181);
});

test('дубликаты в hydration схлопываются', () => {
  const tracks = extractTracksFromHtml(hydration + hydration);
  assert.equal(tracks.length, 2);
});

test('fallback по ссылкам, когда hydration нет', () => {
  const html = `<div><a href="https://soundcloud.com/artist-one/track-a?si=1">Track A</a>
    <a href="https://soundcloud.com/artist-two/track-b">Track B</a>
    <a href="https://soundcloud.com/artist-two/sets/my-set">Set</a>
    <script>var x = 1;</script></div>`;
  const tracks = fallbackLinksFromHtml(html);
  assert.equal(tracks.length, 2);
  assert.equal(tracks[0].title, 'Track A');
  assert.equal(tracks[0].url, 'https://soundcloud.com/artist-one/track-a');
  assert.equal(tracks[1].artist, 'Artist Two');
});

test('мусор вместо HTML не роняет парсер', () => {
  assert.deepEqual(extractTracksFromHtml(''), []);
  assert.deepEqual(extractTracksFromHtml(null), []);
  assert.deepEqual(fallbackLinksFromHtml('<html></html>'), []);
});

console.log('\nвалидация ссылок');

test('нормальные ссылки проходят', () => {
  assert.ok(isValidSoundCloudUrl('https://soundcloud.com/artist/track'));
  assert.ok(isValidSoundCloudUrl('https://m.soundcloud.com/artist/track-2'));
});

test('мусор не проходит', () => {
  for (const bad of [
    'http://soundcloud.com/artist/track',
    'https://soundcloud.com.evil.com/artist/track',
    'https://soundcloud.com/search/sounds',
    'https://soundcloud.com/artist',
    'javascript:alert(1)',
    'file:///etc/passwd',
    'https://api.soundcloud.com/tracks/1/stream',
    '',
  ]) {
    assert.equal(isValidSoundCloudUrl(bad), false, `должно отклоняться: ${bad}`);
  }
});

test('embed указывает на официальный виджет', () => {
  const url = embedFor('https://soundcloud.com/a/b');
  const u = new URL(url);
  assert.equal(u.hostname, 'w.soundcloud.com');
  assert.equal(u.searchParams.get('url'), 'https://soundcloud.com/a/b');
  assert.equal(u.searchParams.get('auto_play'), 'false');
});

console.log('\nпровайдер SoundCloud на поддельном ответе');

test('searchSoundCloud парсит страницу поиска и не ходит в api-v2', async () => {
  const calls = [];
  const realFetch = globalThis.fetch;
  globalThis.fetch = async (url) => {
    calls.push(String(url));
    if (String(url).includes('soundcloud.com/search')) {
      return new Response(hydration, { status: 200, headers: { 'Content-Type': 'text/html' } });
    }
    return new Response('nope', { status: 500 });
  };
  try {
    const { searchSoundCloud } = await import('../src/soundcloud.js');
    const res = await searchSoundCloud('midnight', { limit: 10 });
    assert.equal(res.tracks.length, 2);
    assert.equal(res.tracks[0].title, 'Midnight Drive');
  } finally {
    globalThis.fetch = realFetch;
  }
  assert.ok(calls.every((u) => !/api-v2|stream|transcode/.test(u)), 'никаких внутренних эндпоинтов');
});

console.log('\nHTTP API');

const child = spawn(process.execPath, ['server.js'], {
  cwd: new URL('..', import.meta.url).pathname,
  env: { ...process.env, PORT: String(PORT), HOST },
  stdio: 'ignore',
});

async function waitForServer(tries = 60) {
  for (let i = 0; i < tries; i++) {
    try {
      const res = await fetch(`http://${HOST}:${PORT}/api/health`);
      if (res.ok) return;
    } catch {
      /* ещё не поднялся */
    }
    await new Promise((r) => setTimeout(r, 100));
  }
  throw new Error('сервер не поднялся');
}

try {
  await waitForServer();
  const base = `http://${HOST}:${PORT}`;

  const health = await (await fetch(`${base}/api/health`)).json();
  assert.equal(health.ok, true);
  assert.ok(Array.isArray(health.providers));

  const noQ = await fetch(`${base}/api/search`);
  assert.equal(noQ.status, 400);
  assert.match((await noQ.json()).error, /q/);

  const badEmbed = await fetch(`${base}/api/embed?url=${encodeURIComponent('https://evil.com/x/y')}`);
  assert.equal(badEmbed.status, 400);

  const index = await fetch(`${base}/`);
  assert.equal(index.status, 200);
  assert.match(await index.text(), /Wave/);

  const traversal = await fetch(`${base}/../package.json`);
  assert.notEqual(traversal.status, 200, 'выйти за пределы public/ нельзя');

  const badAudio = await fetch(`${base}/api/audio?u=${encodeURIComponent('https://evil.com/a.mp3')}`);
  assert.equal(badAudio.status, 403);

  const trackPage = await fetch(`${base}/player?url=${encodeURIComponent('https://soundcloud.com/a/b')}`);
  assert.equal(trackPage.status, 200);
  assert.match(await trackPage.text(), /w\.soundcloud\.com\/player/);

  console.log('  ok  /api/health, /api/search, /api/embed, /, /player, защита путей');
  passed++;
} finally {
  child.kill('SIGTERM');
  await Promise.race([once(child, 'exit'), new Promise((r) => setTimeout(r, 2000))]);
}

console.log(`\n${passed} проверок прошло${process.exitCode ? ' (есть падения)' : ''}\n`);
