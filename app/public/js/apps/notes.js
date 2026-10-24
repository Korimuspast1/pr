/* Notes — real markdown files in ~/notes, autosaved through the API. */

import { api } from '../api.js';
import { esc, fmtDate, humanSize, debounce } from '../util.js';
import { icons } from '../icons.js';

const DIR = '/home/user/notes';

export default {
  id: 'notes',
  name: 'Заметки',
  icon: 'notes',
  width: 820,
  height: 540,

  mount(body, win) {
    let files = [];
    let current = null;
    let dirty = false;

    body.innerHTML = `
      <div class="notes-wrap">
        <aside class="notes-list">
          <div class="notes-head">
            <span style="font-size:.78em;color:var(--faint);text-transform:uppercase;letter-spacing:.07em">Заметки</span>
            <button class="tb-btn" data-a="new" title="Новая заметка">${icons.plus}</button>
          </div>
          <div class="notes-items"></div>
        </aside>
        <section class="notes-main">
          <div class="toolbar">
            <span class="note-title" style="flex:1;font-weight:600;color:var(--text);font-size:.9em;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">—</span>
            <div class="seg note-mode">
              <button data-mode="edit" class="on">редактор</button>
              <button data-mode="view">просмотр</button>
            </div>
            <button class="tb-btn" data-a="save" title="Сохранить (Ctrl+S)">${icons.save}</button>
            <button class="tb-btn" data-a="del" title="Удалить">${icons.trash}</button>
          </div>
          <textarea class="editor-area note-area" spellcheck="false" placeholder="Выбери заметку слева или создай новую…"></textarea>
          <div class="browser-page note-view hidden"></div>
          <div class="statusbar">
            <span class="st-msg">готов</span>
            <span class="grow"></span>
            <span class="st-meta">—</span>
          </div>
        </section>
      </div>`;

    const $ = (s) => body.querySelector(s);
    const itemsEl = $('.notes-items');
    const area = $('.note-area');
    const view = $('.note-view');
    const titleEl = $('.note-title');
    const msg = $('.st-msg');
    const meta = $('.st-meta');

    const safeName = (n) => n.replace(/[^\wа-яА-ЯёЁ .-]+/g, '_').slice(0, 80) || 'note';

    async function refresh() {
      try {
        await api.exec(`mkdir -p ${DIR}`);
        const d = await api.files(DIR);
        files = (d.items || []).filter((i) => !i.dir && /\.md$/.test(i.name));
        paintList();
      } catch (e) {
        msg.textContent = 'нет доступа к ' + DIR;
      }
    }

    function paintList() {
      itemsEl.innerHTML = '';
      if (!files.length) {
        itemsEl.innerHTML = '<div class="ch-empty" style="padding:18px 10px">Заметок нет.<br>Нажми + чтобы создать.</div>';
        return;
      }
      for (const f of files) {
        const b = document.createElement('button');
        b.className = 'note-item' + (current && current.endsWith('/' + f.name) ? ' active' : '');
        b.innerHTML = `<span class="ni-name">${esc(f.name.replace(/\.md$/, ''))}</span><span class="ni-date">${fmtDate(f.mtime).slice(0, 10)}</span>`;
        b.onclick = () => open(DIR + '/' + f.name);
        itemsEl.appendChild(b);
      }
    }

    async function open(path) {
      if (dirty && !(await window.desktop.wm.confirm('Несохранённые изменения потеряются. Открыть другую заметку?', { ok: 'Открыть' }))) return;
      try {
        const d = await api.read(path);
        current = path;
        area.value = d.text;
        titleEl.textContent = path.split('/').pop().replace(/\.md$/, '');
        meta.textContent = `${humanSize(d.size)} · изменён ${fmtDate(d.mtime)}`;
        dirty = false;
        setMode('edit');
        paintList();
        win.setTitle(`${titleEl.textContent} — заметки`);
      } catch (e) {
        win.notify('Не удалось открыть', String(e.message || e), 'error');
      }
    }

    async function save() {
      if (!current) return;
      try {
        const r = await api.write(current, area.value);
        dirty = false;
        msg.textContent = 'сохранено';
        meta.textContent = `${humanSize(r.size)} · только что`;
        refresh();
      } catch (e) {
        msg.textContent = 'ошибка сохранения';
        win.notify('Не удалось сохранить', String(e.message || e), 'error');
      }
    }

    const autosave = debounce(save, 1500);
    area.addEventListener('input', () => {
      dirty = true;
      msg.textContent = 'изменено…';
      autosave();
    });

    async function create() {
      const name = prompt('Название заметки', 'Заметка ' + new Date().toLocaleDateString('ru-RU'));
      if (!name) return;
      const path = `${DIR}/${safeName(name)}.md`;
      await api.write(path, `# ${name}\n\n`);
      await refresh();
      await open(path);
      area.focus();
    }

    async function remove() {
      if (!current) return;
      const ok = await window.desktop.wm.confirm(`Удалить заметку «${titleEl.textContent}»?`, { ok: 'Удалить', danger: true });
      if (!ok) return;
      await api.exec(`rm -f '${current.replace(/'/g, `'\\''`)}'`);
      current = null;
      area.value = '';
      titleEl.textContent = '—';
      meta.textContent = '—';
      await refresh();
      win.notify('Удалено', 'заметка удалена', 'ok');
    }

    function setMode(mode) {
      body.querySelectorAll('.note-mode button').forEach((b) => b.classList.toggle('on', b.dataset.mode === mode));
      const editing = mode === 'edit';
      area.classList.toggle('hidden', !editing);
      view.classList.toggle('hidden', editing);
      if (!editing) {
        view.innerHTML = miniMarkdown(area.value);
      }
    }

    body.querySelectorAll('.note-mode button').forEach((b) => {
      b.onclick = () => setMode(b.dataset.mode);
    });
    body.querySelector('[data-a="new"]').onclick = create;
    body.querySelector('[data-a="save"]').onclick = save;
    body.querySelector('[data-a="del"]').onclick = remove;

    area.addEventListener('keydown', (e) => {
      if (e.key === 's' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); save(); }
      if (e.key === 'Tab') {
        e.preventDefault();
        const s = area.selectionStart;
        area.setRangeText('  ', s, area.selectionEnd, 'end');
      }
    });

    refresh();
    win.spec.onClose = () => { if (dirty) save(); };
  },
};

/* Minimal markdown → HTML for the preview pane. */
function miniMarkdown(src) {
  const esc2 = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  const lines = esc2(src).split('\n');
  let html = '';
  let inCode = false;
  let inList = false;
  const inline = (t) => t
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/\*([^*]+)\*/g, '<em>$1</em>')
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>')
    .replace(/~~([^~]+)~~/g, '<s>$1</s>');

  for (const raw of lines) {
    const line = raw;
    if (/^```/.test(line)) {
      if (inCode) { html += '</code></pre>'; inCode = false; }
      else { html += '<pre><code>'; inCode = true; }
      continue;
    }
    if (inCode) { html += line + '\n'; continue; }
    if (/^\s*[-*]\s+/.test(line)) {
      if (!inList) { html += '<ul>'; inList = true; }
      html += `<li>${inline(line.replace(/^\s*[-*]\s+/, ''))}</li>`;
      continue;
    }
    if (inList) { html += '</ul>'; inList = false; }
    if (/^\s*$/.test(line)) { html += ''; continue; }
    const h = line.match(/^(#{1,4})\s+(.*)$/);
    if (h) { html += `<h${h[1].length}>${inline(h[2])}</h${h[1].length}>`; continue; }
    if (/^>\s?/.test(line)) { html += `<blockquote>${inline(line.replace(/^>\s?/, ''))}</blockquote>`; continue; }
    if (/^(-{3,}|\*{3,})$/.test(line.trim())) { html += '<hr>'; continue; }
    html += `<p>${inline(line)}</p>`;
  }
  if (inList) html += '</ul>';
  if (inCode) html += '</code></pre>';
  return html || '<div class="empty">Пусто</div>';
}
