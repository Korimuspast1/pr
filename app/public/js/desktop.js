/* Desktop shell: panel, dock, launcher, popovers, notifications, app registry. */

import { WM } from './wm.js';
import { api } from './api.js';
import { state, set, on, applyTheme, WALLPAPERS } from './store.js';
import { icons, iconHTML } from './icons.js';
import { esc, humanSize } from './util.js';

const ACCENTS = ['#79c0ff', '#7ee787', '#d2a8ff', '#e3b341', '#ff7b72', '#56d4dd', '#f778ba', '#a5d6ff'];

export function createDesktop(apps) {
  const registry = new Map(apps.map((a) => [a.id, a]));

  /* ── core ──────────────────────────────────────────────────────────── */
  const wm = new WM({
    layer: document.getElementById('windows'),
    toasts: document.getElementById('toasts'),
  });

  const desktop = {
    wm,
    apps,
    registry,
    launch,
    execInTerminal: (cmd) => {
      const termWin = wm.byApp('terminal');
      termWin?.body.dispatchEvent(new CustomEvent('desktop:run', { detail: { cmd } }));
    },
    notify: (t, b, k) => wm.notify(t, b, k),
  };
  window.desktop = desktop;

  function launch(appId, args = {}) {
    const app = registry.get(appId);
    if (!app) return wm.notify('Нет приложения', `«${appId}» не зарегистрировано`, 'error');
    const existing = app.singleton === false ? null : wm.byApp(appId);
    if (existing) {
      wm.restore(existing);
      wm.focus(existing);
      existing.spec.onArgs?.(args, existing);
      return existing;
    }
    return wm.open({
      appId,
      title: app.name,
      icon: app.icon,
      width: app.width,
      height: app.height,
      args,
      mount: app.mount,
    });
  }

  /* ── static icons ──────────────────────────────────────────────────── */
  const setIco = (id, name, size = 14) => {
    const el = document.getElementById(id);
    if (el) el.innerHTML = icons[name] || '';
    if (el) { el.style.width = size + 'px'; el.style.height = size + 'px'; }
  };
  setIco('appsIco', 'launcher', 15);
  setIco('rootIco', 'root', 13);
  setIco('cpuIco', 'cpu', 14);
  setIco('netIco', 'network', 14);
  setIco('volumeIco', 'volume', 14);
  setIco('batteryIco', 'battery', 14);
  setIco('userIco', 'user', 14);
  setIco('searchIco', 'search', 17);

  /* ── dock ──────────────────────────────────────────────────────────── */
  const dock = document.getElementById('dock');
  const dockApps = apps.filter((a) => a.dock !== false);
  for (const app of dockApps) {
    const b = document.createElement('button');
    b.className = 'dock-item';
    b.dataset.app = app.id;
    b.innerHTML = `${icons[app.icon] || icons.file}<span class="dot"></span><span class="tip">${esc(app.name)}</span>`;
    b.onclick = () => {
      const open = wm.byApp(app.id);
      if (open) {
        if (open === wm.focused && !open.minimized) wm.minimize(open);
        else { wm.restore(open); wm.focus(open); }
      } else launch(app.id);
    };
    dock.appendChild(b);
  }
  const sep = document.createElement('div');
  sep.className = 'dock-sep';
  dock.appendChild(sep);
  const trash = document.createElement('button');
  trash.className = 'dock-item';
  trash.style.color = 'var(--dim)';
  trash.innerHTML = `${icons.trash}<span class="tip">Мусор — нет (сандбокс)</span>`;
  trash.onclick = () => wm.notify('Корзина не нужна', 'Файлы удаляются сразу, rm -rf. Будь осторожен.', 'info');
  dock.appendChild(trash);

  const syncDock = () => {
    for (const b of dock.querySelectorAll('.dock-item[data-app]')) {
      const w = wm.byApp(b.dataset.app);
      b.classList.toggle('open', !!w);
      b.classList.toggle('active', !!w && w === wm.focused && !w.minimized);
    }
  };

  /* ── panel tasks ───────────────────────────────────────────────────── */
  const tasks = document.getElementById('tasks');
  const syncTasks = () => {
    tasks.innerHTML = '';
    for (const w of wm.list) {
      if (w.minimized) continue;
      const app = registry.get(w.appId);
      const b = document.createElement('button');
      b.className = 'task' + (w === wm.focused ? ' active' : '');
      b.innerHTML = `${iconHTML(app?.icon || 'file', 13)}<span class="t">${esc(w.title)}</span>`;
      b.onclick = () => { wm.restore(w); wm.focus(w); };
      tasks.appendChild(b);
    }
  };

  wm.onChange(() => { syncTasks(); syncDock(); });

  /* ── launcher ──────────────────────────────────────────────────────── */
  const launcher = document.getElementById('launcher');
  const grid = document.getElementById('launcherGrid');
  const launcherInput = document.getElementById('launcherInput');
  let selIndex = 0;

  const renderLauncher = (filter = '') => {
    const q = filter.trim().toLowerCase();
    const list = apps.filter((a) => !q || a.name.toLowerCase().includes(q) || a.id.includes(q));
    grid.innerHTML = '';
    list.forEach((app, i) => {
      const b = document.createElement('button');
      b.className = 'launcher-tile' + (i === selIndex ? ' sel' : '');
      b.innerHTML = `<span class="tile-ico">${icons[app.icon] || icons.file}</span><span class="name">${esc(app.name)}</span>`;
      b.onclick = () => { closeLauncher(); launch(app.id); };
      b.onmouseenter = () => { selIndex = i; markSel(); };
      grid.appendChild(b);
    });
    grid.dataset.count = String(list.length);
    return list;
  };
  const markSel = () => {
    grid.querySelectorAll('.launcher-tile').forEach((n, i) => n.classList.toggle('sel', i === selIndex));
    grid.querySelector('.launcher-tile.sel')?.scrollIntoView({ block: 'nearest' });
  };
  const openLauncher = () => {
    launcher.classList.remove('hidden');
    document.getElementById('appsBtn').classList.add('active');
    selIndex = 0;
    launcherInput.value = '';
    renderLauncher('');
    setTimeout(() => launcherInput.focus(), 30);
  };
  const closeLauncher = () => {
    launcher.classList.add('hidden');
    document.getElementById('appsBtn').classList.remove('active');
  };
  const toggleLauncher = () => (launcher.classList.contains('hidden') ? openLauncher() : closeLauncher());

  document.getElementById('appsBtn').onclick = (e) => { e.stopPropagation(); toggleLauncher(); };
  launcher.onclick = (e) => { if (e.target === launcher) closeLauncher(); };
  launcherInput.oninput = () => { selIndex = 0; renderLauncher(launcherInput.value); };
  launcherInput.onkeydown = (e) => {
    const count = Number(grid.dataset.count || 0);
    if (e.key === 'Escape') { closeLauncher(); }
    else if (e.key === 'ArrowDown') { e.preventDefault(); selIndex = Math.min(count - 1, selIndex + 2); markSel(); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); selIndex = Math.max(0, selIndex - 2); markSel(); }
    else if (e.key === 'ArrowRight') { e.preventDefault(); selIndex = Math.min(count - 1, selIndex + 1); markSel(); }
    else if (e.key === 'ArrowLeft') { e.preventDefault(); selIndex = Math.max(0, selIndex - 1); markSel(); }
    else if (e.key === 'Enter') {
      e.preventDefault();
      const list = renderLauncher(launcherInput.value);
      const app = list[selIndex];
      if (app) { closeLauncher(); launch(app.id); }
    }
  };

  /* ── popovers ──────────────────────────────────────────────────────── */
  const quick = document.getElementById('quick');
  const userMenu = document.getElementById('userMenu');
  const calendar = document.getElementById('calendar');
  const popovers = [quick, userMenu, calendar];

  const hidePopovers = () => popovers.forEach((p) => p.classList.add('hidden'));
  const place = (el, anchor, width = 320) => {
    const r = anchor.getBoundingClientRect();
    el.style.width = width + 'px';
    const left = Math.min(window.innerWidth - width - 10, Math.max(10, r.right - width));
    el.style.left = left + 'px';
    el.style.top = r.bottom + 6 + 'px';
  };

  const rootSwitchHTML = () => `
    <div class="pop-row">
      <span class="k" style="display:flex;align-items:center;gap:8px">${iconHTML('root', 14)} root mode</span>
      <button class="switch ${state.root ? 'on' : ''}" data-root></button>
    </div>`;

  function buildQuick() {
    quick.innerHTML = `
      <h4>Быстрые настройки</h4>
      ${rootSwitchHTML()}
      <div class="pop-sep"></div>
      <div class="pop-row"><span class="k">процессор</span><span class="v q-cpu">—</span></div>
      <div class="mini-bar" style="margin:2px 0 8px"><i class="q-cpub" style="width:0%"></i></div>
      <div class="pop-row"><span class="k">память</span><span class="v q-mem">—</span></div>
      <div class="mini-bar" style="margin:2px 0 8px"><i class="q-memb" style="width:0%"></i></div>
      <div class="pop-row"><span class="k">диск</span><span class="v q-disk">—</span></div>
      <div class="mini-bar" style="margin:2px 0 8px"><i class="q-diskb" style="width:0%"></i></div>
      <div class="pop-sep"></div>
      <div class="pop-row"><span class="k">акцент</span><div class="swatches q-sw"></div></div>
      <div class="pop-row"><span class="k">обои</span><div class="wall-swatches q-wall" style="display:flex;gap:5px"></div></div>
      <div class="pop-sep"></div>
      <button class="pop-btn" data-go="settings">${iconHTML('settings', 14)}<span>Настройки</span></button>
      <button class="pop-btn" data-go="monitor">${iconHTML('monitor', 14)}<span>Системный монитор</span></button>
      <button class="pop-btn danger" data-lock>${iconHTML('lock', 14)}<span>Заблокировать экран</span></button>`;

    quick.querySelector('[data-root]').onclick = (e) => {
      set('root', !state.root);
      e.currentTarget.classList.toggle('on', state.root);
      buildQuick();
      wm.notify(state.root ? 'root mode включён' : 'root mode выключен',
        state.root ? 'команды в терминале и файловые операции идут через sudo -n' : 'снова обычный пользователь',
        state.root ? 'warn' : 'info');
      paintRootUi();
    };

    const sw = quick.querySelector('.q-sw');
    for (const c of ACCENTS) {
      const b = document.createElement('button');
      b.className = 'swatch' + (state.accent === c ? ' active' : '');
      b.style.cssText = `background:${c};width:20px;height:20px;border-radius:6px`;
      b.onclick = () => { set('accent', c); applyTheme(); buildQuick(); };
      sw.appendChild(b);
    }
    const wall = quick.querySelector('.q-wall');
    for (const w of WALLPAPERS) {
      const b = document.createElement('button');
      b.className = 'wall-swatch' + (state.wallpaper === w.id ? ' active' : '');
      b.style.cssText = `background:${w.css};width:38px;height:26px;border-radius:6px;border-width:2px`;
      b.title = w.name;
      b.onclick = () => { set('wallpaper', w.id); applyTheme(); buildQuick(); };
      wall.appendChild(b);
    }
    quick.querySelectorAll('[data-go]').forEach((b) => {
      b.onclick = () => { hidePopovers(); launch(b.dataset.go); };
    });
    quick.querySelector('[data-lock]').onclick = () => { hidePopovers(); lockScreen(); };
    if (lastStats) paintQuickStats(lastStats);
  }

  function paintQuickStats(s) {
    const q = (sel) => quick.querySelector(sel);
    if (!q('.q-cpu')) return;
    q('.q-cpu').textContent = `${s.cpu}% · load ${s.load[0]}`;
    q('.q-cpub').style.width = Math.max(2, s.cpu) + '%';
    const memPct = (s.mem.used / s.mem.total) * 100;
    q('.q-mem').textContent = `${humanSize(s.mem.used)} / ${humanSize(s.mem.total)}`;
    q('.q-memb').style.width = memPct + '%';
    q('.q-disk').textContent = `${humanSize(s.disk.used)} / ${humanSize(s.disk.total)}`;
    q('.q-diskb').style.width = s.disk.pct + '%';
  }

  document.getElementById('quickBtn').onclick = (e) => {
    e.stopPropagation();
    const wasHidden = quick.classList.contains('hidden');
    hidePopovers();
    if (wasHidden) { buildQuick(); place(quick, document.getElementById('quickBtn')); quick.classList.remove('hidden'); }
  };

  function buildUserMenu() {
    userMenu.innerHTML = `
      <div class="pop-row" style="padding-bottom:10px">
        <span class="k" style="display:flex;align-items:center;gap:9px;color:var(--text)">
          ${iconHTML('user', 16)} <b>user@sandbox</b>
        </span>
      </div>
      <div class="pop-row"><span class="k">права</span><span class="v" id="umRights">user</span></div>
      <div class="pop-row"><span class="k">sudo -n</span><span class="v" id="umSudo">—</span></div>
      <div class="pop-sep"></div>
      <button class="pop-btn" data-go="settings">${iconHTML('settings', 14)}<span>Настройки</span></button>
      <button class="pop-btn" data-go="files">${iconHTML('files', 14)}<span>Домашняя папка</span></button>
      <button class="pop-btn" data-go="terminal">${iconHTML('terminal', 14)}<span>Терминал</span></button>
      <div class="pop-sep"></div>
      <button class="pop-btn danger" data-lock>${iconHTML('lock', 14)}<span>Заблокировать</span></button>
      <button class="pop-btn danger" data-closeall>${iconHTML('power', 14)}<span>Закрыть все окна</span></button>`;
    userMenu.querySelectorAll('[data-go]').forEach((b) => {
      b.onclick = () => { hidePopovers(); launch(b.dataset.go); };
    });
    userMenu.querySelector('[data-lock]').onclick = () => { hidePopovers(); lockScreen(); };
    userMenu.querySelector('[data-closeall]').onclick = () => { hidePopovers(); wm.closeAll(); };
    if (lastStats) {
      userMenu.querySelector('#umRights').textContent = state.root ? 'root (sudo)' : lastStats.user;
      userMenu.querySelector('#umSudo').textContent = lastStats.sudoPasswordless ? 'без пароля ✓' : 'нужен пароль';
      userMenu.querySelector('#umSudo').style.color = lastStats.sudoPasswordless ? 'var(--green)' : 'var(--red)';
    }
  }

  document.getElementById('userBtn').onclick = (e) => {
    e.stopPropagation();
    const wasHidden = userMenu.classList.contains('hidden');
    hidePopovers();
    if (wasHidden) { buildUserMenu(); place(userMenu, document.getElementById('userBtn'), 260); userMenu.classList.remove('hidden'); }
  };

  const calendarState = { month: new Date().getMonth(), year: new Date().getFullYear() };
  function buildCalendar() {
    const { month, year } = calendarState;
    const first = new Date(year, month, 1);
    const startDow = (first.getDay() + 6) % 7; // monday first
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const prevDays = new Date(year, month, 0).getDate();
    const today = new Date();
    const cells = [];
    for (let i = startDow - 1; i >= 0; i--) cells.push({ d: prevDays - i, other: true });
    for (let d = 1; d <= daysInMonth; d++) cells.push({ d, other: false });
    while (cells.length % 7) cells.push({ d: cells.length - startDow - daysInMonth + 1, other: true });

    const monthName = new Date(year, month, 1).toLocaleDateString('ru-RU', { month: 'long', year: 'numeric' });
    calendar.innerHTML = `
      <div class="cal-head">
        <button data-mv="-1">${icons.chevronLeft}</button>
        <span style="text-transform:capitalize">${esc(monthName)}</span>
        <button data-mv="1">${icons.chevronRight}</button>
      </div>
      <div style="text-align:center;color:var(--dim);font-size:.82em;margin-bottom:8px">
        ${today.toLocaleDateString('ru-RU', { weekday: 'long', day: 'numeric', month: 'long' })}
      </div>
      <div class="cal-grid">
        ${['пн', 'вт', 'ср', 'чт', 'пт', 'сб', 'вс'].map((d) => `<span class="wd">${d}</span>`).join('')}
        ${cells.map((c) => {
          const isToday = !c.other && c.d === today.getDate() && month === today.getMonth() && year === today.getFullYear();
          return `<span class="d${c.other ? ' other' : ''}${isToday ? ' today' : ''}">${c.d}</span>`;
        }).join('')}
      </div>`;
    calendar.querySelectorAll('[data-mv]').forEach((b) => {
      b.onclick = () => {
        calendarState.month += Number(b.dataset.mv);
        if (calendarState.month < 0) { calendarState.month = 11; calendarState.year--; }
        if (calendarState.month > 11) { calendarState.month = 0; calendarState.year++; }
        buildCalendar();
      };
    });
  }

  document.getElementById('clockBtn').onclick = (e) => {
    e.stopPropagation();
    const wasHidden = calendar.classList.contains('hidden');
    hidePopovers();
    if (wasHidden) {
      const now = new Date();
      calendarState.month = now.getMonth();
      calendarState.year = now.getFullYear();
      buildCalendar();
      place(calendar, document.getElementById('clockBtn'), 300);
      calendar.classList.remove('hidden');
    }
  };

  document.addEventListener('pointerdown', (e) => {
    if (e.target.closest('.popover') || e.target.closest('#quickBtn') || e.target.closest('#userBtn') || e.target.closest('#clockBtn') || e.target.closest('#appsBtn')) return;
    hidePopovers();
  });

  /* ── lock screen ───────────────────────────────────────────────────── */
  function lockScreen() {
    if (document.getElementById('lockScreen')) return;
    const lock = document.createElement('div');
    lock.id = 'lockScreen';
    lock.style.cssText = 'position:fixed;inset:0;z-index:9800;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:16px;background:rgba(4,8,13,.82);backdrop-filter:blur(24px);cursor:pointer;animation:fade .2s';
    const paint = () => {
      const d = new Date();
      lock.innerHTML = `
        <div style="font-size:3.4em;font-weight:300;letter-spacing:.02em;font-variant-numeric:tabular-nums">${d.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}</div>
        <div style="color:var(--dim);font-size:.95em">${d.toLocaleDateString('ru-RU', { weekday: 'long', day: 'numeric', month: 'long' })}</div>
        <div style="margin-top:26px;color:var(--faint);font-size:.82em;display:flex;align-items:center;gap:8px">${icons.lock.replace('<svg', '<svg style="width:15px;height:15px"')} нажми, чтобы разблокировать</div>
        <div style="margin-top:8px;color:#3d4551;font-size:.74em">user@sandbox · ${state.root ? 'root mode' : 'обычный режим'}</div>`;
    };
    paint();
    const timer = setInterval(paint, 1000);
    const unlock = () => { clearInterval(timer); lock.remove(); };
    lock.onclick = unlock;
    document.addEventListener('keydown', function onKey(e) { if (e.key === 'Escape' || e.key === 'Enter') { unlock(); document.removeEventListener('keydown', onKey); } });
    document.body.appendChild(lock);
  }

  /* ── clock ─────────────────────────────────────────────────────────── */
  const clockTxt = document.getElementById('clockTxt');
  const tickClock = () => {
    const d = new Date();
    const opts = { hour: '2-digit', minute: '2-digit', hour12: !state.clock24 };
    if (state.showSeconds) opts.second = '2-digit';
    const t = d.toLocaleTimeString('ru-RU', opts);
    const date = d.toLocaleDateString('ru-RU', { weekday: 'short', day: 'numeric', month: 'short' });
    clockTxt.textContent = `${date}  ${t}`;
  };
  tickClock();
  setInterval(tickClock, state.showSeconds ? 1000 : 10000);
  on('clock24', tickClock);
  on('showSeconds', tickClock);

  /* ── stats polling ─────────────────────────────────────────────────── */
  let lastStats = null;
  let fails = 0;
  const netInd = document.getElementById('netIndicator');
  const cpuTxt = document.getElementById('cpuTxt');

  function paintRootUi() {
    document.getElementById('rootIndicator').classList.toggle('hidden', !state.root);
    document.documentElement.style.setProperty('--accent', state.accent);
  }

  async function poll() {
    try {
      const s = await api.stats();
      lastStats = s;
      fails = 0;
      cpuTxt.textContent = `${s.cpu}%`;
      netInd.classList.remove('offline');
      netInd.title = `Соединение с сервером: pid ${s.pid} · ${s.transport}`;
      if (document.getElementById('quick') && !quick.classList.contains('hidden')) paintQuickStats(s);
      if (!userMenu.classList.contains('hidden')) {
        userMenu.querySelector('#umSudo').textContent = s.sudoPasswordless ? 'без пароля ✓' : 'нужен пароль';
      }
      document.querySelector('.user-ind').title = `${s.user}@${s.host}`;
    } catch (e) {
      fails++;
      if (fails >= 2) {
        netInd.classList.add('offline');
        netInd.title = 'Нет связи с сервером: ' + e.message;
        cpuTxt.textContent = '—';
        if (fails === 2) wm.notify('Нет связи с сервером', 'Проверь, что node app/server.js запущен. Повторяю попытки…', 'error');
      }
    }
  }
  poll();
  setInterval(poll, 3000);
  on('root', paintRootUi);
  paintRootUi();

  /* ── keyboard shortcuts ────────────────────────────────────────────── */
  document.addEventListener('keydown', (e) => {
    const typing = /^(INPUT|TEXTAREA)$/.test(e.target.tagName) || e.target.isContentEditable;
    if (e.key === 'Escape') { hidePopovers(); if (!launcher.classList.contains('hidden')) closeLauncher(); return; }
    if (e.key === 'Meta' || e.key === 'OS') { e.preventDefault(); toggleLauncher(); return; }
    if (e.ctrlKey && e.altKey) {
      const map = { t: 'terminal', f: 'files', e: 'editor', b: 'browser', m: 'monitor', s: 'settings' };
      const app = map[e.key.toLowerCase()];
      if (app) { e.preventDefault(); launch(app); }
    }
    if (typing) return;
    if (e.key === '/' && !state.root) { /* nothing — reserved */ }
  });

  // terminal external command hook
  wm.onChange(() => {
    const term = wm.byApp('terminal');
    if (term && !term._hooked) {
      term._hooked = true;
      term.body.addEventListener('desktop:run', (ev) => {
        const { cmd } = ev.detail || {};
        if (!cmd) return;
        const input = term.body.querySelector('.term-inputline input');
        if (input) {
          input.value = cmd;
          input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
        }
      });
    }
  });

  window.addEventListener('resize', () => hidePopovers());

  return desktop;
}
