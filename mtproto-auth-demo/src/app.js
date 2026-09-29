// Интерфейс демонстрации: тянет шаги из runHandshake() и рисует карточки,
// hex-дампы сообщений и панель состояния сессии.

import { hex } from './bytes.js';
import { runHandshake } from './handshake.js';

const TOTAL_STEPS = 11;
const FIELD_COLORS = ['#38bdf8', '#34d399', '#f472b6', '#fbbf24', '#a78bfa', '#fb923c', '#22d3ee', '#f87171', '#4ade80', '#c084fc'];

const el = {
  run: document.getElementById('btn-run'),
  step: document.getElementById('btn-step'),
  reset: document.getElementById('btn-reset'),
  auto: document.getElementById('chk-auto'),
  speed: document.getElementById('rng-speed'),
  speedOut: document.getElementById('out-speed'),
  steps: document.getElementById('steps'),
  placeholder: document.getElementById('placeholder'),
  progress: document.getElementById('progress'),
  stateList: document.getElementById('state-list'),
  resultPanel: document.getElementById('result-panel'),
  resultBody: document.getElementById('result-body'),
  packet: document.getElementById('packet'),
  wireLabel: document.getElementById('wire-label'),
  peerClient: document.getElementById('peer-client'),
  peerServer: document.getElementById('peer-server'),
  clientSecret: document.getElementById('peer-client-secret'),
  serverSecret: document.getElementById('peer-server-secret'),
  toast: document.getElementById('toast'),
};

const app = {
  iterator: null,
  running: false,
  finished: false,
  autoTimer: null,
  stateEntries: new Map(),
};

/* ───────────────────────── прогресс ───────────────────────── */
function buildProgress() {
  el.progress.innerHTML = '';
  for (let i = 0; i < TOTAL_STEPS; i++) {
    const s = document.createElement('span');
    s.title = `Шаг ${i}`;
    el.progress.append(s);
  }
}

function markProgress(n) {
  [...el.progress.children].forEach((node, i) => {
    node.classList.toggle('done', i <= n);
    node.classList.toggle('current', i === n);
  });
}

/* ───────────────────────── утилиты ───────────────────────── */
function showToast(text) {
  el.toast.textContent = text;
  el.toast.classList.add('show');
  clearTimeout(showToast.t);
  showToast.t = setTimeout(() => el.toast.classList.remove('show'), 1600);
}

async function copyValue(text) {
  try {
    await navigator.clipboard.writeText(text);
    showToast('Значение скопировано');
  } catch {
    showToast('Не удалось скопировать');
  }
}

