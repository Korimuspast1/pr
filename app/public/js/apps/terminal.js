/* Terminal — a real PTY (bash -i) rendered by xterm.js.
 * Full-screen programs work: top, htop, vim, nano, less, python REPL, mc…
 */

import { api } from '../api.js';
import { state, on } from '../store.js';
import { esc } from '../util.js';

let xtermLoading = null;

async function loadXtermViaApi() {
  const asset = await api.asset('xterm');
  if (!asset || !asset.code) throw new Error('ассет xterm пуст');
  // eslint-disable-next-line no-new-func
  new Function(asset.code)();
  if (!window.Terminal) throw new Error('xterm не инициализировался');
}

function loadXterm() {
  if (window.Terminal) return Promise.resolve();
  if (xtermLoading) return xtermLoading;
  xtermLoading = new Promise((resolve, reject) => {
    const viaApi = () => loadXtermViaApi().then(resolve).catch(reject);
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = '/vendor/xterm/xterm.css';
    document.head.appendChild(link);
    const s = document.createElement('script');
    s.src = '/vendor/xterm/xterm.js';
    s.async = true;
    const fail = (why) => reject(new Error(why));
    const guard = setTimeout(() => { clearTimeout(guard); viaApi(); }, 4000);
    s.onload = () => {
      clearTimeout(guard);
      if (window.Terminal) resolve();
      else viaApi();
    };
    s.onerror = () => { clearTimeout(guard); viaApi(); };
    document.head.appendChild(s);
  });
  return xtermLoading;
}

/**
 * Резервный терминал: тот же настоящий PTY, но без xterm.js —
 * вывод чистится от ANSI и печатается построчно. `cd`, root и любые
 * команды работают, потому что сессия — живой bash, а не отдельные exec.
 */
function mountFallback(host, win, setStatus) {
  host.innerHTML = `
    <div class="term-screen" style="height:100%;overflow:auto;padding:10px 12px;font-family:var(--mono);font-size:.86em;line-height:1.55;user-select:text;white-space:pre-wrap;word-break:break-word"></div>
    <div style="display:flex;gap:6px;padding:0 12px 10px;align-items:center;font-family:var(--mono);font-size:.86em">
      <span style="color:var(--green);font-weight:700">${state.root ? 'root' : 'user'}@sandbox</span><span style="color:var(--dim)">$</span>
      <input class="fb-input" spellcheck="false" autocomplete="off" style="flex:1;background:none;border:0;outline:none;color:var(--text);font:inherit" />
    </div>`;
  const screen = host.querySelector('.term-screen');
  const input = host.querySelector('.fb-input');
  setStatus('резервный режим · PTY без xterm.js');

  let session = null;
  let offset = 0;
  let alive = true;
  let pending = '';
  const decoder = new TextDecoder('utf-8');

  const ANSI_RE = /\x1b\[[0-9;?]*[ -/]*[@-~]|\x1b\][^\x07]*\x07|\x1b[()][A-Z0-9]/g;

  const flush = (text, force = false) => {
    pending += text;
    const parts = pending.split('\n');
    if (!force) pending = parts.pop();
    else pending = '';
    for (const raw of parts) {
      const clean = raw.replace(ANSI_RE, '').replace(/\r/g, '');
      if (!clean && !force) continue;
      const d = document.createElement('div');
      d.textContent = clean;
      screen.appendChild(d);
    }
    if (force && pending) {
      const d = document.createElement('div');
      d.textContent = pending.replace(ANSI_RE, '');
      screen.appendChild(d);
      pending = '';
    }
    while (screen.children.length > 2000) screen.firstElementChild.remove();
    screen.scrollTop = screen.scrollHeight;
  };

  const latin1ToBytes = (str) => {
    const u = new Uint8Array(str.length);
    for (let i = 0; i < str.length; i++) u[i] = str.charCodeAt(i) & 0xff;
    return u;
  };
  const utf8ToLatin1 = (str) => {
    const bytes = new TextEncoder().encode(str);
    let out = '';
    for (const b of bytes) out += String.fromCharCode(b);
    return out;
  };

  async function loop() {
    while (alive && session) {
      try {
        const r = await api.ptyRead(session.id, offset, 900);
        if (!alive) return;
        if (r.data) flush(decoder.decode(latin1ToBytes(r.data), { stream: true }));
        if (typeof r.offset === 'number') offset = r.offset;
        if (r.dead) { setStatus('сессия завершена'); return; }
      } catch {
        await new Promise((r) => setTimeout(r, 800));
      }
    }
  }

  (async () => {
    try {
      session = await api.ptyOpen({ root: state.root, cols: 100, rows: 40 });
      win.setTitle(`${state.root ? 'root' : 'user'}@sandbox — терминал (резервный)`);
      setStatus('готово · резервный режим');
      loop();
    } catch (e) {
      flush(`не удалось открыть PTY: ${e.message}\n`, true);
      setStatus('ошибка сессии');
    }
  })();

  input.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      const cmd = input.value;
      input.value = '';
      flush(`\n${state.root ? 'root' : 'user'}@sandbox$ ${cmd}\n`, true);
      if (session) api.ptyWrite(session.id, utf8ToLatin1(cmd + '\r')).catch(() => {});
    } else if (e.key === 'l' && e.ctrlKey) {
      e.preventDefault();
      screen.innerHTML = '';
    } else if (e.key === 'c' && e.ctrlKey) {
      e.preventDefault();
      if (session) api.ptyWrite(session.id, '\u0003').catch(() => {});
    }
  });

  body_focus(input);
  win.spec.onClose = () => {
    alive = false;
    if (session) api.ptyClose(session.id).catch(() => {});
  };
}

