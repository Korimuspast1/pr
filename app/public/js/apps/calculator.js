/* Calculator — real arithmetic with a tiny safe parser (no eval). */

const OPS = ['+', '−', '×', '÷'];

function tokenize(src) {
  const tokens = [];
  let i = 0;
  const s = src.replace(/\s+/g, '');
  while (i < s.length) {
    const c = s[i];
    if (/[0-9.]/.test(c)) {
      let n = '';
      while (i < s.length && /[0-9.]/.test(s[i])) n += s[i++];
      if ((n.match(/\./g) || []).length > 1) throw new Error('две точки в числе');
      tokens.push({ t: 'num', v: Number(n) });
    } else if (c === '%') { tokens.push({ t: 'pct' }); i++; }
    else if ('+-*/×÷−'.includes(c)) { tokens.push({ t: 'op', v: c }); i++; }
    else if (c === '(') { tokens.push({ t: 'lp' }); i++; }
    else if (c === ')') { tokens.push({ t: 'rp' }); i++; }
    else if (s.startsWith('sqrt', i)) { tokens.push({ t: 'fn', v: 'sqrt' }); i += 4; }
    else throw new Error(`неизвестный символ ${c}`);
  }
  return tokens;
}

function evaluate(src) {
  const tokens = tokenize(src);
  let pos = 0;
  const peek = () => tokens[pos];
  const eat = (t) => { if (peek()?.t === t) { pos++; return true; } return false; };

  function parseExpr() {
    let v = parseTerm();
    while (peek()?.t === 'op' && (peek().v === '+' || peek().v === '-' || peek().v === '−')) {
      const op = tokens[pos++].v;
      const r = parseTerm();
      v = op === '+' ? v + r : v - r;
    }
    return v;
  }
  function parseTerm() {
    let v = parseFactor();
    while (peek()?.t === 'op' && (peek().v === '*' || peek().v === '*' || peek().v === '×' || peek().v === '/' || peek().v === '÷')) {
      const op = tokens[pos++].v;
      const r = parseFactor();
      if (op === '/' || op === '÷') {
        if (r === 0) throw new Error('деление на ноль');
        v = v / r;
      } else v = v * r;
    }
    return v;
  }
  function parseFactor() {
    if (eat('op')) {
      const op = tokens[pos - 1].v;
      const v = parseFactor();
      return op === '-' || op === '−' ? -v : v;
    }
    return parseUnary();
  }
  function parseUnary() {
    let v = parseAtom();
    while (peek()?.t === 'pct') { pos++; v = v / 100; }
    return v;
  }
  function parseAtom() {
    const tk = peek();
    if (!tk) throw new Error('неожиданный конец выражения');
    if (tk.t === 'num') { pos++; return tk.v; }
    if (tk.t === 'lp') {
      pos++;
      const v = parseExpr();
      if (!eat('rp')) throw new Error('нет закрывающей скобки');
      return v;
    }
    if (tk.t === 'fn' && tk.v === 'sqrt') {
      pos++;
      const v = parseAtom();
      if (v < 0) throw new Error('корень из отрицательного');
      return Math.sqrt(v);
    }
    throw new Error('ожидалось число');
  }

  const value = parseExpr();
  if (pos < tokens.length) throw new Error('лишние символы');
  if (!Number.isFinite(value)) throw new Error('не число');
  return value;
}

const format = (n) => {
  if (!Number.isFinite(n)) return '∞';
  const abs = Math.abs(n);
  if (abs !== 0 && (abs >= 1e15 || abs < 1e-9)) return n.toExponential(8).replace('e', 'e');
  const rounded = Math.round(n * 1e12) / 1e12;
  return String(rounded);
};

