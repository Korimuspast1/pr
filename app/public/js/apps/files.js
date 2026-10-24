/* Files — file manager with real filesystem operations. */

import { api } from '../api.js';
import { state, on } from '../store.js';
import { esc, humanSize, fmtDate, shortenPath } from '../util.js';
import { iconHTML, icons } from '../icons.js';

const PLACES = [
  { label: 'Домашняя папка', path: '/home/user', icon: 'home' },
  { label: 'Репозиторий', path: '/home/user/pr', icon: 'code' },
  { label: 'Скриншот', path: '/home/user/pr/screenshot', icon: 'image' },
  { label: 'Приложение', path: '/home/user/pr/app', icon: 'files' },
  { label: 'Временные файлы', path: '/tmp', icon: 'files' },
  { label: 'Конфиги', path: '/etc', icon: 'settings' },
  { label: 'Логи', path: '/var/log', icon: 'file' },
  { label: 'root', path: '/root', icon: 'lock' },
];

const TEXT_EXT = new Set(['txt', 'md', 'js', 'mjs', 'cjs', 'json', 'yml', 'yaml', 'html', 'css', 'py', 'sh', 'toml', 'ini', 'conf', 'css', 'ts', 'tsx', 'jsx', 'xml', 'csv', 'log', 'gitignore', 'env', 'service', 'lock']);
const IMG_EXT = new Set(['png', 'jpg', 'jpeg', 'gif', 'webp', 'svg', 'bmp', 'ico']);