function colorizeSchema(text) {
  return text
    .split('\n')
    .map((line) =>
      line.replace(/^([a-zA-Z_]+#[0-9a-f]+)/, '<span class="ctor">$1</span>')
    )
    .join('\n');
}

/* ───────────────────────── hex-дамп ───────────────────────── */
function renderDump({ bytes, fields, label, direction }) {
  const wrap = document.createElement('div');
  wrap.className = 'dump';

  const head = document.createElement('div');
  head.className = 'dump-head';
  const arrow = direction === 'up' ? 'client → server' : direction === 'down' ? 'server → client' : 'внутренняя структура';
  head.innerHTML = `<span><strong>${label}</strong> · ${arrow} · ${bytes.length} байт</span>`;
  const toggle = document.createElement('button');
  toggle.className = 'dump-toggle';
  toggle.textContent = 'развернуть дамп';
  head.append(toggle);
  wrap.append(head);

  const legend = document.createElement('div');
  legend.className = 'dump-legend';
  wrap.append(legend);

  const body = document.createElement('div');
  body.className = 'dump-body collapsed';
  wrap.append(body);

  const tip = document.createElement('div');
  tip.className = 'dump-tip';
  tip.textContent = 'Наведите на поле, чтобы подсветить его байты.';
  wrap.append(tip);

  // карта байт → поле
  const owner = new Int16Array(bytes.length).fill(-1);
  fields.forEach((f, i) => {
    for (let k = f.offset; k < f.offset + f.length && k < bytes.length; k++) owner[k] = i;
  });

  const rows = [];
  for (let off = 0; off < bytes.length; off += 16) {
    const row = document.createElement('div');
    row.className = 'dump-row';
    const offSpan = document.createElement('span');
    offSpan.className = 'dump-off';
    offSpan.textContent = off.toString(16).padStart(4, '0').toUpperCase() + ' │ ';
    row.append(offSpan);
    for (let i = off; i < Math.min(off + 16, bytes.length); i++) {
      const b = document.createElement('span');
      const fi = owner[i];
      b.className = 'byte';
      if (fi >= 0) {
        b.dataset.f = String(fi);
        b.dataset.color = FIELD_COLORS[fi % FIELD_COLORS.length];
        b.style.color = b.dataset.color;
      } else {
        b.style.color = '#64748b';
      }
      b.textContent = bytes[i].toString(16).padStart(2, '0').toUpperCase();
      row.append(b);
      row.append(document.createTextNode(i % 16 === 7 ? '  ' : ' '));
    }
    rows.push(row);
    body.append(row);
  }

  fields.forEach((f, i) => {
    const item = document.createElement('span');
    item.className = 'legend-item';
    const color = FIELD_COLORS[i % FIELD_COLORS.length];
    item.style.color = color;
    item.style.borderColor = color + '55';
    item.style.background = color + '12';
    item.textContent = f.name;
    item.dataset.f = String(i);
    item.addEventListener('mouseenter', () => {
      body.querySelectorAll(`.byte[data-f="${i}"]`).forEach((n) => {
        n.classList.add('hl');
        n.style.background = n.dataset.color;
        n.style.color = '#060a12';
      });
      const value = f.display && f.display.length > 96 ? f.display.slice(0, 96) + '…' : f.display;
      tip.innerHTML = `<strong style="color:${color}">${f.name}</strong>${f.type ? ` : ${f.type}` : ''} — смещение ${f.offset}, длина ${f.length} байт${
        value ? ` · <span style="color:#dbe6fb">${value}</span>` : ''
      }${f.comment ? ` · ${f.comment}` : ''}`;
    });
    item.addEventListener('mouseleave', () => {
      body.querySelectorAll('.byte.hl').forEach((n) => {
        n.classList.remove('hl');
        n.style.background = '';
        n.style.color = n.dataset.color;
      });
      tip.textContent = 'Наведите на поле, чтобы подсветить его байты.';
    });
    item.addEventListener('click', () => copyValue(f.display || ''));
    legend.append(item);
  });

  toggle.addEventListener('click', () => {
    const collapsed = body.classList.toggle('collapsed');
    toggle.textContent = collapsed ? 'развернуть дамп' : 'свернуть дамп';
  });

  if (rows.length <= 6) {
    body.classList.remove('collapsed');
    toggle.remove();
  }

  return wrap;
}

/* ───────────────────────── карточка шага ───────────────────────── */
function renderStep(step) {
  const card = document.createElement('article');
  card.className = `step ${step.actor}`;

  const head = document.createElement('div');
  head.className = 'step-head';
  head.innerHTML = `
    <div class="step-num">${step.n}</div>
    <div>
      <h3 class="step-title">${step.title}</h3>
      <div class="step-chips">
        <span class="chip ${step.actor}">${step.actor === 'client' ? 'клиент' : 'сервер'}</span>
        <span class="chip">${step.kind === 'send' ? 'сообщение' : 'вычисление'}</span>
        ${step.message ? `<span class="chip size">${step.message.bytes.length} байт</span>` : ''}
      </div>
    </div>`;
  card.append(head);

  const body = document.createElement('div');
  body.className = 'step-body';

  if (step.intro) {
    const p = document.createElement('p');
    p.className = 'step-intro';
    p.textContent = step.intro;
    body.append(p);
  }

  if (step.schema) {
    const pre = document.createElement('pre');
    pre.className = 'schema';
    pre.innerHTML = colorizeSchema(step.schema);
    body.append(pre);
  }

  if (step.trace?.length) {
    const trace = document.createElement('div');
    trace.className = 'trace';
    for (const row of step.trace) {
      const r = document.createElement('div');
      r.className = 'trace-row' + (row.highlight ? ' highlight' : '');
      const k = document.createElement('div');
      k.className = 'trace-key';
      k.textContent = row.label;
      const v = document.createElement('div');
      v.className = 'trace-val' + (row.long || String(row.value).length > 180 ? ' long' : '');
      v.textContent = row.value;
      v.title = 'Клик — скопировать, двойной клик — развернуть';
      v.addEventListener('click', () => {
        if (v.classList.contains('long') && !v.classList.contains('open')) v.classList.add('open');
        else copyValue(String(row.value));
      });
      r.append(k, v);
      trace.append(r);
    }
    body.append(trace);
  }

  if (step.inner) {
    body.append(renderDump({ ...step.inner, label: step.inner.label, direction: 'inner' }));
  }

  if (step.message) {
    body.append(renderDump(step.message));
  }

  card.append(body);
  return card;
}

/* ───────────────────────── панель состояния ───────────────────────── */
const STATE_LABELS = {
  rsa_fingerprint: 'отпечаток RSA-ключа сервера',
  nonce: 'nonce (клиент)',
  server_nonce: 'server_nonce',
  pq: 'pq',
  p: 'p',
  q: 'q',
  new_nonce: 'new_nonce',
  g: 'g',
  dh_prime: 'dh_prime',
  a: 'a',
  b: 'b',
  g_a: 'g_a',
  g_b: 'g_b',
  auth_key_client: 'auth_key (клиент)',
  auth_key_server: 'auth_key (сервер)',
  auth_key_id: 'auth_key_id',
  server_salt: 'server_salt',
};

function updateState(reveal) {
  if (!reveal) return;
  for (const [key, info] of Object.entries(reveal)) {
    app.stateEntries.set(key, info);
  }
  el.stateList.innerHTML = '';
  if (app.stateEntries.size === 0) {
    el.stateList.innerHTML = '<p class="empty">Пока ничего не известно ни одной из сторон.</p>';
    return;
  }
  for (const [key, info] of app.stateEntries) {
    const item = document.createElement('div');
    item.className = `state-item ${info.owner}`;
    const name = document.createElement('div');
    name.className = 'state-name';
    name.innerHTML = `<span>${STATE_LABELS[key] ?? key}</span>${info.secret ? '<span class="lock">секрет</span>' : ''}`;
    const value = document.createElement('div');
    value.className = 'state-value' + (info.long || info.value.length > 90 ? ' long' : '');
    value.textContent = info.value;
    value.addEventListener('click', () => {
      if (value.classList.contains('long') && !value.classList.contains('open')) value.classList.add('open');
      else copyValue(info.value);
    });
    item.append(name, value);
    el.stateList.append(item);
  }
}

function updatePeers(step) {
  el.peerClient.classList.toggle('active', step.actor === 'client');
  el.peerServer.classList.toggle('active', step.actor === 'server');
  if (step.reveal?.b) el.clientSecret.textContent = 'b — 2048-битный секрет';
  if (step.reveal?.a) el.serverSecret.textContent = 'a — 2048-битный секрет';
  if (step.message) {
    const dir = step.message.direction;
    el.packet.className = 'packet';
    void el.packet.offsetWidth; // перезапуск анимации
    el.packet.classList.add(dir);
    el.wireLabel.textContent = `${step.message.label} · ${step.message.bytes.length} байт`;
  } else {
    el.wireLabel.textContent = step.actor === 'client' ? 'клиент считает…' : 'сервер считает…';
  }
}

function renderResult(result) {
  el.resultPanel.hidden = false;
  el.resultBody.innerHTML = `
    <div class="match-banner">${result.keysMatch ? '✓ Ключи совпали' : '✗ Ключи различаются'}</div>
    <div class="result-line"><span>auth_key (2048 бит)</span><code>${result.authKey}</code></div>
    <div class="result-line"><span>auth_key_id</span><code>${result.authKeyId}</code></div>
    <div class="result-line"><span>server_salt</span><code>${result.serverSalt}</code></div>`;
  el.resultBody.querySelectorAll('code').forEach((c) => {
    c.style.cursor = 'pointer';
    c.addEventListener('click', () => copyValue(c.textContent));
  });
  el.clientSecret.textContent = `auth_key_id ${result.authKeyId}`;
  el.serverSecret.textContent = `auth_key_id ${result.authKeyId}`;
}

/* ───────────────────────── управление прогоном ───────────────────────── */
async function nextStep() {
  if (!app.iterator || app.running || app.finished) return;
  app.running = true;
  el.step.disabled = true;
  try {
    const { value: step, done } = await app.iterator.next();
    if (done) {
      finish();
      return;
    }
    el.placeholder?.remove();
    const card = renderStep(step);
    el.steps.append(card);
    markProgress(step.n);
    updateState(step.reveal);
    updatePeers(step);
    if (step.result) renderResult(step.result);
    card.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  } catch (error) {
    console.error(error);
    showToast('Ошибка: ' + error.message);
    finish();
    return;
  } finally {
    app.running = false;
    el.step.disabled = app.finished;
  }

  if (el.auto.checked && !app.finished) {
    clearTimeout(app.autoTimer);
    app.autoTimer = setTimeout(nextStep, Number(el.speed.value));
  }
}

function finish() {
  app.finished = true;
  el.step.disabled = true;
  el.run.disabled = true;
  el.run.textContent = '✓ Рукопожатие завершено';
  el.wireLabel.textContent = 'auth_key согласован';
  el.peerClient.classList.add('active');
  el.peerServer.classList.add('active');
  clearTimeout(app.autoTimer);
}

function start() {
  reset(false);
  app.iterator = runHandshake();
  app.finished = false;
  el.run.disabled = true;
  el.run.textContent = 'Выполняется…';
  el.step.disabled = false;
  el.reset.disabled = false;
  nextStep();
}

function reset(full = true) {
  clearTimeout(app.autoTimer);
  app.iterator = null;
  app.finished = false;
  app.running = false;
  app.stateEntries.clear();
  el.steps.innerHTML = '';
  el.resultPanel.hidden = true;
  el.stateList.innerHTML = '<p class="empty">Пока ничего не известно ни одной из сторон.</p>';
  el.clientSecret.textContent = 'b — неизвестно';
  el.serverSecret.textContent = 'a — неизвестно';
  el.peerClient.classList.remove('active');
  el.peerServer.classList.remove('active');
  el.wireLabel.textContent = 'соединение не установлено';
  buildProgress();
  if (full) {
    el.run.disabled = false;
    el.run.textContent = '▶ Запустить рукопожатие';
    el.step.disabled = true;
    el.reset.disabled = true;
    const ph = document.createElement('div');
    ph.className = 'placeholder';
    ph.id = 'placeholder';
    ph.innerHTML = `<div class="placeholder-icon">🔐</div><p>Нажмите «Запустить рукопожатие». Демонстрация сгенерирует RSA-ключ сервера, проведёт обмен из четырёх запросов и покажет каждый байт, который уходит в сеть.</p>`;
    el.steps.append(ph);
    el.placeholder = ph;
  }
}

el.run.addEventListener('click', start);
el.step.addEventListener('click', () => {
  clearTimeout(app.autoTimer);
  nextStep();
});
el.reset.addEventListener('click', () => reset(true));
el.speed.addEventListener('input', () => {
  el.speedOut.textContent = (Number(el.speed.value) / 1000).toFixed(1) + ' с';
});
el.auto.addEventListener('change', () => {
  if (el.auto.checked && app.iterator && !app.finished && !app.running) {
    app.autoTimer = setTimeout(nextStep, Number(el.speed.value));
  } else {
    clearTimeout(app.autoTimer);
  }
});
document.addEventListener('keydown', (e) => {
  if (e.code === 'Space' && e.target === document.body) {
    e.preventDefault();
    if (!app.iterator) start();
    else {
      clearTimeout(app.autoTimer);
      nextStep();
    }
  }
});

buildProgress();
el.speedOut.textContent = (Number(el.speed.value) / 1000).toFixed(1) + ' с';
console.info('auth_key lab — вся криптография выполняется локально в браузере.', { hex });
