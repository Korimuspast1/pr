/* Window manager: draggable / resizable / stackable windows, toasts, modal dialogs. */

import { iconHTML, icons } from './icons.js';

const PANEL_H = 34;
const DOCK_RESERVE = 96;

let zIndex = 100;
let seq = 0;

export class WM {
  constructor({ layer, toasts }) {
    this.layer = layer;
    this.toasts = toasts;
    this.windows = new Map();
    this.focused = null;
    this.listeners = new Set();
    this._bound = false;
    this._bindGlobal();
  }

  onChange(fn) {
    this.listeners.add(fn);
    return () => this.listeners.delete(fn);
  }

  emit() {
    for (const fn of this.listeners) fn(this);
  }

  get list() {
    return [...this.windows.values()];
  }

  has(appId) {
    return this.list.some((w) => w.appId === appId);
  }

  byApp(appId) {
    return this.list.find((w) => w.appId === appId);
  }

  open(spec) {
    const existing = spec.singleton !== false ? this.byApp(spec.appId) : null;
    if (existing) {
      this.restore(existing);
      this.focus(existing);
      return existing;
    }

    const vw = window.innerWidth;
    const vh = window.innerHeight;
    const width = Math.min(spec.width || 820, vw - 40);
    const height = Math.min(spec.height || 540, vh - PANEL_H - DOCK_RESERVE);
    const cascade = this.list.length % 7;
    const x = Math.max(16, Math.min(spec.x ?? 120 + cascade * 28, vw - width - 16));
    const y = Math.max(PANEL_H + 12, Math.min(spec.y ?? 70 + cascade * 26, vh - DOCK_RESERVE - 60));

    const el = document.createElement('section');
    el.className = 'window';
    el.dataset.app = spec.appId;
    el.style.left = x + 'px';
    el.style.top = y + 'px';
    el.style.width = width + 'px';
    el.style.height = height + 'px';
    el.style.zIndex = ++zIndex;

    el.innerHTML = `
      <header class="win-head">
        <span class="win-icon">${iconHTML(spec.icon || 'file', 15)}</span>
        <span class="win-title"></span>
        <span class="win-btns">
          <button class="win-btn" data-act="min" title="Свернуть">${icons.minimize}</button>
          <button class="win-btn" data-act="max" title="Развернуть">${icons.maximize}</button>
          <button class="win-btn danger" data-act="close" title="Закрыть">${icons.close}</button>
        </span>
      </header>
      <div class="win-body"></div>
      <div class="rz rz-e"></div><div class="rz rz-s"></div><div class="rz rz-se"></div>
    `;

    const win = {
      id: 'w' + ++seq,
      appId: spec.appId,
      el,
      body: el.querySelector('.win-body'),
      titleEl: el.querySelector('.win-title'),
      spec,
      minimized: false,
      maximized: false,
      prevRect: null,
      setTitle: (t) => {
        win.title = t;
        win.titleEl.textContent = t;
        win.titleEl.title = t;
        this.emit();
      },
      close: () => this.close(win),
      focus: () => this.focus(win),
      notify: (title, body, kind) => this.notify(title, body, kind),
    };
    win.title = spec.title || 'Окно';
    win.titleEl.textContent = win.title;

    this.layer.appendChild(el);
    this.windows.set(win.id, win);

    el.querySelector('[data-act="min"]').onclick = (e) => { e.stopPropagation(); this.minimize(win); };
    el.querySelector('[data-act="max"]').onclick = (e) => { e.stopPropagation(); this.toggleMax(win); };
    el.querySelector('[data-act="close"]').onclick = (e) => { e.stopPropagation(); this.close(win); };

    el.addEventListener('pointerdown', () => this.focus(win), true);
    this._makeDraggable(win);
    this._makeResizable(win);

    this.focus(win);
    try {
      spec.mount?.(win.body, win);
    } catch (e) {
      win.body.innerHTML = `<div class="app-error">Ошибка приложения: ${String(e.message || e)}</div>`;
    }
    this.emit();
    return win;
  }

  focus(win) {
    if (!win || win.minimized) return;
    if (this.focused === win) {
      win.el.style.zIndex = ++zIndex;
      return;
    }
    this.focused = win;
    for (const w of this.windows.values()) w.el.classList.toggle('focused', w === win);
    win.el.style.zIndex = ++zIndex;
    this.emit();
  }

  minimize(win) {
    win.minimized = true;
    win.el.classList.add('minimized');
    if (this.focused === win) {
      this.focused = null;
      const next = this.list.filter((w) => !w.minimized).pop();
      if (next) this.focus(next);
    }
    this.emit();
  }

  restore(win) {
    if (!win.minimized) return;
    win.minimized = false;
    win.el.classList.remove('minimized');
    this.emit();
  }

  toggleMax(win) {
    if (win.maximized) {
      win.maximized = false;
      win.el.classList.remove('maximized');
      if (win.prevRect) Object.assign(win.el.style, win.prevRect);
    } else {
      win.prevRect = {
        left: win.el.style.left,
        top: win.el.style.top,
        width: win.el.style.width,
        height: win.el.style.height,
      };
      win.maximized = true;
      win.el.classList.add('maximized');
      win.el.style.left = '';
      win.el.style.top = '';
      win.el.style.width = '';
      win.el.style.height = '';
    }
    win.spec.onResize?.(win.body, win);
  }

