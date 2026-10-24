/* Text editor — CodeMirror 5 (real syntax highlighting) over the sandbox FS. */

import { api } from '../api.js';
import { loadCodeMirror, modeFor } from '../codemirror.js';
import { esc, humanSize } from '../util.js';
import { icons } from '../icons.js';

export default {
  id: 'editor',
  name: 'Редактор',
  icon: 'editor',
  width: 840,
  height: 580,
  singleton: false,

  mount(body, win) {
    let path = win.spec.args?.path || null;
    let rootMode = !!win.spec.args?.root;
    let original = '';
    let dirty = false;
    let cm = null;
    let area = null;

    body.innerHTML = `
      <div class="editor-wrap">
        <div class="toolbar">
          <button class="tb-btn" data-a="open" title="Открыть файл">${icons.folder}</button>
          <button class="tb-btn" data-a="save" title="Сохранить (Ctrl+S)">${icons.save}</button>
          <div class="crumbs" style="color:var(--dim);font-size:.82em;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">—</div>
          <button class="tb-btn" data-a="wrap" title="Перенос строк">↵</button>
          <button class="tb-btn" data-a="close" title="Закрыть">${icons.close}</button>
        </div>
        <div class="editor-host"><textarea class="editor-area" spellcheck="false" placeholder="Открой файл кнопкой слева или через «Файлы»…"></textarea></div>
        <div class="statusbar">
          <span class="st-msg">готов</span>
          <span class="grow"></span>
          <span class="st-pos">—</span>
          <span class="st-lang"></span>
          <span class="st-size">—</span>
        </div>
      </div>`;

    const $ = (s) => body.querySelector(s);
    const host = $('.editor-host');
    const pathEl = $('.crumbs');
    const msg = $('.st-msg');
    const posEl = $('.st-pos');
    const langEl = $('.st-lang');
    const sizeEl = $('.st-size');
    const saveBtn = body.querySelector('[data-a="save"]');
    area = $('.editor-area');
    let wrapOn = false;

    const text = () => (cm ? cm.getValue() : area.value);
    const setText = (t) => { if (cm) cm.setValue(t); else area.value = t; };

    const setDirty = (v) => {
      dirty = v;
      body.querySelector('.editor-wrap').classList.toggle('dirty', v);
      win.setTitle(`${path ? path.split('/').pop() : 'Редактор'}${v ? ' •' : ''} — редактор`);
      saveBtn.disabled = !path;
    };

    const updateMeta = () => {
      const value = text();
      const lines = value.split('\n').length;
      let cur = 1;
      if (cm) cur = cm.getCursor().line + 1;
      else cur = value.slice(0, area.selectionStart).split('\n').length;
      posEl.textContent = `стр ${cur}/${lines}`;
      sizeEl.textContent = humanSize(new Blob([value]).size);
    };

    async function ensureEditor(mode) {
      if (cm) {
        if (mode) cm.setOption('mode', mode);
        return;
      }
      try {
        const CM = await loadCodeMirror([mode].filter(Boolean));
        const value = area.value;
        area.remove();
        cm = CM(host, {
          value,
          mode: mode || null,
          lineNumbers: true,
          theme: 'sandbox',
          indentUnit: 2,
          tabSize: 2,
          lineWrapping: wrapOn,
          autofocus: false,
          viewportMargin: 40,
          extraKeys: {
            'Ctrl-S': () => save(),
            'Cmd-S': () => save(),
            Tab: (c) => c.replaceSelection('  '),
          },
        });
        cm.on('change', () => {
          setDirty(cm.getValue() !== original);
          updateMeta();
        });
        cm.on('cursorActivity', updateMeta);
        langEl.textContent = mode || 'text';
        updateMeta();
      } catch (e) {
        console.warn('[editor] CodeMirror недоступен, работаю на textarea:', e.message);
        langEl.textContent = 'textarea';
      }
    }

    async function open(p) {
      try {
        const data = await api.read(p, rootMode);
        if (data.binary) {
          win.notify('Двоичный файл', 'Открываю просмотрщиком', 'warn');
          window.desktop.launch('viewer', { path: p, name: p.split('/').pop() });
          return;
        }
        path = data.path;
        original = data.text;
        const mode = modeFor(path);
        setText(data.text);
        await ensureEditor(mode);
        pathEl.textContent = path;
        pathEl.title = path;
        setDirty(false);
        rootMode = !!data.root || rootMode;
        msg.textContent = data.truncated
          ? 'файл обрезан (512 КБ)'
          : `загружено · ${data.encoding}${rootMode ? ' · root' : ''}`;
        updateMeta();
      } catch (e) {
        win.notify('Не удалось открыть', String(e.message || e), 'error');
      }
    }

    async function save() {
      if (!path) return;
      try {
        msg.textContent = 'сохранение…';
        const r = await api.write(path, text(), rootMode);
        original = text();
        setDirty(false);
        msg.textContent = `сохранено · ${humanSize(r.size)}`;
        win.notify('Сохранено', path, 'ok', 2200);
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
    body.querySelector('[data-a="wrap"]').onclick = () => {
      wrapOn = !wrapOn;
      if (cm) cm.setOption('lineWrapping', wrapOn);
      else area.style.whiteSpace = wrapOn ? 'pre-wrap' : 'pre';
    };

    win.spec.onArgs = (args) => { if (args?.path) open(args.path); };
    win.spec.onClose = () => {};

    if (path) open(path);
    else { area.disabled = false; msg.textContent = 'файл не выбран — открой через «Файлы»'; }
  },
};
