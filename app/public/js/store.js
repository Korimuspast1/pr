/* Tiny reactive store + persistent preferences. */

const KEY = 'sandbox-desktop:v1';

const defaults = {
  root: false,
  accent: '#79c0ff',
  wallpaper: 'aurora',
  fontScale: 1,
  clock24: true,
  showSeconds: false,
  confirmKill: true,
  cwd: '/home/user/pr',
  terminalHistory: [],
  bookmarks: [
    { title: 'GitHub', url: 'https://github.com/Korimuspast1/pr' },
    { title: 'sandbox API', url: '/api/stats' },
  ],
};

function load() {
  try {
    const raw = JSON.parse(localStorage.getItem(KEY) || '{}');
    return { ...defaults, ...raw, root: false };
  } catch {
    return { ...defaults, root: false };
  }
}

export const state = load();
const listeners = new Map();

export function on(key, fn) {
  if (!listeners.has(key)) listeners.set(key, new Set());
  listeners.get(key).add(fn);
  return () => listeners.get(key).delete(fn);
}

export function set(key, value) {
  state[key] = value;
  const persist = { ...state };
  delete persist.root;
  try {
    localStorage.setItem(KEY, JSON.stringify(persist));
  } catch {}
  for (const fn of listeners.get(key) || []) fn(value, key);
  for (const fn of listeners.get('*') || []) fn(value, key);
}

export function applyTheme() {
  const root = document.documentElement;
  root.style.setProperty('--accent', state.accent);
  root.style.setProperty('--font-scale', state.fontScale);
  root.dataset.wallpaper = state.wallpaper;
}

export const WALLPAPERS = [
  { id: 'aurora', name: 'Aurora', css: 'radial-gradient(1100px 760px at 20% 12%, #1d4ed8 0%, transparent 62%), radial-gradient(900px 700px at 84% 88%, #0f766e 0%, transparent 60%), linear-gradient(160deg, #070d15 0%, #0f1e2e 52%, #102b36 100%)' },
  { id: 'sunset', name: 'Sunset', css: 'radial-gradient(1000px 700px at 78% 18%, #b45309 0%, transparent 58%), radial-gradient(900px 700px at 16% 82%, #7c3aed 0%, transparent 60%), linear-gradient(160deg, #12080f 0%, #221223 55%, #2a1622 100%)' },
  { id: 'graphite', name: 'Graphite', css: 'radial-gradient(900px 700px at 30% 20%, #1f2937 0%, transparent 60%), radial-gradient(800px 600px at 80% 80%, #111827 0%, transparent 60%), linear-gradient(160deg, #080a0e 0%, #10141b 60%, #151a22 100%)' },
  { id: 'mint', name: 'Mint', css: 'radial-gradient(900px 700px at 24% 16%, #065f46 0%, transparent 60%), radial-gradient(900px 700px at 82% 86%, #0e7490 0%, transparent 58%), linear-gradient(160deg, #04120f 0%, #08201d 55%, #0a2530 100%)' },
];
