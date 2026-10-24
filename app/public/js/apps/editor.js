/* Text editor — reads/writes real files through the API. */

import { api } from '../api.js';
import { esc, humanSize } from '../util.js';
import { icons } from '../icons.js';

const LANG = { js: 'javascript', json: 'json', md: 'markdown', py: 'python', sh: 'shell', css: 'css', html: 'html', yml: 'yaml', yaml: 'yaml' };

export default {
  id: 'editor',
  name: 'Редактор',
  icon: 'editor',
  width: 780,
  height: 540,
  singleton: false,

  mount(body, win) {
    let path = win.spec.args?.path || null;
    let original = '';
    let dirty = false;

    body.innerHTML = `
      <div class="editor-wrap">
        <div class="toolbar">
          <button class="tb-btn" data-a="open" title="Открыть файл">${icons.folder}</button>
          <button class="tb-btn" data-a="save" title="Сохранить (Ctrl+S)">${icons.save}</button>
          <div class="crumbs" style="color:var(--dim);font-size:.82em;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">—</div>
          <button class="tb-btn" data-a="close" title="Закрыть">${icons.close}</button>
        </div>
        <textarea class="editor-area" spellcheck="false" placeholder="Открой файл кнопкой слева или через «Файлы»…"></textarea>
        <div class="statusbar">
          <span class="st-msg">готов</span>
          <span class="grow"></span>
          <span class="st-pos">—</span>
          <span class="st-lang"></span>
          <span class="st-size">—</span>
        </div>
      </div>`;

    const $ = (s) => body.querySelector(s);
    const area = $('.editor-area');
    const pathEl = $('.crumbs');
    const msg = $('.st-msg');
    const posEl = $('.st-pos');
    const langEl = $('.st-lang');
    const sizeEl = $('.st-size');
    const saveBtn = body.querySelector('[data-a="save"]');

    const setDirty = (v) => {
      dirty = v;
      area.classList.toggle('dirty', v);
      win.setTitle(`${path ? path.split('/').pop() : 'Редактор'}${v ? ' •' : ''} — редактор`);
      saveBtn.disabled = !path;
    };

    const updateMeta = () => {
      const lines = area.value.split('\n').length;
      const cur = area.value.slice(0, area.selectionStart).split('\n').length;
      posEl.textContent = `стр ${cur}/${lines}`;
      sizeEl.textContent = humanSize(new Blob([area.value]).size);
      const e = path ? path.split('.').pop().toLowerCase() : '';
      langEl.textContent = LANG[e] || (e ? e : '');
    };

    async function open(p) {
      try {
        const data = await api.read(p);
        if (data.binary) {
          win.notify('Двоичный файл', 'Открываю просмотрщиком — как изображение или скачиванием.', 'warn');
          window.desktop.launch('viewer', { path: p, name: p.split('/').pop() });
          return;
        }
        path = data.path;
        original = data.text;
        area.value = data.text;
        area.disabled = false;
        pathEl.textContent = path;
        pathEl.title = path;
        setDirty(false);
        msg.textContent = data.truncated ? 'файл обрезан (512 КБ)' : 'загружено';
        updateMeta();
      } catch (e) {
        win.notify('Не удалось открыть', String(e.message || e), 'error');
      }
    }

    async function save() {
      if (!path) return;
      try {
        msg.textContent = 'сохранение…';
        const r = await api.write(path, area.value);
        original = area.value;
        setDirty(false);
        msg.textContent = `сохранено · ${humanSize(r.size)}`;
        win.notify('Сохранено', path, 'ok');
      } catch (e) {
        msg.textContent = 'ошибка';
        win.notify('Не удалось сохранить', String(e.message || e), 'error');
      }
    }

    area.addEventListener('input', () => {
      setDirty(area.value !== original);
      updateMeta();
    });
    area.addEventListener('click', updateMeta);
    area.addEventListener('keyup', updateMeta);
    area.addEventListener('keydown', (e) => {
      if (e.key === 's' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); save(); }
      if (e.key === 'Tab') {
        e.preventDefault();
        const s = area.selectionStart;
        area.setRangeText('  ', s, area.selectionEnd, 'end');
        setDirty(true);
      }
    });

    body.querySelector('[data-a="save"]').onclick = save;
    body.querySelector('[data-a="close"]').onclick = () => win.close();
    body.querySelector('[data-a="open"]').onclick = () => window.desktop.launch('files');

    win.spec.onArgs = (args) => { if (args?.path) open(args.path); };
    win.spec.onClose = () => {};

    if (path) open(path);
    else { area.disabled = true; msg.textContent = 'файл не выбран'; }

    body.addEventListener('keydown', (e) => {
      if (e.key === 's' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); save(); }
    });
  },
};
