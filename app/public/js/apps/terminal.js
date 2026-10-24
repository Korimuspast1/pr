/* Terminal — real bash inside the sandbox. */

import { api } from '../api.js';
import { state, on } from '../store.js';
import { ansiToHTML, esc, shortenPath } from '../util.js';
import { iconHTML } from '../icons.js';

const MAX_LINES = 2500;

export default {
  id: 'terminal',
  name: 'Терминал',
  icon: 'terminal',
  width: 760,
  height: 470,

  mount(body, win) {
    body.innerHTML = `
      <div class="term-wrap">
        <div class="term-screen"></div>
        <div class="statusbar">
          <span class="tc-status">готов</span>
          <span class="grow"></span>
          <span class="tc-cwd"></span>
          <span class="tc-mode"></span>
        </div>
      </div>`;
    const wrap = body.querySelector('.term-wrap');
    const screen = body.querySelector('.term-screen');
    const statusEl = body.querySelector('.tc-status');
    const cwdEl = body.querySelector('.tc-cwd');
    const modeEl = body.querySelector('.tc-mode');

    let cwd = state.cwd;
    let busy = false;
    let history = [...(state.terminalHistory || [])];
    let hIndex = history.length;
    let currentInput = null;

    const promptSegs = () => {
      const root = state.root;
      return `<span class="tc-user">${root ? 'root' : 'user'}@sandbox</span><span class="tc-sep">:</span><span class="tc-path">${esc(shortenPath(cwd))}</span><span class="tc-sign">${root ? '#' : '$'}</span> `;
    };

    const append = (htmlOrText, { ansi = false, cls = '' } = {}) => {
      const d = document.createElement('div');
      d.className = 'term-line ' + cls;
      d.innerHTML = ansi ? ansiToHTML(htmlOrText) : esc(htmlOrText) || '&nbsp;';
      screen.appendChild(d);
      trim();
      return d;
    };

    const trim = () => {
      while (screen.querySelectorAll('.term-line').length > MAX_LINES) {
        screen.firstElementChild?.remove();
      }
    };

    const scrollDown = () => { screen.scrollTop = screen.scrollHeight; };

    const echoCmd = (cmd) => {
      const d = document.createElement('div');
      d.className = 'term-line';
      d.innerHTML = promptSegs() + `<span>${esc(cmd)}</span>`;
      screen.appendChild(d);
      trim();
    };

    const renderInputLine = () => {
      const line = document.createElement('div');
      line.className = 'term-inputline';
      line.innerHTML = `${promptSegs()}<input class="term-caret" spellcheck="false" autocomplete="off" autocapitalize="off" aria-label="команда" />`;
      screen.appendChild(line);
      const input = line.querySelector('input');
      input.addEventListener('keydown', onKey);
      currentInput = input;
      input.focus();
    };

    const freezeInputLine = () => {
      // remove the live input line (the command is echoed separately)
      screen.querySelectorAll('.term-inputline').forEach((n) => n.remove());
      currentInput = null;
    };

    const setStatus = (text) => { statusEl.textContent = text; };

    const syncStatus = () => {
      cwdEl.textContent = shortenPath(cwd);
      modeEl.textContent = state.root ? 'root' : 'user';
      modeEl.style.color = state.root ? 'var(--red)' : '';
      win.setTitle(`${state.root ? 'root' : 'user'}@sandbox: ${shortenPath(cwd)} — bash`);
    };

    async function run(raw) {
      const cmd = raw.trim();
      if (!cmd) return;
      freezeInputLine();
      echoCmd(raw);
      history.push(raw);
      if (history.length > 300) history = history.slice(-300);
      if (state.terminalHistory !== history) {
        state.terminalHistory = history;
        localStorage.setItem('sandbox-desktop:v1', JSON.stringify({ ...state, root: false }));
      }
      hIndex = history.length;

      if (cmd === 'clear' || cmd === 'cls') {
        screen.innerHTML = '';
        setStatus('готов');
        renderInputLine();
        return;
      }

      busy = true;
      setStatus(state.root ? 'выполняется (root)…' : 'выполняется…');
      const t0 = performance.now();
      try {
        const res = await api.exec(raw, { root: state.root });
        if (res.out) {
          for (const line of res.out.replace(/\r\n/g, '\n').split('\n')) {
            const clean = line.includes('\r') ? line.slice(line.lastIndexOf('\r') + 1) : line;
            append(clean, { ansi: true });
          }
        }
        if (res.cwd && res.cwd !== cwd) {
          cwd = res.cwd;
          state.cwd = res.cwd;
        }
        const ms = Math.round(performance.now() - t0);
        setStatus(`${res.code === 0 ? 'успешно' : 'код ' + res.code} · ${ms} мс`);
      } catch (e) {
        append(`\x1b[31m${e.message}\x1b[0m`, { ansi: true });
        setStatus('ошибка');
      } finally {
        busy = false;
        syncStatus();
        renderInputLine();
        scrollDown();
      }
    }

    function onKey(e) {
      const input = e.currentTarget;
      if (e.key === 'Enter') {
        const v = input.value;
        input.value = '';
        run(v);
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        if (!history.length) return;
        hIndex = Math.max(0, hIndex - 1);
        input.value = history[hIndex] || '';
      } else if (e.key === 'ArrowDown') {
        e.preventDefault();
        hIndex = Math.min(history.length, hIndex + 1);
        input.value = history[hIndex] || '';
      } else if (e.key === 'l' && e.ctrlKey) {
        e.preventDefault();
        screen.innerHTML = '';
        renderInputLine();
      } else if (e.key === 'c' && e.ctrlKey && busy) {
        e.preventDefault();
        api.kill('current').catch(() => {});
        setStatus('прервано');
      } else if (e.key === 'Tab') {
        e.preventDefault();
        input.value = input.value; // autocomplete is intentionally out of scope
      }
    }

    screen.addEventListener('click', () => currentInput?.focus());
    wrap.addEventListener('pointerdown', () => setTimeout(() => currentInput?.focus(), 0));

    // banner + prompt
    const root = state.root;
    append(`\x1b[32m\x1b[1msandboxOS\x1b[0m \x1b[90m— bash в контейнере, доступен настоящий root\x1b[0m`, { ansi: true });
    append(`\x1b[90mвключи root mode в панели быстрых настроек, чтобы команды шли через sudo -n\x1b[0m`, { ansi: true });
    append('');
    syncStatus();
    renderInputLine();
    win.body.style.userSelect = 'text';

    const offRoot = on('root', () => {
      syncStatus();
      if (!busy) {
        freezeInputLine();
        renderInputLine();
        scrollDown();
      }
    });

    win.spec.onClose = () => offRoot();
    setTimeout(() => currentInput?.focus(), 60);
  },
};
