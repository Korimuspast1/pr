// Дымовой тест интерфейса без браузера: подменяем минимальный DOM и прогоняем
// весь сценарий app.js, чтобы поймать опечатки и ошибки времени выполнения.

class ClassList {
  constructor(node) {
    this.node = node;
    this.set = new Set();
  }
  add(...c) { c.forEach((x) => x && this.set.add(x)); }
  remove(...c) { c.forEach((x) => this.set.delete(x)); }
  contains(c) { return this.set.has(c); }
  toggle(c, force) {
    const want = force === undefined ? !this.set.has(c) : force;
    if (want) this.set.add(c); else this.set.delete(c);
    return want;
  }
  toString() { return [...this.set].join(' '); }
}

class El {
  constructor(tag = 'div') {
    this.tagName = tag.toUpperCase();
    this.children = [];
    this.parent = null;
    this.classList = new ClassList(this);
    this.dataset = {};
    this.style = {};
    this.listeners = {};
    this.textContent = '';
    this._html = '';
    this.hidden = false;
    this.disabled = false;
    this.value = '1200';
    this.checked = true;
  }
  set className(v) {
    this.classList.set = new Set(String(v).split(/\s+/).filter(Boolean));
  }
  get className() { return this.classList.toString(); }
  set innerHTML(v) { this._html = v; this.children = []; }
  get innerHTML() { return this._html; }
  append(...nodes) {
    for (const n of nodes) {
      if (typeof n === 'string') continue;
      n.parent = this;
      this.children.push(n);
    }
  }
  remove() {
    if (this.parent) this.parent.children = this.parent.children.filter((c) => c !== this);
  }
  addEventListener(type, fn) { (this.listeners[type] ||= []).push(fn); }
  dispatch(type, ev = {}) { (this.listeners[type] || []).forEach((fn) => fn(ev)); }
  scrollIntoView() {}
  get offsetWidth() { return 100; }
  querySelectorAll(selector) {
    const out = [];
    const match = makeMatcher(selector);
    const walk = (node) => {
      for (const c of node.children ?? []) {
        if (match(c)) out.push(c);
        walk(c);
      }
    };
    walk(this);
    return out;
  }
}

function makeMatcher(selector) {
  const attr = selector.match(/\[data-f="(\d+)"\]/);
  const classes = selector.replace(/\[[^\]]*\]/g, '').split('.').filter(Boolean);
  return (node) =>
    node.classList !== undefined &&
    classes.every((c) => node.classList.contains(c)) &&
    (!attr || node.dataset.f === attr[1]);
}

const registry = new Map();
for (const id of [
  'btn-run', 'btn-step', 'btn-reset', 'chk-auto', 'rng-speed', 'out-speed', 'steps', 'placeholder',
  'progress', 'state-list', 'result-panel', 'result-body', 'packet', 'wire-label', 'peer-client',
  'peer-server', 'peer-client-secret', 'peer-server-secret', 'toast',
]) {
  registry.set(id, new El('div'));
}

globalThis.document = {
  getElementById: (id) => registry.get(id) ?? new El('div'),
  createElement: (tag) => new El(tag),
  createTextNode: (t) => ({ text: t }),
  addEventListener: () => {},
  body: new El('body'),
};
Object.defineProperty(globalThis, 'navigator', {
  value: { clipboard: { writeText: async () => {} } },
  configurable: true,
});

const app = await import('../src/app.js');
void app;

// Прогон: жмём «Запустить» и добираем шаги вручную (автопрогон тоже включён).
registry.get('btn-run').dispatch('click');

const deadline = Date.now() + 60000;
while (Date.now() < deadline) {
  await new Promise((r) => setTimeout(r, 120));
  const run = registry.get('btn-run');
  if (run.textContent.includes('завершено')) break;
  registry.get('btn-step').dispatch('click');
}

const steps = registry.get('steps').children.filter((c) => c.classList.contains('step'));
const result = registry.get('result-body').innerHTML;

let fail = false;
const check = (name, ok, extra = '') => {
  console.log(`${ok ? 'OK  ' : 'FAIL'} ${name}${extra ? ' — ' + extra : ''}`);
  if (!ok) fail = true;
};

check('отрисовано 11 карточек шагов', steps.length === 11, String(steps.length));
check('панель результата заполнена', result.includes('auth_key_id'));
check('ключи совпали', result.includes('✓ Ключи совпали'));
check('кнопка перешла в состояние «завершено»', registry.get('btn-run').textContent.includes('завершено'));

// Проверяем интерактив hex-дампа: наводим на первое поле легенды первой карточки с дампом.
const dump = steps
  .flatMap((s) => s.children)
  .flatMap((s) => s.children)
  .find((n) => n.classList.contains('dump'));
if (dump) {
  const legend = dump.children.find((c) => c.classList.contains('dump-legend'));
  const body = dump.children.find((c) => c.classList.contains('dump-body'));
  legend.children[0].dispatch('mouseenter');
  const highlighted = body.querySelectorAll('.byte.hl').length;
  legend.children[0].dispatch('mouseleave');
  check('подсветка байтов поля работает', highlighted > 0, `${highlighted} байт`);
} else {
  check('найден hex-дамп', false);
}

console.log(fail ? '\nЕсть ошибки' : '\nИнтерфейс отработал сценарий целиком');
process.exit(fail ? 1 : 0);
