/* System monitor — live load, memory, disk and a real process table. */

import { api } from '../api.js';
import { state } from '../store.js';
import { esc, humanSize, humanUptime } from '../util.js';
import { icons } from '../icons.js';

export default {
  id: 'monitor',
  name: 'Системный монитор',
  icon: 'monitor',
  width: 880,
  height: 560,

  mount(body, win) {
    body.innerHTML = `
      <div class="mon-wrap">
        <div class="mon-cards">
          <div class="mon-card">
            <div class="lb">процессор</div>
            <div class="vl"><span class="cpu-v">0%</span></div>
            <div class="mini-bar"><i class="cpu-b" style="width:0%"></i></div>
            <div class="lb" style="margin-top:6px"><span class="cpu-m">—</span></div>
          </div>
          <div class="mon-card">
            <div class="lb">память</div>
            <div class="vl"><span class="mem-v">—</span></div>
            <div class="mini-bar"><i class="mem-b" style="width:0%"></i></div>
            <div class="lb" style="margin-top:6px"><span class="mem-m">—</span></div>
          </div>
          <div class="mon-card">
            <div class="lb">диск /</div>
            <div class="vl"><span class="dsk-v">—</span></div>
            <div class="mini-bar"><i class="dsk-b" style="width:0%"></i></div>
            <div class="lb" style="margin-top:6px"><span class="dsk-m">—</span></div>
          </div>
        </div>
        <div class="toolbar" style="border-top:1px solid rgba(255,255,255,.06)">
          <span style="color:var(--dim);font-size:.8em">Процессы (топ по CPU)</span>
          <span class="grow"></span>
          <button class="tb-btn" data-a="refresh" title="Обновить">${icons.refresh}</button>
        </div>
        <div class="mon-table">
          <div class="mrow head">
            <span>PID</span><span>Польз.</span><span>Команда</span>
            <span>CPU</span><span>MEM</span><span>RSS</span><span></span>
          </div>
          <div class="mon-rows"></div>
        </div>
        <div class="statusbar">
          <span class="st-info">—</span>
          <span class="grow"></span>
          <span class="st-root"></span>
        </div>
      </div>`;

    const $ = (s) => body.querySelector(s);
    const rowsEl = $('.mon-rows');
    let alive = true;
    let timer = null;
    let tableBusy = false;

    const paintStats = (s) => {
      $('.cpu-v').textContent = `${s.cpu}%`;
      const cb = $('.cpu-b');
      cb.style.width = Math.max(2, s.cpu) + '%';
      cb.classList.toggle('hot', s.cpu > 80);
      $('.cpu-m').textContent = `${s.cores} потока · load ${s.load.join(' / ')}`;
      const memPct = (s.mem.used / s.mem.total) * 100;
      $('.mem-v').textContent = humanSize(s.mem.used);
      $('.mem-b').style.width = Math.max(2, memPct) + '%';
      $('.mem-m').textContent = `из ${humanSize(s.mem.total)} · ${memPct.toFixed(0)}%`;
      $('.dsk-v').textContent = `${s.disk.pct}%`;
      $('.dsk-b').style.width = Math.max(2, s.disk.pct) + '%';
      $('.dsk-m').textContent = `${humanSize(s.disk.used)} из ${humanSize(s.disk.total)}`;
      $('.st-info').textContent = `${s.host} · ${s.kernel} · uptime ${humanUptime(s.uptime)}`;
      $('.st-root').textContent = state.root ? 'kill через sudo' : 'kill как user';
    };

    async function paintProcesses() {
      if (tableBusy) return;
      tableBusy = true;
      try {
        const data = await api.proc();
        const rows = data.rows.slice(0, 60);
        rowsEl.innerHTML = rows.map((p) => `
          <div class="mrow" data-pid="${p.pid}">
            <span>${p.pid}</span>
            <span style="color:var(--dim)">${esc(p.user)}</span>
            <span class="cmd" title="${esc(p.args)}">${esc(p.comm)}</span>
            <span>${p.cpu.toFixed(1)}</span>
            <span>${p.mem.toFixed(1)}</span>
            <span>${humanSize(p.rss)}</span>
            <button class="kill" title="Завершить процесс">${icons.close}</button>
          </div>`).join('');
        rowsEl.querySelectorAll('.kill').forEach((btn) => {
          btn.onclick = async (e) => {
            e.stopPropagation();
            const pid = Number(btn.closest('.mrow').dataset.pid);
            if (state.confirmKill) {
              const ok = await window.desktop.wm.confirm(`Завершить процесс ${pid}?`, { ok: 'Завершить', danger: true });
              if (!ok) return;
            }
            try {
              const r = await api.killproc(pid, { root: state.root });
              win.notify(r.ok ? 'Сигнал отправлен' : 'Не удалось', `pid ${pid} · ${r.out || 'SIGTERM'}`, r.ok ? 'ok' : 'error');
              paintProcesses();
            } catch (err) {
              win.notify('Ошибка', String(err.message || err), 'error');
            }
          };
        });
      } catch {} finally {
        tableBusy = false;
      }
    }

    async function tick() {
      if (!alive) return;
      try { paintStats(await api.stats()); } catch {}
      paintProcesses();
      timer = setTimeout(tick, 2500);
    }

    body.querySelector('[data-a="refresh"]').onclick = () => { paintProcesses(); };
    win.spec.onClose = () => { alive = false; clearTimeout(timer); };
    tick();
  },
};
