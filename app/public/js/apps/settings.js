/* Settings — appearance, panel, privileges, diagnostics, about. */

import { api, probeTransports } from '../api.js';
import { state, set, on, WALLPAPERS, applyTheme } from '../store.js';
import { esc, humanSize, humanUptime, fmtDate } from '../util.js';
import { icons, iconHTML } from '../icons.js';

const ACCENTS = ['#79c0ff', '#7ee787', '#d2a8ff', '#e3b341', '#ff7b72', '#56d4dd', '#f778ba', '#a5d6ff'];

export default {
  id: 'settings',
  name: 'Настройки',
  icon: 'settings',
  width: 720,
  height: 620,

  mount(body, win) {
    body.innerHTML = `
      <div class="set-wrap">
        <div class="set-group">
          <h4>Внешний вид</h4>
          <div class="set-row">
            <div><div class="t">Акцентный цвет</div><div class="d">цвет иконок, ссылок и акцентов интерфейса</div></div>
            <div class="ctl"><div class="swatches"></div></div>
          </div>
          <div class="set-row">
            <div><div class="t">Обои</div><div class="d">фон рабочего стола</div></div>
            <div class="ctl"><div class="wall-swatches"></div></div>
          </div>
          <div class="set-row">
            <div><div class="t">Масштаб шрифта</div><div class="d">размер текста во всём интерфейсе</div></div>
            <div class="ctl"><input class="range" type="range" min="0.85" max="1.3" step="0.05" /><b class="scale-v"></b></div>
          </div>
        </div>

        <div class="set-group">
          <h4>Панель</h4>
          <div class="set-row">
            <div><div class="t">24-часовой формат</div><div class="d">часы в верхней панели</div></div>
            <div class="ctl"><button class="switch s24"></button></div>
          </div>
          <div class="set-row">
            <div><div class="t">Секунды в часах</div><div class="d">обновление раз в секунду</div></div>
            <div class="ctl"><button class="switch ssec"></button></div>
          </div>
        </div>

        <div class="set-group">
          <h4>Права и безопасность</h4>
          <div class="set-row">
            <div><div class="t">root mode</div><div class="d">все команды и операции с файлами выполняются через sudo -n</div></div>
            <div class="ctl"><button class="switch sroot"></button></div>
          </div>
          <div class="set-row">
            <div><div class="t">Подтверждать kill</div><div class="d">спрашивать перед завершением процесса</div></div>
            <div class="ctl"><button class="switch skill"></button></div>
          </div>
        </div>

        <div class="set-group">
          <h4>Диагностика</h4>
          <div class="set-row">
            <div><div class="t">Транспорт API</div><div class="d">проверить, какие способы запросов проходят через прокси</div></div>
            <div class="ctl"><button class="btn ghost diag">Проверить</button></div>
          </div>
          <div class="diag-log" style="font-family:var(--mono);font-size:.74em;color:var(--dim);line-height:1.7"></div>
        </div>

        <div class="set-group">
          <h4>О системе</h4>
          <div class="set-row" style="display:block">
            <div class="about-grid">
              <span class="k">рабочий стол</span><span class="v">sandboxOS 1.0</span>
              <span class="k">хост</span><span class="v a-host">—</span>
              <span class="k">ядро</span><span class="v a-kernel">—</span>
              <span class="k">процессор</span><span class="v a-cpu">—</span>
              <span class="k">память</span><span class="v a-mem">—</span>
              <span class="k">диск</span><span class="v a-disk">—</span>
              <span class="k">uptime</span><span class="v a-up">—</span>
              <span class="k">node / python</span><span class="v a-rt">—</span>
              <span class="k">git</span><span class="v a-git">—</span>
              <span class="k">серверный pid</span><span class="v a-pid">—</span>
            </div>
          </div>
          <div class="set-row">
            <div><div class="t">Скриншот рабочего стола</div><div class="d">screenshot/desktop.png</div></div>
            <div class="ctl"><button class="btn ghost shot">Открыть</button></div>
          </div>
          <div class="set-row">
            <div><div class="t">Сбросить настройки</div><div class="d">обои, акцент, закладки вернутся к исходным</div></div>
            <div class="ctl"><button class="btn ghost reset">Сбросить</button></div>
          </div>
        </div>
      </div>`;

    const $ = (s) => body.querySelector(s);

    // accents
    const sw = $('.swatches');
    for (const c of ACCENTS) {
      const b = document.createElement('button');
      b.className = 'swatch' + (state.accent === c ? ' active' : '');
      b.style.background = c;
      b.title = c;
      b.onclick = () => {
        set('accent', c);
        applyTheme();
        sw.querySelectorAll('.swatch').forEach((n) => n.classList.toggle('active', n.style.background === c || n.title === c));
      };
      sw.appendChild(b);
    }

    // wallpapers
    const ws = $('.wall-swatches');
    for (const w of WALLPAPERS) {
      const b = document.createElement('button');
      b.className = 'wall-swatch' + (state.wallpaper === w.id ? ' active' : '');
      b.style.background = w.css;
      b.title = w.name;
      b.onclick = () => {
        set('wallpaper', w.id);
        applyTheme();
        ws.querySelectorAll('.wall-swatch').forEach((n) => n.classList.toggle('active', n.title === w.name));
      };
      ws.appendChild(b);
    }

    // font scale
    const range = $('.range');
    const scaleV = $('.scale-v');
    range.value = state.fontScale;
    scaleV.textContent = Math.round(state.fontScale * 100) + '%';
    range.oninput = () => {
      const v = Number(range.value);
      set('fontScale', v);
      scaleV.textContent = Math.round(v * 100) + '%';
      applyTheme();
    };

    // switches
    const bindSwitch = (sel, key, after) => {
      const btn = $(sel);
      const paint = () => btn.classList.toggle('on', !!state[key]);
      btn.onclick = () => {
        set(key, !state[key]);
        paint();
        after?.();
      };
      paint();
    };
    bindSwitch('.s24', 'clock24');
    bindSwitch('.ssec', 'showSeconds');
    bindSwitch('.skill', 'confirmKill');
    bindSwitch('.sroot', 'root', () => {
      window.desktop.wm.notify(state.root ? 'root mode включён' : 'root mode выключен', state.root ? 'команды выполняются через sudo -n' : 'обычный пользователь', state.root ? 'warn' : 'info');
    });

    // diagnostics
    $('.diag').onclick = async () => {
      const log = $('.diag-log');
      log.innerHTML = 'проверяю…';
      const res = await probeTransports();
      log.innerHTML = res.map((r) => r.ok
        ? `<div style="color:var(--green)">✓ ${r.method} ${esc(r.url.split('?')[0])} — ${r.ms} мс, pid ${r.pid}</div>`
        : `<div style="color:var(--red)">✗ ${r.method} ${esc(r.url.split('?')[0])} — ${esc(r.error)}</div>`).join('');
    };

    $('.shot').onclick = () => window.desktop.launch('viewer', { path: '/home/user/pr/screenshot/desktop.png' });
    $('.reset').onclick = async () => {
      const ok = await window.desktop.wm.confirm('Сбросить все настройки рабочего стола?', { ok: 'Сбросить', danger: true });
      if (!ok) return;
      localStorage.removeItem('sandbox-desktop:v1');
      location.reload();
    };

    const fillAbout = async () => {
      try {
        const s = await api.stats();
        $('.a-host').textContent = `${s.host} · ${s.user}`;
        $('.a-kernel').textContent = s.kernel;
        $('.a-cpu').textContent = `${s.cores}× ${s.cpuModel}`;
        $('.a-mem').textContent = `${humanSize(s.mem.used)} / ${humanSize(s.mem.total)}`;
        $('.a-disk').textContent = `${humanSize(s.disk.used)} / ${humanSize(s.disk.total)} (${s.disk.pct}%)`;
        $('.a-up').textContent = humanUptime(s.uptime);
        $('.a-rt').textContent = `${s.node} · ${s.python}`;
        $('.a-git').textContent = `${s.git.branch} · ${s.git.head.slice(0, 7)}${s.git.dirty ? ' · dirty' : ''}`;
        $('.a-pid').textContent = String(s.pid);
      } catch (e) {
        $('.a-host').textContent = 'нет связи с сервером';
      }
    };
    fillAbout();
  },
};