function body_focus(input) {
  setTimeout(() => input.focus(), 60);
}

const THEME = {
  background: '#0b1016',
  foreground: '#d6dee8',
  cursor: '#7ee787',
  cursorAccent: '#0b1016',
  selectionBackground: 'rgba(121,192,255,.28)',
  black: '#1b2229', red: '#ff7b72', green: '#7ee787', yellow: '#e3b341',
  blue: '#79c0ff', magenta: '#d2a8ff', cyan: '#56d4dd', white: '#e6edf3',
  brightBlack: '#6e7681', brightRed: '#ffa198', brightGreen: '#a5f3a0',
  brightYellow: '#f2cc60', brightBlue: '#a5d6ff', brightMagenta: '#e2c5ff',
  brightCyan: '#8ce8ee', brightWhite: '#ffffff',
};

export const rootTerminal = {
  id: 'terminal-root',
  name: 'Терминал (root)',
  icon: 'root',
  width: 860,
  height: 540,
  dock: false,
  mount(body, win) {
    win.spec.args = { ...(win.spec.args || {}), root: true };
    return terminalApp.mount(body, win);
  },
};

const terminalApp = {
  id: 'terminal',
  name: 'Терминал',
  icon: 'terminal',
  width: 860,
  height: 540,

  async mount(body, win) {
    body.innerHTML = `
      <div class="term-wrap">
        <div class="term-host"></div>
        <div class="statusbar">
          <span class="tc-status">соединение…</span>
          <span class="grow"></span>
          <span class="tc-size">—</span>
          <span class="tc-mode">user</span>
        </div>
      </div>`;
    const host = body.querySelector('.term-host');
    const statusEl = body.querySelector('.tc-status');
    const sizeEl = body.querySelector('.tc-size');
    const modeEl = body.querySelector('.tc-mode');

    let term = null;
    let session = null;
    let alive = true;
    let offset = 0;
    const TD = window.TextDecoder || TextDecoder;
    let decoder = new TD('utf-8');
    let resizeTimer = null;
    let rootSession = win.spec.args?.root ?? state.root;

    const setStatus = (t) => { statusEl.textContent = t; };
    const paintMode = () => {
      modeEl.textContent = rootSession ? 'root (sudo)' : 'user';
      modeEl.style.color = rootSession ? 'var(--red)' : '';
    };

    try {
      await loadXterm();
    } catch (e) {
      // Fallback: простой построчный режим поверх /api/exec
      mountFallback(host, win, setStatus);
      return;
    }
    if (!alive) return;

    const measure = () => {
      const w = host.clientWidth || 800;
      const h = host.clientHeight || 400;
      const cw = 8.4;
      const ch = 17.5;
      return {
        cols: Math.max(20, Math.floor((w - 16) / cw)),
        rows: Math.max(6, Math.floor((h - 12) / ch)),
      };
    };

    const { cols, rows } = measure();

    term = new window.Terminal({
      theme: THEME,
      fontFamily: '"JetBrains Mono", "DejaVu Sans Mono", ui-monospace, monospace',
      fontSize: 13,
      lineHeight: 1.25,
      cursorBlink: true,
      cursorStyle: 'bar',
      scrollback: 5000,
      allowProposedApi: true,
      convertEol: false,
      macOptionIsMeta: true,
    });
    term.open(host);
    term.focus();
    sizeEl.textContent = `${cols}×${rows}`;

    // ── session open ───────────────────────────────────────────────────
    async function openSession() {
      try {
        setStatus('открываю сессию…');
        const s = await api.ptyOpen({ root: rootSession, cols, rows });
        if (!alive) { api.ptyClose(s.id).catch(() => {}); return; }
        session = s;
        offset = 0;
        decoder = new TD('utf-8');
        paintMode();
        setStatus('готово');
        win.setTitle(`${rootSession ? 'root' : 'user'}@sandbox — терминал`);
        pump();
      } catch (e) {
        term.write(`\r\n\x1b[31mне удалось открыть PTY: ${e.message}\x1b[0m\r\n`);
        setStatus('ошибка сессии');
      }
    }

    const latin1ToBytes = (str) => {
      const u = new Uint8Array(str.length);
      for (let i = 0; i < str.length; i++) u[i] = str.charCodeAt(i) & 0xff;
      return u;
    };
    const utf8ToLatin1 = (str) => {
      const bytes = new TextEncoder().encode(str);
      let out = '';
      for (const b of bytes) out += String.fromCharCode(b);
      return out;
    };

    // ── output pump (long-poll) ────────────────────────────────────────
    let failStreak = 0;
    async function pump() {
      while (alive && session) {
        if (document.hidden) { await new Promise((r) => setTimeout(r, 1500)); continue; }
        const focused = win === window.desktop?.wm?.focused;
        try {
          const r = await api.ptyRead(session.id, offset, focused ? 1500 : 600);
          failStreak = 0;
          if (!alive || !session) return;
          if (r.error) { term.write(`\r\n\x1b[31m${r.error}\x1b[0m\r\n`); setStatus('сессия потеряна'); return; }
          if (r.data) {
            term.write(decoder.decode(latin1ToBytes(r.data), { stream: true }));
          }
          if (r.data === '' && !focused) await new Promise((res) => setTimeout(res, 700));
          if (typeof r.offset === 'number') offset = r.offset;
          if (r.dead) {
            setStatus('сессия завершена');
            return;
          }
        } catch (e) {
          if (!alive) return;
          failStreak++;
          setStatus(failStreak > 2 ? 'связь нестабильна, повторяю…' : 'переподключение…');
          await new Promise((res) => setTimeout(res, Math.min(4000, 500 * failStreak)));
          if (failStreak === 5) {
            win.notify('Терминал: связь с сервером нестабильна',
              'Превью-прокси рвёт длинные запросы. Пробую снова автоматически.', 'warn');
          }
        }
      }
    }

    // ── input ──────────────────────────────────────────────────────────
    let inputBuf = '';
    let inputTimer = null;
    const flushInput = () => {
      if (!inputBuf || !session) return;
      const chunk = inputBuf;
      inputBuf = '';
      api.ptyWrite(session.id, utf8ToLatin1(chunk)).catch(() => {});
    };
    term.onData((data) => {
      inputBuf += data;
      clearTimeout(inputTimer);
      if (data.length > 32) flushInput();
      else inputTimer = setTimeout(flushInput, 12);
    });

    term.onResize(({ cols: c, rows: r }) => {
      sizeEl.textContent = `${c}×${r}`;
      if (!session) return;
      clearTimeout(resizeTimer);
      resizeTimer = setTimeout(() => api.ptyResize(session.id, c, r).catch(() => {}), 220);
    });
    term.onTitleChange((t) => { if (t) win.setTitle(t + ' — терминал'); });

    // fit on window resize
    const refit = () => {
      const m = measure();
      if (m.cols !== term.cols || m.rows !== term.rows) term.resize(m.cols, m.rows);
    };
    const ro = new ResizeObserver(refit);
    ro.observe(host);

    // restart session when root mode is toggled
    const offRoot = on('root', async () => {
      if (!session) return;
      const wantRoot = state.root;
      if (wantRoot === rootSession) return;
      if (win.spec.args?.root != null) {
        win.notify('Это root-сессия', 'Открой обычный «Терминал», чтобы работать как user', 'info');
        return;
      }
      if (!(await window.desktop.wm.confirm(
        wantRoot ? 'Перезапустить сессию с правами root?' : 'Перезапустить сессию как user?',
        { ok: 'Перезапустить' }
      ))) return;
      const old = session;
      session = null;
      api.ptyClose(old.id).catch(() => {});
      rootSession = wantRoot;
      term.write('\r\n\x1b[90m— новая сессия —\x1b[0m\r\n');
      openSession();
    });

    win.spec.onClose = () => {
      alive = false;
      offRoot();
      ro.disconnect();
      if (session) api.ptyClose(session.id).catch(() => {});
    };

    body.addEventListener('pointerdown', () => term.focus());
    term.attachCustomKeyEventHandler?.((e) => {
      // let the desktop handle Ctrl+Alt shortcuts, pass everything else to the PTY
      if (e.ctrlKey && e.altKey) return false;
      return true;
    });

    openSession();
  },
};

export default terminalApp;
