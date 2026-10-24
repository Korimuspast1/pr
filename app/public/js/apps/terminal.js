/* Terminal — a real PTY (bash -i) rendered by xterm.js.
 * Full-screen programs work: top, htop, vim, nano, less, python REPL, mc…
 */

import { api } from '../api.js';
import { state, on } from '../store.js';
import { esc } from '../util.js';

let xtermLoading = null;

function loadXterm() {
  if (window.Terminal) return Promise.resolve();
  if (xtermLoading) return xtermLoading;
  xtermLoading = new Promise((resolve, reject) => {
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = '/vendor/xterm/xterm.css';
    document.head.appendChild(link);
    const s = document.createElement('script');
    s.src = '/vendor/xterm/xterm.js';
    s.async = true;
    s.onload = () => (window.Terminal ? resolve() : reject(new Error('xterm не инициализировался')));
    s.onerror = () => reject(new Error('не удалось загрузить xterm.js'));
    document.head.appendChild(s);
  });
  return xtermLoading;
}

/** Простейший резервный терминал, если xterm.js не загрузился. */
function mountFallback(host, win, setStatus) {
  host.innerHTML = `
    <div class="term-screen" style="height:100%;overflow:auto;padding:10px 12px;font-family:var(--mono);font-size:.86em;line-height:1.6;user-select:text"></div>
    <div class="term-inputline" style="padding:0 12px 10px"><span class="tc-user">user@sandbox</span><span class="tc-sep">:</span><span class="tc-path">~</span><span class="tc-sign">$</span><input spellcheck="false" /></div>`;
  const screen = host.querySelector('.term-screen');
  const input = host.querySelector('input');
  setStatus('резервный режим (без xterm.js)');
  const write = (t) => {
    const d = document.createElement('div');
    d.textContent = t;
    d.style.whiteSpace = 'pre-wrap';
    screen.appendChild(d);
    screen.scrollTop = screen.scrollHeight;
  };
  write('xterm.js недоступен — работаю в простом режиме.');
  input.addEventListener('keydown', async (e) => {
    if (e.key !== 'Enter') return;
    const cmd = input.value;
    input.value = '';
    write('$ ' + cmd);
    const r = await api.exec(cmd, { root: state.root });
    write(r.out || '(нет вывода)');
  });
  setTimeout(() => input.focus(), 50);
  win.spec.onClose = () => {};
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
    let decoder = new TextDecoder('utf-8');
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
        decoder = new TextDecoder('utf-8');
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
    async function pump() {
      while (alive && session) {
        try {
          const r = await api.ptyRead(session.id, offset);
          if (!alive || !session) return;
          if (r.error) { term.write(`\r\n\x1b[31m${r.error}\x1b[0m\r\n`); setStatus('сессия потеряна'); return; }
          if (r.data) {
            term.write(decoder.decode(latin1ToBytes(r.data), { stream: true }));
          }
          if (typeof r.offset === 'number') offset = r.offset;
          if (r.dead) {
            setStatus('сессия завершена');
            return;
          }
        } catch (e) {
          if (!alive) return;
          setStatus('переподключение…');
          await new Promise((res) => setTimeout(res, 700));
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