export default {
  id: 'calculator',
  name: 'Калькулятор',
  icon: 'calculator',
  width: 340,
  height: 470,
  dock: true,

  mount(body, win) {
    let expr = '';
    let memory = 0;
    const history = [];

    body.innerHTML = `
      <div class="calc-wrap">
        <div class="calc-display">
          <div class="calc-expr">&nbsp;</div>
          <div class="calc-value">0</div>
        </div>
        <div class="calc-keys">
          <button class="ck fn" data-k="C">C</button>
          <button class="ck fn" data-k="CE">CE</button>
          <button class="ck fn" data-k="%">%</button>
          <button class="ck op" data-k="÷">÷</button>
          <button class="ck fn" data-k="sqrt">√</button>
          <button class="ck" data-k="7">7</button>
          <button class="ck" data-k="8">8</button>
          <button class="ck" data-k="9">9</button>
          <button class="ck op" data-k="×">×</button>
          <button class="ck fn" data-k="(">(</button>
          <button class="ck" data-k="4">4</button>
          <button class="ck" data-k="5">5</button>
          <button class="ck" data-k="6">6</button>
          <button class="ck op" data-k="−">−</button>
          <button class="ck fn" data-k=")">)</button>
          <button class="ck" data-k="1">1</button>
          <button class="ck" data-k="2">2</button>
          <button class="ck" data-k="3">3</button>
          <button class="ck op" data-k="+">+</button>
          <button class="ck fn" data-k="⌫">⌫</button>
          <button class="ck span2" data-k="0">0</button>
          <button class="ck" data-k=".">.</button>
          <button class="ck eq" data-k="=">=</button>
        </div>
        <div class="calc-mem">
          <button data-m="mc">MC</button><button data-m="mr">MR</button>
          <button data-m="m+">M+</button><button data-m="m-">M−</button>
          <span class="grow"></span>
          <span class="calc-hist-btn" title="История">🕘</span>
        </div>
        <div class="calc-history hidden"></div>
      </div>`;

    const $ = (s) => body.querySelector(s);
    const valEl = $('.calc-value');
    const exprEl = $('.calc-expr');
    const histEl = $('.calc-history');

    const paint = () => {
      valEl.textContent = expr || '0';
      valEl.classList.toggle('small', (expr || '').length > 12);
      exprEl.innerHTML = expr ? '&nbsp;' : '&nbsp;';
    };

    const paintHistory = () => {
      histEl.innerHTML = history.length
        ? history.map((h) => `<div class="ch-row"><span>${h.e}</span><b>${h.r}</b></div>`).join('')
        : '<div class="ch-empty">пока пусто</div>';
    };

    function press(k) {
      if (k === 'C') expr = '';
      else if (k === 'CE') expr = expr.replace(/[0-9.]+$/, '');
      else if (k === '⌫') expr = expr.slice(0, -1);
      else if (k === '=') {
        if (!expr.trim()) return;
        try {
          const r = format(evaluate(expr));
          history.unshift({ e: expr, r });
          if (history.length > 40) history.pop();
          exprEl.textContent = expr + ' =';
          expr = r;
          paintHistory();
        } catch (e) {
          exprEl.innerHTML = `<span class="calc-err">${e.message}</span>`;
          return;
        }
      } else if (k === 'sqrt') {
        expr += 'sqrt(';
      } else {
        expr += k;
      }
      paint();
    }

    body.querySelectorAll('[data-k]').forEach((b) => { b.onclick = () => press(b.dataset.k); });
    body.querySelectorAll('[data-m]').forEach((b) => {
      b.onclick = () => {
        const act = b.dataset.m;
        let cur = 0;
        try { cur = expr ? evaluate(expr) : 0; } catch { cur = 0; }
        if (act === 'mc') { memory = 0; win.notify('Память очищена', '', 'info', 1800); }
        if (act === 'mr') { expr = format(memory); paint(); }
        if (act === 'm+') { memory += cur; win.notify('В памяти', format(memory), 'info', 1600); }
        if (act === 'm-') { memory -= cur; win.notify('В памяти', format(memory), 'info', 1600); }
      };
    });
    $('.calc-hist-btn').onclick = () => histEl.classList.toggle('hidden');
    paintHistory();

    body.tabIndex = 0;
    body.addEventListener('keydown', (e) => {
      const k = e.key;
      if (/^[0-9.]$/.test(k)) { press(k === '*' ? '×' : k); e.preventDefault(); }
      else if (k === '+') { press('+'); e.preventDefault(); }
      else if (k === '-') { press('−'); e.preventDefault(); }
      else if (k === '*') { press('×'); e.preventDefault(); }
      else if (k === '/') { press('÷'); e.preventDefault(); }
      else if (k === '(' || k === ')' || k === '%') { press(k); e.preventDefault(); }
      else if (k === 'Enter' || k === '=') { press('='); e.preventDefault(); }
      else if (k === 'Backspace') { press('⌫'); e.preventDefault(); }
      else if (k === 'Escape') { press('C'); e.preventDefault(); }
    });
    setTimeout(() => body.focus(), 50);
    paint();
    win.setTitle('Калькулятор');
  },
};
