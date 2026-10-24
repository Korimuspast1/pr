/* Disk usage analyzer — real `du` + `find` output. */

import { api } from '../api.js';
import { esc, humanSize } from '../util.js';
import { icons } from '../icons.js';

const shq = (s) => "'" + String(s).replace(/'/g, `'\\''`) + "'";

export default {
  id: 'disk',
  name: 'Анализ диска',
  icon: 'disk',
  width: 780,
  height: 560,

  mount(body, win) {
    let path = '/home/user';

    body.innerHTML = `
      <div class="disk-wrap">
        <div class="toolbar">
          <button class="tb-btn" data-a="up" title="Вверх">${icons.arrowUp}</button>
          <button class="tb-btn" data-a="scan" title="Сканировать">${icons.refresh}</button>
          <input class="field disk-path" spellcheck="false" />
          <button class="tb-btn" data-a="go" title="Перейти">${icons.chevronRight}</button>
        </div>
        <div class="disk-summary">
          <div class="disk-total"><span class="d-size">—</span><span class="d-label">всего в папке</span></div>
          <div class="disk-quota">
            <div class="mini-bar"><i style="width:0%"></i></div>
            <div class="d-quota">—</div>
          </div>
        </div>
        <div class="disk-cols">
          <div class="disk-col">
            <div class="disk-col-title">Папки</div>
            <div class="disk-dirs"></div>
          </div>
          <div class="disk-col">
            <div class="disk-col-title">Самые большие файлы</div>
            <div class="disk-files"></div>
          </div>
        </div>
        <div class="statusbar"><span class="st-msg">готов</span><span class="grow"></span><span class="st-path">—</span></div>
      </div>`;

    const $ = (s) => body.querySelector(s);
    const pathInput = $('.disk-path');
    const dirsEl = $('.disk-dirs');
    const filesEl = $('.disk-files');
    const msg = $('.st-msg');

    const parseDu = (out) =>
      out.split('\n').map((l) => l.trim()).filter(Boolean).map((l) => {
        const m = l.match(/^(\d+)\s+(.+)$/);
        return m ? { size: Number(m[1]), name: m[2] } : null;
      }).filter(Boolean);

    async function scan(p = path) {
      path = p;
      pathInput.value = p;
      msg.textContent = 'сканирую…';
      dirsEl.innerHTML = '<div class="ch-empty">…</div>';
      filesEl.innerHTML = '<div class="ch-empty">…</div>';

      try {
        const [du, big, df, st] = await Promise.all([
          api.exec(`du -sb ${shq(p)}/* 2>/dev/null | sort -rn | head -n 14`, { root: false }),
          api.exec(`find ${shq(p)} -xdev -type f -size +256k -printf '%s\\t%p\\n' 2>/dev/null | sort -rn | head -n 14`),
          api.exec(`df -B1 / | tail -1`),
          api.exec(`du -sb ${shq(p)} 2>/dev/null | cut -f1`),
        ]);

        const dirs = parseDu(du.out).map((d) => ({ ...d, name: d.name.replace(new RegExp('^' + p.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '/?'), '') }));
        const max = Math.max(1, ...dirs.map((d) => d.size));
        dirsEl.innerHTML = dirs.length
          ? dirs.map((d) => `
            <div class="disk-row" title="${esc(p + '/' + d.name)}">
              <span class="dr-name">${esc(d.name)}</span>
              <span class="dr-size">${humanSize(d.size)}</span>
              <span class="dr-bar"><i style="width:${Math.max(2, (d.size / max) * 100)}%"></i></span>
            </div>`).join('')
          : '<div class="ch-empty">пусто или нет доступа</div>';
        dirsEl.querySelectorAll('.disk-row').forEach((row, i) => {
          row.onclick = () => scan((p === '/' ? '' : p) + '/' + dirs[i].name);
        });

        const files = big.out.split('\n').map((l) => {
          const [size, ...rest] = l.split('\t');
          return rest.length ? { size: Number(size), name: rest.join('\t').replace(p === '/' ? '/' : p + '/', '') } : null;
        }).filter(Boolean);
        const maxF = Math.max(1, ...files.map((f) => f.size));
        filesEl.innerHTML = files.length
          ? files.map((f) => `
            <div class="disk-row" title="${esc(f.name)}">
              <span class="dr-name">${esc(f.name)}</span>
              <span class="dr-size">${humanSize(f.size)}</span>
              <span class="dr-bar"><i style="width:${Math.max(2, (f.size / maxF) * 100)}%"></i></span>
            </div>`).join('')
          : '<div class="ch-empty">крупных файлов нет</div>';

        const total = Number((st.out || '0').trim()) || dirs.reduce((a, b) => a + b.size, 0);
        $('.d-size').textContent = humanSize(total);
        const dfParts = (df.out || '').trim().split(/\s+/);
        const usedPct = Number(String(dfParts[4] || '0').replace('%', ''));
        $('.disk-summary .mini-bar i').style.width = Math.max(2, usedPct) + '%';
        $('.d-quota').textContent = `раздел / — ${dfParts[2] ? humanSize(Number(dfParts[2])) : '—'} из ${dfParts[1] ? humanSize(Number(dfParts[1])) : '—'} (${usedPct}%)`;
        $('.st-path').textContent = p;
        msg.textContent = 'готово';
        win.setTitle(`Анализ диска — ${p}`);
      } catch (e) {
        msg.textContent = String(e.message || e);
      }
    }

    body.querySelector('[data-a="up"]').onclick = () => scan(path.replace(/\/[^/]+\/?$/, '') || '/');
    body.querySelector('[data-a="scan"]').onclick = () => scan(path);
    body.querySelector('[data-a="go"]').onclick = () => scan(pathInput.value || '/');
    pathInput.onkeydown = (e) => { if (e.key === 'Enter') scan(pathInput.value || '/'); };

    win.spec.onArgs = (args) => { if (args?.path) scan(args.path); };
    scan(path);
  },
};