const shq = (s) => "'" + String(s).replace(/'/g, `'\\''`) + "'";
const ext = (n) => (n.includes('.') ? n.split('.').pop().toLowerCase() : '');

export default {
  id: 'files',
  name: 'Файлы',
  icon: 'files',
  width: 860,
  height: 540,

  mount(body, win) {
    let path = '/home/user/pr';
    let items = [];
    let selected = null;
    let filter = '';
    const historyStack = [];

    body.innerHTML = `
      <div class="files-wrap">
        <aside class="files-side">
          <div class="side-title">Места</div>
          <div class="places"></div>
        </aside>
        <div class="files-main">
          <div class="toolbar">
            <button class="tb-btn" data-a="back" title="Назад">${icons.chevronLeft}</button>
            <button class="tb-btn" data-a="up" title="Вверх">${icons.arrowUp}</button>
            <button class="tb-btn" data-a="refresh" title="Обновить">${icons.refresh}</button>
            <div class="crumbs"></div>
            <button class="tb-btn" data-a="mkdir" title="Новая папка">${icons.plus}</button>
            <input class="field" style="flex:0 0 150px" placeholder="фильтр…" />
          </div>
          <div class="file-rows"></div>
          <div class="statusbar">
            <span class="st-count">—</span>
            <span class="grow"></span>
            <span class="st-path"></span>
          </div>
        </div>
      </div>`;

    const $ = (s) => body.querySelector(s);
    const rows = $('.file-rows');
    const crumbs = $('.crumbs');
    const filterInput = body.querySelector('.field');
    const statusCount = $('.st-count');
    const statusPath = $('.st-path');

    // places
    const placesEl = $('.places');
    for (const p of PLACES) {
      const b = document.createElement('button');
      b.className = 'place';
      b.dataset.path = p.path;
      b.innerHTML = `${iconHTML(p.icon, 14)}<span>${esc(p.label)}</span>`;
      b.onclick = () => navigate(p.path);
      placesEl.appendChild(b);
    }

    const markPlaces = () => {
      placesEl.querySelectorAll('.place').forEach((b) => b.classList.toggle('active', b.dataset.path === path));
    };

    async function navigate(target, push = true) {
      try {
        const data = await api.files(target);
        if (data.error) throw new Error(data.error);
        if (push && data.path !== path) historyStack.push(path);
        path = data.path;
        items = data.items;
        selected = null;
        render();
      } catch (e) {
        win.notify('Не удалось открыть папку', String(e.message || e), 'error');
      }
    }

    function render() {
      markPlaces();
      win.setTitle(`Файлы — ${shortenPath(path)}`);
      statusPath.textContent = shortenPath(path);

      // breadcrumbs
      crumbs.innerHTML = '';
      const parts = path.split('/').filter(Boolean);
      const homeBtn = document.createElement('button');
      homeBtn.className = 'crumb-btn';
      homeBtn.textContent = '/';
      homeBtn.onclick = () => navigate('/');
      crumbs.appendChild(homeBtn);
      let acc = '';
      parts.forEach((part, i) => {
        acc += '/' + part;
        const b = document.createElement('button');
        b.className = 'crumb-btn';
        b.textContent = part;
        const target = acc;
        b.onclick = () => navigate(target);
        crumbs.appendChild(b);
        if (i < parts.length - 1) {
          const sep = document.createElement('span');
          sep.className = 'crumb-sep';
          sep.textContent = '›';
          crumbs.appendChild(sep);
        }
      });

      const q = filter.trim().toLowerCase();
      const view = items.filter((i) => !q || i.name.toLowerCase().includes(q));
      statusCount.textContent = `${view.length} элем.`;

      rows.innerHTML = '';
      const head = document.createElement('div');
      head.className = 'frow head';
      head.innerHTML = `<span></span><span>Имя</span><span class="sz">Размер</span><span class="dt">Изменён</span>`;
      rows.appendChild(head);

      if (!view.length) {
        const d = document.createElement('div');
        d.className = 'empty';
        d.textContent = q ? 'Ничего не найдено' : 'Папка пуста';
        rows.appendChild(d);
        return;
      }

      for (const it of view) {
        const row = document.createElement('div');
        const isImg = !it.dir && IMG_EXT.has(ext(it.name));
        const isTxt = !it.dir && (TEXT_EXT.has(ext(it.name)) || !ext(it.name));
        row.className = 'frow' + (it.dir ? ' dir' : '') + (selected === it.name ? ' sel' : '');
        const ic = it.dir ? 'folder' : isImg ? 'image' : isTxt ? 'file' : 'file';
        row.innerHTML = `
          <span class="ico ${it.dir ? '' : isImg ? 'img' : isTxt ? 'code' : ''}">${it.dir ? icons.folder : isImg ? icons.image : isTxt ? icons.file : icons.file}</span>
          <span class="nm">${esc(it.name)}${it.link ? ' ↗' : ''}</span>
          <span class="sz">${it.dir ? '—' : humanSize(it.size)}</span>
          <span class="dt">${fmtDate(it.mtime)}</span>`;
        row.title = it.name;
        row.onclick = () => {
          selected = it.name;
          rows.querySelectorAll('.frow').forEach((r) => r.classList.remove('sel'));
          row.classList.add('sel');
        };
        row.ondblclick = () => openItem(it);
        row.oncontextmenu = (e) => {
          e.preventDefault();
          selected = it.name;
          rows.querySelectorAll('.frow').forEach((r) => r.classList.remove('sel'));
          row.classList.add('sel');
          contextMenu(e.clientX, e.clientY, it);
        };
        rows.appendChild(row);
      }
    }

    const fullPath = (name) => (path === '/' ? '' : path) + '/' + name;

    function openItem(it) {
      const p = fullPath(it.name);
      if (it.dir) return navigate(p);
      const e = ext(it.name);
      if (IMG_EXT.has(e)) return window.desktop.launch('viewer', { path: p, name: it.name });
      if (TEXT_EXT.has(e) || !e) return window.desktop.launch('editor', { path: p });
      window.desktop.launch('editor', { path: p });
    }

    function contextMenu(x, y, it) {
      document.querySelectorAll('.ctx').forEach((n) => n.remove());
      const p = fullPath(it.name);
      const menu = document.createElement('div');
      menu.className = 'ctx';
      menu.style.left = Math.min(x, window.innerWidth - 210) + 'px';
      menu.style.top = Math.min(y, window.innerHeight - 210) + 'px';
      const entry = (icon, label, cls, fn) => {
        const b = document.createElement('button');
        b.className = 'pop-btn ' + (cls || '');
        b.innerHTML = `${iconHTML(icon, 14)}<span>${esc(label)}</span>`;
        b.onclick = () => { menu.remove(); fn(); };
        menu.appendChild(b);
      };
      if (it.dir) {
        entry('folder', 'Открыть', '', () => navigate(p));
        entry('terminal', 'Открыть в терминале', '', () => {
          window.desktop.launch('terminal');
          setTimeout(() => window.desktop.execInTerminal(`cd ${shq(p)}`), 300);
        });
      } else {
        entry('editor', 'Открыть в редакторе', '', () => window.desktop.launch('editor', { path: p }));
        if (IMG_EXT.has(ext(it.name))) entry('image', 'Открыть как изображение', '', () => window.desktop.launch('viewer', { path: p, name: it.name }));
        entry('download', 'Скачать', '', () => {
          const a = document.createElement('a');
          a.href = api.downloadUrl(p);
          a.download = it.name;
          a.click();
        });
      }
      entry('code', 'Копировать путь', '', async () => {
        try { await navigator.clipboard.writeText(p); win.notify('Путь скопирован', p, 'ok'); } catch { win.notify('Путь', p, 'info'); }
      });
      const sep = document.createElement('div');
      sep.className = 'pop-sep';
      menu.appendChild(sep);
      entry('editor', 'Переименовать', '', async () => {
        const name = prompt('Новое имя', it.name);
        if (!name || name === it.name) return;
        const r = await api.exec(`mv ${shq(p)} ${shq(fullPath(name))}`, { root: state.root });
        if (r.code !== 0) win.notify('Ошибка переименования', r.out, 'error');
        navigate(path, false);
      });
      entry('trash', 'Удалить', 'danger', async () => {
        const ok = await window.desktop.wm.confirm(`Удалить ${it.dir ? 'папку' : 'файл'} «${it.name}»?`, { ok: 'Удалить', danger: true });
        if (!ok) return;
        const r = await api.exec(`rm -rf ${shq(p)}`, { root: state.root });
        if (r.code !== 0) win.notify('Ошибка удаления', r.out, 'error');
        else win.notify('Удалено', it.name, 'ok');
        navigate(path, false);
      });
      document.body.appendChild(menu);
      setTimeout(() => {
        const close = (ev) => {
          if (!menu.contains(ev.target)) { menu.remove(); document.removeEventListener('pointerdown', close); }
        };
        document.addEventListener('pointerdown', close);
      }, 0);
    }

    // toolbar actions
    body.querySelector('[data-a="back"]').onclick = () => {
      const prev = historyStack.pop();
      if (prev) navigate(prev, false);
    };
    body.querySelector('[data-a="up"]').onclick = () => {
      if (path === '/') return;
      navigate(path.replace(/\/[^/]+\/?$/, '') || '/');
    };
    body.querySelector('[data-a="refresh"]').onclick = () => navigate(path, false);
    body.querySelector('[data-a="mkdir"]').onclick = async () => {
      const name = prompt('Имя новой папки');
      if (!name) return;
      const r = await api.exec(`mkdir -p ${shq(fullPath(name))}`, { root: state.root });
      if (r.code !== 0) win.notify('Не удалось создать папку', r.out, 'error');
      navigate(path, false);
    };
    filterInput.oninput = () => { filter = filterInput.value; render(); };

    body.addEventListener('keydown', (e) => {
      if (e.key === 'F5') { e.preventDefault(); navigate(path, false); }
    });

    win.spec.onArgs = (args) => {
      if (args?.path) navigate(args.path);
    };

    const offRoot = on('*', () => {});
    win.spec.onClose = () => offRoot();

    navigate('/home/user/pr');
  },
};