  close(win) {
    if (!win) return;
    win.spec.onClose?.(win.body, win);
    win.el.classList.add('closing');
    setTimeout(() => win.el.remove(), 130);
    this.windows.delete(win.id);
    if (this.focused === win) {
      this.focused = null;
      const next = this.list.filter((w) => !w.minimized).pop();
      if (next) this.focus(next);
    }
    this.emit();
  }

  closeAll() {
    for (const w of this.list) this.close(w);
  }

  notify(title, body = '', kind = 'info', timeout = 5200) {
    const el = document.createElement('div');
    el.className = 'toast toast-' + kind;
    el.innerHTML = `
      <span class="toast-ico">${iconHTML(kind === 'error' ? 'warning' : kind === 'ok' ? 'check' : 'info', 16)}</span>
      <div class="toast-txt"><b></b><span></span></div>
    `;
    el.querySelector('b').textContent = title;
    el.querySelector('span:last-child').textContent = body;
    this.toasts.appendChild(el);
    while (this.toasts.children.length > 4) this.toasts.firstElementChild.remove();
    const kill = () => {
      el.classList.add('out');
      setTimeout(() => el.remove(), 220);
    };
    el.onclick = kill;
    if (timeout) setTimeout(kill, timeout);
    requestAnimationFrame(() => el.classList.add('in'));
    return el;
  }

  confirm(message, { title = 'Подтверждение', ok = 'ОК', cancel = 'Отмена', danger = false } = {}) {
    return new Promise((resolve) => {
      const back = document.createElement('div');
      back.className = 'modal-back';
      back.innerHTML = `
        <div class="modal">
          <h3></h3>
          <p></p>
          <div class="modal-actions">
            <button class="btn ghost" data-no></button>
            <button class="btn${danger ? ' danger' : ' primary'}" data-yes></button>
          </div>
        </div>`;
      back.querySelector('h3').textContent = title;
      back.querySelector('p').textContent = message;
      back.querySelector('[data-no]').textContent = cancel;
      back.querySelector('[data-yes]').textContent = ok;
      const done = (v) => {
        back.classList.add('out');
        setTimeout(() => back.remove(), 160);
        resolve(v);
      };
      back.querySelector('[data-no]').onclick = () => done(false);
      back.querySelector('[data-yes]').onclick = () => done(true);
      back.onclick = (e) => { if (e.target === back) done(false); };
      document.body.appendChild(back);
      requestAnimationFrame(() => back.classList.add('in'));
    });
  }

  /* ── dragging ────────────────────────────────────────────────────── */
  _makeDraggable(win) {
    const head = win.el.querySelector('.win-head');
    head.addEventListener('dblclick', (e) => {
      if (e.target.closest('.win-btn')) return;
      this.toggleMax(win);
    });

    head.addEventListener('pointerdown', (e) => {
      if (e.target.closest('.win-btn')) return;
      if (e.button !== 0) return;
      if (win.maximized) return;
      const startX = e.clientX;
      const startY = e.clientY;
      const rect = win.el.getBoundingClientRect();
      const offX = startX - rect.left;
      const offY = startY - rect.top;
      win.el.classList.add('dragging');
      head.setPointerCapture?.(e.pointerId);

      const move = (ev) => {
        const maxX = window.innerWidth - 80;
        const maxY = window.innerHeight - 60;
        const left = Math.max(-rect.width + 120, Math.min(ev.clientX - offX, maxX));
        const top = Math.max(PANEL_H, Math.min(ev.clientY - offY, maxY));
        win.el.style.left = left + 'px';
        win.el.style.top = top + 'px';
      };
      const up = () => {
        win.el.classList.remove('dragging');
        head.removeEventListener('pointermove', move);
        head.removeEventListener('pointerup', up);
        head.removeEventListener('pointercancel', up);
      };
      head.addEventListener('pointermove', move);
      head.addEventListener('pointerup', up);
      head.addEventListener('pointercancel', up);
    });
  }

  _makeResizable(win) {
    for (const handle of win.el.querySelectorAll('.rz')) {
      handle.addEventListener('pointerdown', (e) => {
        if (win.maximized) return;
        e.preventDefault();
        e.stopPropagation();
        const dir = handle.className.match(/rz-(e|s|se)/)[1];
        const startX = e.clientX;
        const startY = e.clientY;
        const startW = win.el.offsetWidth;
        const startH = win.el.offsetHeight;
        handle.setPointerCapture?.(e.pointerId);
        win.el.classList.add('resizing');

        const move = (ev) => {
          if (dir.includes('e')) win.el.style.width = Math.max(320, startW + ev.clientX - startX) + 'px';
          if (dir.includes('s')) win.el.style.height = Math.max(200, startH + ev.clientY - startY) + 'px';
        };
        const up = () => {
          win.el.classList.remove('resizing');
          handle.removeEventListener('pointermove', move);
          handle.removeEventListener('pointerup', up);
          win.spec.onResize?.(win.body, win);
        };
        handle.addEventListener('pointermove', move);
        handle.addEventListener('pointerup', up);
      });
    }
  }

  _bindGlobal() {
    if (this._bound) return;
    this._bound = true;
    window.addEventListener('resize', () => {
      for (const w of this.list) {
        if (w.maximized) continue;
        const r = w.el.getBoundingClientRect();
        if (r.left > window.innerWidth - 120) w.el.style.left = Math.max(8, window.innerWidth - r.width - 24) + 'px';
        if (r.top > window.innerHeight - 80) w.el.style.top = Math.max(PANEL_H, window.innerHeight - r.height - DOCK_RESERVE) + 'px';
        w.spec.onResize?.(w.body, w);
      }
    });
  }
}
