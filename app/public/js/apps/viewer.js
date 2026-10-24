/* Image viewer. */

import { api } from '../api.js';
import { humanSize, fmtDate, esc } from '../util.js';
import { icons } from '../icons.js';

export default {
  id: 'viewer',
  name: 'Изображения',
  icon: 'image',
  width: 900,
  height: 620,
  singleton: false,

  mount(body, win) {
    let path = win.spec.args?.path || null;
    let zoom = 1;

    body.innerHTML = `
      <div class="viewer-wrap">
        <div class="toolbar">
          <button class="tb-btn" data-a="out" title="Уменьшить">−</button>
          <button class="tb-btn" data-a="in" title="Увеличить">+</button>
          <button class="tb-btn" data-a="fit" title="Вписать">${icons.maximize}</button>
          <button class="tb-btn" data-a="open" title="Файлы">${icons.folder}</button>
          <div class="crumbs" style="color:var(--dim);font-size:.82em;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">—</div>
          <button class="tb-btn" data-a="dl" title="Скачать">${icons.download}</button>
        </div>
        <div class="viewer-stage"><img alt="" /></div>
        <div class="statusbar">
          <span class="st-name">—</span>
          <span class="grow"></span>
          <span class="st-dim"></span>
          <span class="st-size"></span>
        </div>
      </div>`;

    const $ = (s) => body.querySelector(s);
    const img = $('img');
    const pathEl = $('.crumbs');

    const applyZoom = () => {
      img.style.transform = `scale(${zoom})`;
      img.style.maxWidth = zoom > 1 ? 'none' : '100%';
      img.style.maxHeight = zoom > 1 ? 'none' : '100%';
    };

    async function load(p) {
      path = p;
      img.src = api.downloadUrl(p, true);
      pathEl.textContent = p;
      pathEl.title = p;
      $('.st-name').textContent = p.split('/').pop();
      win.setTitle(`${p.split('/').pop()} — просмотр`);
      img.onload = () => {
        $('.st-dim').textContent = `${img.naturalWidth} × ${img.naturalHeight}`;
      };
      try {
        const meta = await api.read(p);
        $('.st-size').textContent = humanSize(meta.size) + ' · ' + fmtDate(meta.mtime);
      } catch {}
      zoom = 1;
      applyZoom();
    }

    body.querySelector('[data-a="in"]').onclick = () => { zoom = Math.min(6, zoom * 1.25); applyZoom(); };
    body.querySelector('[data-a="out"]').onclick = () => { zoom = Math.max(0.2, zoom / 1.25); applyZoom(); };
    body.querySelector('[data-a="fit"]').onclick = () => { zoom = 1; applyZoom(); };
    body.querySelector('[data-a="open"]').onclick = () => window.desktop.launch('files');
    body.querySelector('[data-a="dl"]').onclick = () => {
      if (!path) return;
      const a = document.createElement('a');
      a.href = api.downloadUrl(path);
      a.download = path.split('/').pop();
      a.click();
    };

    win.spec.onArgs = (args) => { if (args?.path) load(args.path); };
    if (path) load(path);
    else $('.viewer-stage').innerHTML = '<div class="empty">Открой изображение из «Файлов» или перетащи путь</div>';
  },
};
