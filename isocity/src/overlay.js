// Optional economy read-out. Hidden by default - the city has no HUD.

import { formatMoney, formatClock } from './util.js';

const PANEL_BG = 'rgba(18,22,26,0.82)';
const TEXT = '#ece5d8';
const DIM = 'rgba(236,229,216,0.55)';
const GOLD = '#e8c46a';
const GREEN = '#8fd08a';
const RED = '#e08a7a';

export function drawOverlay(ctx, renderer, sim, w, h) {
  const pad = 16;
  const x = pad;
  const y = pad;
  const width = Math.min(430, w - pad * 2);
  const lines = [];

  ctx.save();
  ctx.setTransform(renderer.dpr, 0, 0, renderer.dpr, 0, 0);
  ctx.font = '500 12px system-ui, sans-serif';
  ctx.textBaseline = 'top';

  const left = [];
  const total = sim.totalMoney();
  const drift = total - sim.initialTotal;
  const household = sim.citizens.reduce((a, c) => a + c.cash, 0);
  const firmsCash = sim.firmList.reduce((a, f) => a + f.cash, 0);
  const L = sim.ledger || {};

  left.push(['h', `День ${sim.day} · ${formatClock(sim.minute)} · ${sim.timeScale}×`]);
  left.push(['kv', 'Денег в городе', `${formatMoney(total)} (${drift >= 0 ? '+' : ''}${formatMoney(drift)})`, drift === 0 ? GREEN : RED]);
  left.push(['kv', 'У домохозяйств', formatMoney(household), GOLD]);
  left.push(['kv', 'У фирм', formatMoney(firmsCash), GOLD]);
  left.push(['kv', 'Городской бюджет', formatMoney(sim.municipality.cash), GOLD]);
  left.push(['gap']);
  left.push(['h', 'Сегодня прошло']);
  left.push(['kv', 'Зарплаты', formatMoney(L.wages + L.cityWages), TEXT]);
  left.push(['kv', 'Покупки у фирм', formatMoney(L.purchases), TEXT]);
  left.push(['kv', 'Аренда и налоги', formatMoney(L.rent), TEXT]);
  left.push(['kv', 'Пособия и помощь', formatMoney(L.benefits + L.welfare), TEXT]);
  left.push(['kv', 'Госзаказ', formatMoney(L.publicWorks), TEXT]);
  left.push(['kv', 'Долги по зарплате', formatMoney(L.unpaid), L.unpaid > 0 ? RED : DIM]);
  left.push(['gap']);
  left.push(['h', 'Фирмы']);

  for (const f of sim.firmList) {
    const state = f.arrears.length ? RED : f.cash < 1 ? RED : TEXT;
    left.push(['row', f.id, f.building.name, `${formatMoney(f.cash)} ₽`, f.price ? `${f.price.toFixed(1)} ₽` : '—', f.public ? '—' : `${f.wage.toFixed(0)} ₽/д`, f.arrears.length ? `долг ${f.arrears.length}` : '', state]);
  }

  left.push(['gap']);
  left.push(['h', 'Жители']);
  const cash = sim.citizens.map((c) => c.cash).sort((a, b) => a - b);
  const hungry = sim.citizens.filter((c) => c.hunger > 1.2).length;
  const avgMorale = sim.citizens.reduce((a, c) => a + c.morale, 0) / sim.citizens.length;
  const employed = sim.citizens.filter((c) => c.work).length;
  left.push(['kv', 'Касса: мин / медиана / макс', `${formatMoney(cash[0])} / ${formatMoney(cash[Math.floor(cash.length / 2)])} / ${formatMoney(cash[cash.length - 1])}`, TEXT]);
  left.push(['kv', 'Работают / без работы', `${employed} / ${sim.citizens.length - employed}`, TEXT]);
  left.push(['kv', 'Голодают', `${hungry}`, hungry > 2 ? RED : TEXT]);
  left.push(['kv', 'Настроение', avgMorale.toFixed(2), avgMorale > 0.6 ? GREEN : RED]);

  // measure
  let height = 20;
  ctx.font = '500 12px system-ui, sans-serif';
  for (const l of left) height += lineHeight(l);

  roundRect(ctx, x, y, width, height + 22, 12);
  ctx.fillStyle = PANEL_BG;
  ctx.fill();
  ctx.strokeStyle = 'rgba(255,255,255,0.08)';
  ctx.lineWidth = 1;
  ctx.stroke();

  let cy = y + 14;
  for (const l of left) {
    cy = drawLine(ctx, l, x + 14, cy, width - 28);
  }
  ctx.restore();
}

function lineHeight(l) {
  switch (l[0]) {
    case 'gap': return 10;
    case 'h': return 24;
    case 'row': return 18;
    default: return 18;
  }
}

function drawLine(ctx, l, x, y, width) {
  const [kind] = l;
  if (kind === 'gap') return y + 10;
  if (kind === 'h') {
    ctx.fillStyle = DIM;
    ctx.font = '600 10px system-ui, sans-serif';
    ctx.fillText(l[1].toUpperCase(), x, y + 6);
    ctx.strokeStyle = 'rgba(255,255,255,0.08)';
    ctx.beginPath();
    ctx.moveTo(x, y + 20);
    ctx.lineTo(x + width, y + 20);
    ctx.stroke();
    ctx.font = '500 12px system-ui, sans-serif';
    return y + 24;
  }
  if (kind === 'kv') {
    ctx.fillStyle = DIM;
    ctx.font = '500 12px system-ui, sans-serif';
    ctx.fillText(l[1], x, y);
    ctx.fillStyle = l[3] || TEXT;
    ctx.textAlign = 'right';
    ctx.fillText(l[2], x + width, y);
    ctx.textAlign = 'left';
    return y + 18;
  }
  if (kind === 'row') {
    const [, id, name, cash, price, wage, debt, color] = l;
    ctx.fillStyle = TEXT;
    ctx.font = '600 12px system-ui, sans-serif';
    ctx.fillText(name, x, y);
    ctx.font = '500 11px system-ui, sans-serif';
    ctx.fillStyle = color;
    ctx.textAlign = 'right';
    ctx.fillText(cash, x + width, y);
    ctx.fillStyle = DIM;
    ctx.fillText(`${price} · ${wage}`, x + width - 62, y);
    if (debt) {
      ctx.fillStyle = RED;
      ctx.font = '500 10px system-ui, sans-serif';
      ctx.fillText(debt, x + width - 150, y + 1);
    }
    ctx.textAlign = 'left';
    return y + 18;
  }
  return y;
}

function roundRect(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.roundRect(x, y, w, h, r);
}
