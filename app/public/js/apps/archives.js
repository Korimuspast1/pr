/* Archives — real tar/gz/zip operations through the shell. */

import { api } from '../api.js';
import { state } from '../store.js';
import { esc, humanSize, fmtDate } from '../util.js';
import { icons } from '../icons.js';

const shq = (s) => "'" + String(s).replace(/'/g, `'\\''`) + "'";
const isArchive = (n) => /\.(zip|tar|tar\.gz|tgz|tar\.bz2|tbz2|tar\.xz|txz|gz|7z|rar)$/i.test(n);

export default {
  id: 'archives',
  name: 'Архивы',
  icon: 'archive',
  width: 820,
  height: 540,

  mount(body, win) {
    let path = '/home/user';
    let items = [];
    let log = '';

    body.innerHTML = `
      <div class="arc-wrap">
        <div class="toolbar">
          <button class="tb-btn" data-a="up" title="Вверх">${icons.arrowUp}</button>
          <button class="tb-btn" data-a="refresh" title="Обновить">${icons.refresh}</button>
          <input class="field arc-path" spellcheck="false" />
          <button class="tb-btn" data-a="go" title="Перейти">${icons.chevronRight}</button>
        </div>
        <div class="arc-body">
          <div class="arc-list">
            <div class="frow head"><span></span><span>Имя</span><span class="sz">Размер</span><span class="dt">Изменён</span></div>
            <div class="arc-rows"></div>
          </div>
          <div class="arc-side">
            <div class="side-title">Создать архив</div>
            <input class="field arc-name" placeholder="имя (без расширения)" />
            <div class="arc-actions">
              <button class="btn ghost" data-c="tar.gz">tar.gz</button>
              <button class="btn ghost" data-c="zip">zip</button>
            </div>
            <div class="side-title" style="margin-top:14px">Действия</div>
            <div class="arc-actions">
              <button class="btn ghost" data-e="extract">Извлечь выбранное</button>
              <button class="btn ghost" data-e="list">Список содержимого</button>
              <button class="btn ghost" data-e="download">Скачать</button>
            </div>
            <div class="arc-hint">Выбери папку слева и нажми tar.gz/zip — архив появится рядом.</div>
          </div>
        </div>
        <pre class="arc-log">— вывод команд будет тут —</pre>
        <div class="statusbar"><span class="st-msg">готов</span><span class="grow"></span><span class="st-sel">—</span></div>
      </div>`;

    const $ = (s) => body.querySelector(s);
    const rowsEl = $('.arc-rows');
    const logEl = $('.arc-log');
    const pathInput = $('.arc-path');
    const nameInput = $('.arc-name');
    const msg = $('.st-msg');
    const selEl = $('.st-sel');
    let selected = null;

    const full = (n) => (path === '/' ? '' : path) + '/' + n;

    const setLog = (text) => {
      log = text || '';
      logEl.textContent = log || '— вывод команд будет тут —';
      logEl.scrollTop = logEl.scrollHeight;
    };

    async function load(p = path) {
      try {
        const d = await api.files(p);
        if (d.error) throw new Error(d.error);
        path = d.path;
        items = d.items;
        pathInput.value = path;
        selected = null;
        paint();
      } catch (e) {
        msg.textContent = String(e.message || e);
      }
    }

    function paint() {
      rowsEl.innerHTML = '';
      if (!items.length) {
        rowsEl.innerHTML = '<div class="empty">Пусто</div>';
        return;
      }
      for (const it of items) {
        const row = document.createElement('div');
        row.className = 'frow' + (it.dir ? ' dir' : '') + (it.name === selected ? ' sel' : '');
        const tag = isArchive(it.name) ? ' 🗜' : '';
        row.innerHTML = `<span class="ico">${it.dir ? icons.folder : icons.archive}</span>
          <span class="nm">${esc(it.name)}${tag}</span>
          <span class="sz">${it.dir ? '—' : humanSize(it.size)}</span>
          <span class="dt">${fmtDate(it.mtime)}</span>`;
        row.onclick = () => {
          selected = it.name;
          selEl.textContent = full(it.name);
          paint();
        };
        row.ondblclick = () => {
          if (it.dir) load(full(it.name));
          else if (isArchive(it.name)) listArchive(full(it.name));
        };
        rowsEl.appendChild(row);
      }
      selEl.textContent = selected ? full(selected) : '—';
      win.setTitle(`Архивы — ${path}`);
    }

    async function run(cmd) {
      msg.textContent = 'выполняется…';
      setLog(`$ ${cmd}`);
      const r = await api.exec(cmd, { root: state.root });
      setLog(`$ ${cmd}\n${r.out || '(нет вывода)'}\nexit ${r.code}`);
      msg.textContent = r.code === 0 ? 'готово' : `код ${r.code}`;
      if (r.code !== 0) win.notify('Команда завершилась с ошибкой', `exit ${r.code}`, 'error');
      return r;
    }

    async function listArchive(p) {
      const isZip = /\.zip$/i.test(p);
      await run(isZip ? `unzip -l ${shq(p)} | head -n 80` : `tar -tvf ${shq(p)} 2>&1 | head -n 80`);
    }

    async function create(kind) {
      const name = (nameInput.value || 'archive').replace(/[^\w.-]+/g, '_');
      if (!selected) return win.notify('Ничего не выбрано', 'Кликни по папке или файлу слева', 'warn');
      const target = full(selected);
      const out = `${path === '/' ? '' : path}/${name}.${kind}`;
      const cmd = kind === 'zip'
        ? `cd ${shq(path)} && zip -r ${shq(out)} ${shq(selected)} | tail -n 5`
        : `cd ${shq(path)} && tar -czf ${shq(out)} ${shq(selected)} && ls -lh ${shq(out)}`;
      await run(cmd);
      await load(path);
    }

    async function extract() {
      if (!selected) return win.notify('Ничего не выбрано', 'Кликни по архиву слева', 'warn');
      const p = full(selected);
      if (!isArchive(selected)) return win.notify('Это не архив', selected, 'warn');
      const dir = `${p.replace(/\.(zip|tar|tar\.gz|tgz|tar\.bz2|tbz2|tar\.xz|txz|gz)$/i, '')}_extracted`;
      const isZip = /\.zip$/i.test(p);
      await run(isZip
        ? `mkdir -p ${shq(dir)} && unzip -o ${shq(p)} -d ${shq(dir)} | tail -n 5`
        : `mkdir -p ${shq(dir)} && tar -xvf ${shq(p)} -C ${shq(dir)} 2>&1 | tail -n 8`);
      await load(path);
      win.notify('Распаковано', dir, 'ok');
    }

    body.querySelector('[data-a="up"]').onclick = () => load(path.replace(/\/[^/]+\/?$/, '') || '/');
    body.querySelector('[data-a="refresh"]').onclick = () => load(path);
    body.querySelector('[data-a="go"]').onclick = () => load(pathInput.value);
    pathInput.onkeydown = (e) => { if (e.key === 'Enter') load(pathInput.value); };
    body.querySelectorAll('[data-c]').forEach((b) => { b.onclick = () => create(b.dataset.c); });
    body.querySelector('[data-e="extract"]').onclick = extract;
    body.querySelector('[data-e="list"]').onclick = () => {
      if (!selected) return win.notify('Ничего не выбрано', '', 'warn');
      listArchive(full(selected));
    };
    body.querySelector('[data-e="download"]').onclick = () => {
      if (!selected) return win.notify('Ничего не выбрано', '', 'warn');
      const a = document.createElement('a');
      a.href = api.downloadUrl(full(selected));
      a.download = selected;
      a.click();
    };

    win.spec.onArgs = (args) => { if (args?.path) load(args.path); };
    load(path);
  },
};
