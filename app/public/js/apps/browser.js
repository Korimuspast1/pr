/* Browser — Firefox-style: tabs, bookmarks bar, history, view-source, downloads. */

import { api } from '../api.js';
import { state, set, on } from '../store.js';
import { esc, humanSize, fmtDate } from '../util.js';
import { icons, iconHTML } from '../icons.js';

const KEEP = new Set(['H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'P', 'UL', 'OL', 'LI', 'A', 'CODE', 'PRE', 'BLOCKQUOTE', 'TABLE', 'THEAD', 'TBODY', 'TR', 'TD', 'TH', 'STRONG', 'B', 'EM', 'I', 'IMG', 'HR', 'BR', 'SPAN', 'DIV', 'SECTION', 'ARTICLE', 'MAIN', 'HEADER', 'FOOTER', 'NAV', 'FIGURE', 'FIGCAPTION', 'DL', 'DT', 'DD', 'SMALL', 'SUP', 'SUB', 'KBD', 'MARK', 'TIME', 'BODY', 'HTML']);
const DROP = new Set(['SCRIPT', 'STYLE', 'IFRAME', 'OBJECT', 'EMBED', 'FORM', 'INPUT', 'BUTTON', 'SELECT', 'TEXTAREA', 'LINK', 'META', 'NOSCRIPT', 'TEMPLATE', 'SVG', 'VIDEO', 'AUDIO', 'CANVAS', 'HEAD', 'TITLE', 'BASE']);
const SEARCH = 'https://lite.duckduckgo.com/lite/?q=';

function sanitize(html, baseUrl) {
  const doc = new DOMParser().parseFromString(html, 'text/html');
  doc.querySelectorAll('script,style,link,meta,noscript,template,svg,iframe,object,embed,form,input,button,select,textarea,video,audio,canvas,head,title,base').forEach((n) => n.remove());

  const walk = (node, depth) => {
    if (depth > 70) return;
    for (const child of [...node.childNodes]) {
      if (child.nodeType === 3) continue;
      if (child.nodeType !== 1) { child.remove(); continue; }
      const tag = child.tagName;
      if (DROP.has(tag)) { child.remove(); continue; }
      if (!KEEP.has(tag)) {
        const frag = doc.createDocumentFragment();
        while (child.firstChild) frag.appendChild(child.firstChild);
        child.replaceWith(frag);
        continue;
      }
      for (const attr of [...child.attributes]) {
        const n = attr.name.toLowerCase();
        if (!['href', 'src', 'alt', 'title', 'colspan', 'rowspan'].includes(n)) child.removeAttribute(attr.name);
      }
      if (tag === 'A' && child.getAttribute('href')) {
        const href = child.getAttribute('href');
        if (href.startsWith('#') || href.toLowerCase().startsWith('javascript:')) child.removeAttribute('href');
        else { try { child.setAttribute('href', new URL(href, baseUrl).href); } catch { child.removeAttribute('href'); } }
      }
      if (tag === 'IMG') {
        const src = child.getAttribute('src');
        if (src) { try { child.setAttribute('src', new URL(src, baseUrl).href); } catch { child.remove(); continue; } }
        else { child.remove(); continue; }
        child.setAttribute('loading', 'lazy');
        child.setAttribute('referrerpolicy', 'no-referrer');
      }
      walk(child, depth + 1);
    }
  };
  walk(doc.body, 0);
  return { title: doc.title || '', body: doc.body.innerHTML };
}

const sameOrigin = (u) => { try { return new URL(u).origin === location.origin; } catch { return false; } };
const pretty = (u) => String(u || '').replace(/^https?:\/\//, '').replace(/\/$/, '');
const favicon = (u) => {
  try { return new URL(u).origin + '/favicon.ico'; } catch { return null; }
};

export default {
  id: 'browser',
  name: 'Браузер',
  icon: 'browser',
  width: 980,
  height: 640,

  mount(body, win) {
    let tabs = [];
    let activeId = null;
    let tabSeq = 0;
    let downloads = [];

    body.innerHTML = `
      <div class="ff-wrap">
        <div class="ff-tabbar">
          <div class="ff-tabs"></div>
          <button class="ff-newtab" title="Новая вкладка (Ctrl+T)">${icons.plus}</button>
        </div>
        <div class="toolbar ff-toolbar">
          <button class="tb-btn" data-a="back" title="Назад">${icons.chevronLeft}</button>
          <button class="tb-btn" data-a="fwd" title="Вперёд">${icons.chevronRight}</button>
          <button class="tb-btn" data-a="reload" title="Обновить (Ctrl+R)">${icons.refresh}</button>
          <button class="tb-btn" data-a="home" title="Домой">${icons.home}</button>
          <input class="field ff-url" placeholder="Поиск или адрес сайта" spellcheck="false" />
          <button class="tb-btn ff-star" title="В закладки">☆</button>
          <button class="tb-btn" data-a="menu" title="Меню">☰</button>
        </div>
        <div class="ff-bookmarks"></div>
        <div class="ff-pages"></div>
        <div class="statusbar">
          <span class="st-status">готов</span>
          <span class="grow"></span>
          <span class="st-meta"></span>
        </div>
      </div>`;

    const $ = (s) => body.querySelector(s);
    const tabsEl = $('.ff-tabs');
    const pagesEl = $('.ff-pages');
    const urlbar = $('.ff-url');
    const bookmarksEl = $('.ff-bookmarks');
    const statusEl = $('.st-status');
    const metaEl = $('.st-meta');
    const starBtn = $('.ff-star');

    const activeTab = () => tabs.find((t) => t.id === activeId);
    let mounted = true;
    const alive = () => mounted;

    /* ── tab strip ─────────────────────────────────────────────────── */
    function paintTabs() {
      tabsEl.innerHTML = '';
      tabs.forEach((t) => {
        const el = document.createElement('div');
        el.className = 'ff-tab' + (t.id === activeId ? ' active' : '') + (t.loading ? ' loading' : '');
        el.innerHTML = `<span class="ff-tab-ico">${t.loading ? '<span class="spin"></span>' : '🌐'}</span><span class="lbl">${esc(t.title || 'Новая вкладка')}</span><button class="x" title="Закрыть">×</button>`;
        el.onclick = (e) => {
          if (e.target.classList.contains('x')) { e.stopPropagation(); closeTab(t.id); return; }
          activate(t.id);
        };
        el.onauxclick = (e) => { if (e.button === 1) closeTab(t.id); };
        tabsEl.appendChild(el);
      });
      const active = activeTab();
      starBtn.textContent = active && isBookmarked(active.url) ? '★' : '☆';
      starBtn.style.color = active && isBookmarked(active.url) ? 'var(--yellow)' : '';
    }

    function activate(id) {
      activeId = id;
      for (const t of tabs) t.page.classList.toggle('hidden', t.id !== id);
      const t = activeTab();
      if (t) {
        urlbar.value = t.url ? t.url : '';
        win.setTitle(`${t.title || 'Новая вкладка'} — браузер`);
        statusEl.textContent = t.status || 'готов';
        metaEl.textContent = t.meta || '';
      }
      paintTabs();
    }

    function newTab(url = null, { background = false } = {}) {
      const id = 't' + ++tabSeq;
      const page = document.createElement('div');
      page.className = 'browser-page';
      pagesEl.appendChild(page);
      const tab = { id, page, title: 'Новая вкладка', url: '', stack: [], index: -1, status: 'готов', meta: '' };
      tabs.push(tab);
      renderStart(page);
      if (!background) activate(id);
      paintTabs();
      if (url) navigate(url, tab);
      return tab;
    }

    function closeTab(id) {
      const i = tabs.findIndex((t) => t.id === id);
      if (i < 0) return;
      const [tab] = tabs.splice(i, 1);
      tab.page.remove();
      if (activeId === id) {
        const next = tabs[i] || tabs[i - 1];
        if (next) activate(next.id);
        else { newTab(); return; }
      }
      paintTabs();
    }

    /* ── start page ────────────────────────────────────────────────── */
    function renderStart(page) {
      const bms = state.bookmarks || [];
      page.innerHTML = `
        <div class="browser-start">
          <h2 style="color:#fff;margin:0">Новая вкладка</h2>
          <div style="color:var(--faint);font-size:.85em">
            Страницы загружает сервер сандбокса (curl). Доступны github.com, registry.npmjs.org, pypi.org и т. п.;
            остальные сайты могут отваливаться по TLS.
          </div>
          <div class="bm-grid">
            ${bms.map((b, i) => `<button class="bm-card" data-bm="${i}">${iconHTML('pin', 17)}<span>${esc(b.title)}<small>${esc(b.url)}</small></span></button>`).join('')}
            <button class="bm-card" data-bm="stats">${iconHTML('cpu', 17)}<span>Состояние сандбокса<small>/api/stats</small></span></button>
            <button class="bm-card" data-bm="proc">${iconHTML('monitor', 17)}<span>Список процессов<small>/api/proc</small></span></button>
            <button class="bm-card" data-bm="shot">${iconHTML('image', 17)}<span>Скриншот рабочего стола<small>screenshot/desktop.png</small></span></button>
          </div>
        </div>`;
      page.querySelectorAll('[data-bm]').forEach((b) => {
        b.onclick = () => {
          const id = b.dataset.bm;
          if (id === 'stats') return navigate(location.origin + '/api/stats');
          if (id === 'proc') return navigate(location.origin + '/api/proc');
          if (id === 'shot') return window.desktop.launch('viewer', { path: '/home/user/pr/screenshot/desktop.png' });
          navigate(bms[Number(id)].url);
        };
      });
    }

    const normalize = (raw) => {
      const v = String(raw || '').trim();
      if (!v) return null;
      if (/^https?:\/\//i.test(v)) return v;
      if (v.startsWith('/')) return location.origin + v;
      if (/^(localhost|127\.|0\.0\.0\.0)/.test(v)) return 'http://' + v;
      if (v.startsWith('about:')) return v;
      if (/^[\w-]+(\.[\w-]+)+(\/.*)?$/.test(v)) return 'https://' + v;
      return SEARCH + encodeURIComponent(v);
    };

    /* ── navigation ────────────────────────────────────────────────── */
    async function navigate(rawUrl, tab = activeTab(), { push = true, source = false } = {}) {
      if (!tab) return;
      const url = normalize(rawUrl);
      if (!url) return;

      if (url === 'about:blank') { renderStart(tab.page); tab.url = ''; tab.title = 'Новая вкладка'; paintTabs(); return; }
      if (sameOrigin(url) && !url.includes('/api/') && !url.includes('/x/')) {
        window.open(url, '_blank', 'noopener');
        return;
      }

      tab.loading = true;
      tab.status = 'загрузка…';
      tab.meta = '';
      const progress = (ms) => {
        if (tab !== activeTab()) return;
        statusEl.textContent = `загрузка… ${(ms / 1000).toFixed(1)} с`;
      };
      if (tab === activeTab()) { statusEl.textContent = 'загрузка…'; metaEl.textContent = ''; }
      paintTabs();
      tab.page.innerHTML = '<div class="empty">Загрузка…</div>';

      try {
        const r = await api.fetchUrl(url, { onTick: progress });
        tab.loading = false;
        if (push) {
          tab.stack = tab.stack.slice(0, tab.index + 1);
          tab.stack.push(url);
          tab.index = tab.stack.length - 1;
        }
        tab.url = url;
        const type = (r.contentType || '').toLowerCase();

        if (source) {
          tab.page.innerHTML = `<pre style="margin:0"><code>${esc(r.text)}</code></pre>`;
          tab.title = `view-source: ${pretty(url)}`;
        } else if (type.includes('json') || /^\s*[[{]/.test(r.text)) {
          let out = r.text;
          try { out = JSON.stringify(JSON.parse(r.text), null, 2); } catch {}
          tab.page.innerHTML = `<pre style="margin:0"><code>${esc(out)}</code></pre>`;
          tab.title = pretty(url);
        } else if (type.includes('html') || /^\s*</.test(r.text)) {
          const clean = sanitize(r.text, r.url || url);
          tab.page.innerHTML = clean.body || '<div class="empty">Пустая страница</div>';
          tab.title = clean.title || pretty(url);
          tab.page.querySelectorAll('a[href]').forEach((a) => {
            a.addEventListener('click', (e) => {
              e.preventDefault();
              const href = a.getAttribute('href');
              if (!href) return;
              if (sameOrigin(href)) return window.open(href, '_blank', 'noopener');
              navigate(href, tab);
            });
          });
          tab.page.querySelectorAll('img').forEach((img) => {
            img.onerror = () => { img.style.display = 'none'; };
          });
        } else {
          tab.page.innerHTML = `<pre style="margin:0"><code>${esc(r.text.slice(0, 300000))}</code></pre>`;
          tab.title = pretty(url);
        }

        tab.status = `HTTP ${r.status}${r.truncated ? ' · обрезано' : ''}`;
        tab.meta = `${humanSize(r.bytes)} · ${(r.contentType || 'unknown').split(';')[0]}`;
        tab.page.scrollTop = 0;
        if (tab === activeTab()) {
          urlbar.value = url;
          statusEl.textContent = tab.status;
          metaEl.textContent = tab.meta;
          win.setTitle(`${tab.title} — браузер`);
        }
      } catch (e) {
        tab.loading = false;
        const transient = /шлюз|перегружен|пауза|таймаут|связ|сжат|HTTP 5/i.test(e.message || '');
        if (transient && (tab.retries = (tab.retries || 0) + 1) <= 2) {
          if (tab === activeTab()) statusEl.textContent = `шлюз занят, повтор ${tab.retries}/2…`;
          await new Promise((r) => setTimeout(r, 900 * tab.retries));
          if (alive()) return navigate(url, tab, { push: false });
        }
        tab.stack = push ? [...tab.stack.slice(0, tab.index + 1), url] : tab.stack;
        tab.index = push ? tab.stack.length - 1 : tab.index;
        tab.url = url;
        tab.title = 'Не удалось открыть';
        tab.page.innerHTML = `<div class="empty"><div>
          <div style="color:${transient ? 'var(--yellow)' : 'var(--red)'};font-weight:600;margin-bottom:8px">
            ${transient ? 'Превью-прокси не пропустил запрос' : 'Не удалось загрузить страницу'}
          </div>
          <div style="font-family:var(--mono);font-size:.85em;color:var(--dim);max-width:560px">${esc(e.message || e)}</div>
          <div style="margin-top:14px;color:var(--faint);font-size:.85em;max-width:560px">
            ${transient
              ? 'Превью-шлюз (Cloudflare) ответил 502 — это ограничение частоты запросов, а не проблема сайта. Нажми «Повторить»; приложение само делает паузу и снижает темп опросов.'
              : 'Сандбокс ходит в интернет только к github.com, registry.npmjs.org, pypi.org и т. п. Проверь адрес или открой локальную страницу (/api/stats).'}
          </div>
          <div style="margin-top:16px;display:flex;gap:8px;justify-content:center">
            <button class="btn primary" data-retry>Повторить</button>
            <button class="btn ghost" data-local>Открыть /api/stats</button>
          </div>
        </div></div>`;
        tab.page.querySelector('[data-retry]')?.addEventListener('click', () => navigate(url, tab, { push: false }));
        tab.page.querySelector('[data-local]')?.addEventListener('click', () => navigate(location.origin + '/api/stats', tab));
        tab.status = 'ошибка';
        if (tab === activeTab()) {
          statusEl.textContent = 'ошибка';
          win.setTitle('Браузер — ошибка');
        }
      } finally {
        paintTabs();
      }
    }

    /* ── bookmarks ─────────────────────────────────────────────────── */
    const isBookmarked = (url) => !!url && (state.bookmarks || []).some((b) => b.url === url);

    function paintBookmarks() {
      const bms = state.bookmarks || [];
      bookmarksEl.innerHTML = bms.map((b, i) => `
        <button class="ff-bm" data-i="${i}" title="${esc(b.url)}">${iconHTML('pin', 12)}<span>${esc(b.title)}</span></button>`).join('');
      bookmarksEl.querySelectorAll('.ff-bm').forEach((btn) => {
        const b = bms[Number(btn.dataset.i)];
        btn.onclick = () => navigate(b.url);
        btn.oncontextmenu = (e) => {
          e.preventDefault();
          const nb = bms.filter((x) => x !== b);
          set('bookmarks', nb);
          paintBookmarks();
          win.notify('Закладка удалена', b.title, 'info');
        };
      });
    }

    starBtn.onclick = () => {
      const t = activeTab();
      if (!t || !t.url) return win.notify('Нечего сохранять', 'Сначала открой страницу', 'warn');
      const bms = state.bookmarks || [];
      if (isBookmarked(t.url)) {
        set('bookmarks', bms.filter((b) => b.url !== t.url));
        win.notify('Удалено из закладок', t.title, 'info');
      } else {
        set('bookmarks', [...bms, { title: t.title || pretty(t.url), url: t.url }]);
        win.notify('Добавлено в закладки', t.title, 'ok');
      }
      paintBookmarks();
      paintTabs();
    };

    /* ── menu ──────────────────────────────────────────────────────── */
    function openMenu(anchor) {
      document.querySelectorAll('.ctx').forEach((n) => n.remove());
      const r = anchor.getBoundingClientRect();
      const menu = document.createElement('div');
      menu.className = 'ctx';
      menu.style.left = Math.min(window.innerWidth - 260, r.right - 240) + 'px';
      menu.style.top = r.bottom + 6 + 'px';
      menu.style.minWidth = '240px';
      const item = (icon, label, hint, fn) => {
        const b = document.createElement('button');
        b.className = 'pop-btn';
        b.innerHTML = `${iconHTML(icon, 14)}<span style="flex:1">${esc(label)}</span><span style="color:var(--faint);font-size:.8em">${esc(hint || '')}</span>`;
        b.onclick = () => { menu.remove(); fn(); };
        menu.appendChild(b);
        return b;
      };
      const sep = () => { const d = document.createElement('div'); d.className = 'pop-sep'; menu.appendChild(d); };

      item('plus', 'Новая вкладка', 'Ctrl+T', () => newTab());
      item('maximize', 'Дублировать вкладку', '', () => { const t = activeTab(); if (t?.url) newTab(t.url); });
      sep();
      item('code', 'Просмотр исходного кода', '', () => {
        const t = activeTab();
        if (t?.url) navigate(t.url, t, { push: false, source: true });
      });
      item('download', 'Сохранить страницу', '', () => {
        const t = activeTab();
        if (!t?.url) return win.notify('Нет страницы', 'Сначала открой страницу', 'warn');
        const a = document.createElement('a');
        a.href = t.url;
        a.download = (pretty(t.url).replace(/[^\w.-]+/g, '_') || 'page') + '.html';
        a.target = '_blank';
        a.click();
        win.notify('Скачивание', 'Открываю страницу в новой вкладке браузера', 'info');
      });
      item('file', 'Скачать файл по ссылке…', '', async () => {
        const url = prompt('URL файла (http/https)');
        if (!url) return;
        downloads.push({ url, at: Date.now(), name: url.split('/').pop() || 'file' });
        win.notify('Скачивание начато', url, 'info');
        const a = document.createElement('a');
        a.href = url;
        a.target = '_blank';
        a.rel = 'noopener';
        a.click();
      });
      sep();
      item('root', 'История', '', () => showHistory());
      item('trash', 'Очистить историю', '', () => {
        for (const t of tabs) { t.stack = []; t.index = -1; }
        win.notify('История очищена', 'Стек навигации во вкладках сброшен', 'ok');
      });
      sep();
      item('image', 'Открыть скриншот рабочего стола', '', () =>
        window.desktop.launch('viewer', { path: '/home/user/pr/screenshot/desktop.png' }));
      item('settings', 'Настройки рабочего стола', '', () => window.desktop.launch('settings'));

      document.body.appendChild(menu);
      setTimeout(() => {
        const close = (ev) => { if (!menu.contains(ev.target)) { menu.remove(); document.removeEventListener('pointerdown', close); } };
        document.addEventListener('pointerdown', close);
      }, 0);
    }

    function showHistory() {
      const t = activeTab();
      const items = [...(t?.stack || [])].reverse();
      const page = t.page;
      page.innerHTML = `<div class="browser-start"><h2 style="margin:0 0 12px">История вкладки</h2>
        ${items.length ? items.map((u, i) => `<button class="bm-card" data-h="${i}">${iconHTML('chevronRight', 15)}<span>${esc(pretty(u))}<small>${esc(u)}</small></span></button>`).join('') : '<div class="empty">Пусто</div>'}
      </div>`;
      page.querySelectorAll('[data-h]').forEach((b) => {
        b.onclick = () => navigate(items[Number(b.dataset.h)], t);
      });
      t.title = 'История';
      paintTabs();
    }

    /* ── chrome wiring ─────────────────────────────────────────────── */
    body.querySelector('[data-a="back"]').onclick = () => {
      const t = activeTab();
      if (t && t.index > 0) { t.index--; navigate(t.stack[t.index], t, { push: false }); }
    };
    body.querySelector('[data-a="fwd"]').onclick = () => {
      const t = activeTab();
      if (t && t.index < t.stack.length - 1) { t.index++; navigate(t.stack[t.index], t, { push: false }); }
    };
    body.querySelector('[data-a="reload"]').onclick = () => {
      const t = activeTab();
      if (t?.url) navigate(t.url, t, { push: false });
    };
    body.querySelector('[data-a="home"]').onclick = () => {
      const t = activeTab();
      if (!t) return;
      t.url = '';
      t.title = 'Новая вкладка';
      renderStart(t.page);
      urlbar.value = '';
      paintTabs();
      win.setTitle('Браузер — новая вкладка');
    };
    body.querySelector('[data-a="menu"]').onclick = (e) => openMenu(e.currentTarget);
    $('.ff-newtab').onclick = () => { newTab(); urlbar.focus(); };

    urlbar.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') { navigate(urlbar.value); urlbar.blur(); }
      if (e.key === 'Escape') urlbar.blur();
    });
    urlbar.addEventListener('focus', () => urlbar.select());

    body.addEventListener('keydown', (e) => {
      const ctrl = e.ctrlKey || e.metaKey;
      if (ctrl && e.key.toLowerCase() === 't') { e.preventDefault(); newTab(); }
      else if (ctrl && e.key.toLowerCase() === 'w') { e.preventDefault(); if (activeId) closeTab(activeId); }
      else if (ctrl && e.key.toLowerCase() === 'l') { e.preventDefault(); urlbar.focus(); }
      else if (ctrl && e.key.toLowerCase() === 'r') { e.preventDefault(); const t = activeTab(); if (t?.url) navigate(t.url, t, { push: false }); }
      else if (e.key === 'F5') { e.preventDefault(); const t = activeTab(); if (t?.url) navigate(t.url, t, { push: false }); }
    });

    paintBookmarks();
    const offBm = on('bookmarks', paintBookmarks);

    const initialUrl = win.spec.args?.url || null;
    newTab(initialUrl);
    if (initialUrl) urlbar.value = initialUrl;
    else urlbar.focus();
    win.spec.onArgs = (args) => { if (args?.url) newTab(args.url); };
    win.spec.onClose = () => { mounted = false; offBm(); };
  },
};
