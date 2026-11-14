/* Смайлики 😊 — интерпретатор на JavaScript для браузерной песочницы.
   Тот же язык, что и в smiley.py: лексер → парсер → интерпретатор. */

export class SmileyError extends Error {
  constructor(message, line = null) {
    super(message);
    this.name = "SmileyError";
    this.smileyMessage = message;
    this.line = line;
  }
}

const DEFAULT_MAX_STEPS = 300000;
const DEFAULT_MAX_OUTPUT = 2000;
const VARIATION_SELECTORS = new Set(["\ufe0f", "\ufe0e"]);

const EMOJI_TOKENS = [
  ["⬆\uFE0F?🟰", "GE"],
  ["⬇\uFE0F?🟰", "LE"],
  ["📦", "LET"],
  ["🗣", "PRINT"],
  ["❓", "IF"],
  ["🤔", "THEN"],
  ["🙃", "ELSE"],
  ["🔚", "END"],
  ["🔁", "WHILE"],
  ["🔂", "REPEAT"],
  ["🎁", "FUNC"],
  ["↩", "RETURN"],
  ["💔", "BREAK"],
  ["⏭", "CONTINUE"],
  ["✅", "TRUE"],
  ["❌", "FALSE"],
  ["🟰", "EQ"],
  ["🚫", "NE"],
  ["⬆", "GT"],
  ["⬇", "LT"],
  ["🤝", "AND"],
  ["🔀", "OR"],
  ["🙅", "NOT"],
  ["➕", "PLUS"],
  ["➖", "MINUS"],
  ["✖", "STAR"],
  ["➗", "SLASH"],
  ["📐", "MOD"],
  ["⬅", "ASSIGN"],
  ["🙈", "COMMENT"],
];

const KEYWORDS = {
  let: "LET", print: "PRINT", if: "IF", then: "THEN", else: "ELSE", end: "END",
  while: "WHILE", repeat: "REPEAT", func: "FUNC", fun: "FUNC", return: "RETURN",
  break: "BREAK", continue: "CONTINUE", true: "TRUE", false: "FALSE",
  and: "AND", or: "OR", not: "NOT", mod: "MOD",
  "пусть": "LET", "сказать": "PRINT", "если": "IF", "то": "THEN", "иначе": "ELSE",
  "конец": "END", "пока": "WHILE", "повтори": "REPEAT", "функция": "FUNC",
  "вернуть": "RETURN", "стоп": "BREAK", "дальше": "CONTINUE",
  "истина": "TRUE", "ложь": "FALSE", "и": "AND", "или": "OR", "не": "NOT",
};

const EMOJI_PATTERNS = EMOJI_TOKENS.map(([pattern, kind]) => {
  // "⬆\uFE0F?🟰" → [⬆, необязательный селектор, 🟰]
  const parts = [];
  const points = Array.from(pattern);
  for (let i = 0; i < points.length; i += 1) {
    if (points[i] === "\uFE0F" && points[i + 1] === "?") {
      parts.push({ optional: true });
      i += 1;
      continue;
    }
    parts.push({ ch: points[i], optional: false });
  }
  return [parts, kind];
});

const SYMBOLS = { "(": "LPAREN", ")": "RPAREN", ",": "COMMA", ";": "NEWLINE", "+": "PLUS", "-": "MINUS", "*": "STAR", "/": "SLASH", "%": "MOD" };
const TWO_CHAR = { "==": "EQ", "!=": "NE", ">=": "GE", "<=": "LE", "//": "COMMENT" };
const ONE_CHAR = { ">": "GT", "<": "LT", "=": "ASSIGN" };

/* ------------------------------- лексер ------------------------------- */

export function tokenize(source) {
  const chars = Array.from(source);
  const tokens = [];
  let pos = 0;
  let line = 1;
  const at = (offset = 0) => (pos + offset < chars.length ? chars[pos + offset] : "");
  const add = (kind, value, text, atLine = line) => tokens.push({ kind, value, line: atLine, text });

  const matchEmoji = (pattern) => {
    // pattern — список code point'ов; "\uFE0F?" означает необязательный селектор вариации
    let index = pos;
    for (const part of pattern) {
      if (part.optional) {
        while (index < chars.length && VARIATION_SELECTORS.has(chars[index])) index += 1;
        continue;
      }
      if (index >= chars.length || chars[index] !== part.ch) return null;
      index += 1;
    }
    return index;
  };

  const skipComment = () => {
    while (pos < chars.length && chars[pos] !== "\n") pos += 1;
  };

  while (pos < chars.length) {
    const ch = chars[pos];

    if (ch === " " || ch === "\t" || ch === "\r") { pos += 1; continue; }

    if (ch === "\n") { add("NEWLINE", "\n", "\n"); line += 1; pos += 1; continue; }

    if (ch === "#" || ch === "🙈") { skipComment(); continue; }
    if (ch === "/" && at(1) === "/") { skipComment(); continue; }

    if (ch === '"' || ch === "«") {
      const opener = ch;
      const closer = opener === "«" ? "»" : '"';
      const startLine = line;
      pos += 1;
      let out = "";
      for (;;) {
        if (pos >= chars.length) throw new SmileyError("строка не закрыта (не хватает кавычки)", startLine);
        const c = chars[pos];
        if (c === closer) { pos += 1; break; }
        if (c === "\n") throw new SmileyError("строка не может переноситься на новую строку", startLine);
        if (c === "\\") {
          pos += 1;
          const esc = at();
          const map = { n: "\n", t: "\t", "\\": "\\", '"': '"', "«": "«", "»": "»" };
          if (esc in map) { out += map[esc]; pos += 1; continue; }
          if (esc === "u") {
            const code = chars.slice(pos + 1, pos + 5).join("");
            const value = parseInt(code, 16);
            if (Number.isNaN(value)) throw new SmileyError(`плохой \\u-код: ${code}`, startLine);
            out += String.fromCharCode(value);
            pos += 5;
            continue;
          }
          throw new SmileyError(`неизвестная escape-последовательность \\${esc}`, startLine);
        }
        out += c;
        pos += 1;
      }
      add("STR", out, "строка", startLine);
      continue;
    }

    if (/[0-9]/.test(ch)) {
      const start = pos;
      while (pos < chars.length && /[0-9_]/.test(chars[pos])) pos += 1;
      let isFloat = false;
      if (pos + 1 < chars.length && chars[pos] === "." && /[0-9]/.test(chars[pos + 1])) {
        isFloat = true;
        pos += 1;
        while (pos < chars.length && /[0-9_]/.test(chars[pos])) pos += 1;
      }
      const raw = chars.slice(start, pos).join("").replace(/_/g, "");
      add("NUM", isFloat ? parseFloat(raw) : parseInt(raw, 10), raw);
      continue;
    }

    let matched = false;
    for (const [pattern, kind] of EMOJI_PATTERNS) {
      const end = matchEmoji(pattern);
      if (end === null) continue;
      const raw = chars.slice(pos, end).join("");
      pos = end;
      if (kind === "COMMENT") { skipComment(); } else { add(kind, raw, raw); }
      matched = true;
      break;
    }
    if (matched) continue;

    const pair = chars.slice(pos, pos + 2).join("");
    if (TWO_CHAR[pair]) { add(TWO_CHAR[pair], pair, pair); pos += 2; continue; }
    if (ONE_CHAR[ch]) { add(ONE_CHAR[ch], ch, ch); pos += 1; continue; }
    if (SYMBOLS[ch]) { add(SYMBOLS[ch], ch, ch); pos += 1; continue; }

    if (/[\p{L}_]/u.test(ch)) {
      const start = pos;
      while (pos < chars.length && /[\p{L}\p{N}_]/u.test(chars[pos])) pos += 1;
      const name = chars.slice(start, pos).join("");
      const kind = KEYWORDS[name.toLowerCase()];
      if (kind) add(kind, name.toLowerCase(), name); else add("NAME", name, name);
      continue;
    }

    throw new SmileyError(`не понимаю символ ${JSON.stringify(ch)}`, line);
  }

  add("EOF", null, "");
  return tokens;
}

/* ------------------------------- парсер ------------------------------- */

export class Parser {
  constructor(tokens) {
    this.tokens = tokens;
    this.pos = 0;
  }

  peek(offset = 0) {
    return this.tokens[Math.min(this.pos + offset, this.tokens.length - 1)];
  }

  at(...kinds) { return kinds.includes(this.peek().kind); }

  advance() {
    const token = this.peek();
    if (token.kind !== "EOF") this.pos += 1;
    return token;
  }

  match(...kinds) {
    if (this.at(...kinds)) { this.advance(); return true; }
    return false;
  }

  expect(kind, human) {
    if (this.at(kind)) return this.advance();
    throw new SmileyError(`ожидалось ${human}, а тут ${describe(this.peek())}`, this.peek().line);
  }

  skipNewlines() { while (this.at("NEWLINE")) this.advance(); }

  parseProgram() {
    const [body] = this.parseBlock([]);
    return body;
  }

  parseBlock(stop) {
    const body = [];
    for (;;) {
      this.skipNewlines();
      if (stop.length && this.at(...stop)) return [body, this.peek().kind];
      if (this.at("EOF")) return [body, "EOF"];
      body.push(this.parseStatement());
      if (stop.length && this.at(...stop)) return [body, this.peek().kind];
      if (this.at("EOF")) return [body, "EOF"];
      if (!this.match("NEWLINE")) {
        throw new SmileyError(`лишнее после команды: ${describe(this.peek())}`, this.peek().line);
      }
    }
  }

  parseStatement() {
    const token = this.peek();
    const kind = token.kind;

    if (kind === "LET") {
      this.advance();
      const name = this.expect("NAME", "имя переменной после 📦");
      this.expect("ASSIGN", "⬅ (присваивание)");
      return ["let", name.value, this.parseExpression(), token.line];
    }

    if (kind === "PRINT") {
      this.advance();
      const values = [this.parseExpression()];
      while (this.match("COMMA")) values.push(this.parseExpression());
      return ["print", values, token.line];
    }

    if (kind === "IF") return this.parseIf();

    if (kind === "WHILE") {
      this.advance();
      const condition = this.parseExpression();
      const [body, stopped] = this.parseBlock(["END"]);
      if (stopped !== "END") throw new SmileyError("цикл 🔁 не закрыт: не хватает 🔚", token.line);
      this.advance();
      return ["while", condition, body, token.line];
    }

    if (kind === "REPEAT") {
      this.advance();
      const count = this.parseExpression();
      const [body, stopped] = this.parseBlock(["END"]);
      if (stopped !== "END") throw new SmileyError("цикл 🔂 не закрыт: не хватает 🔚", token.line);
      this.advance();
      return ["repeat", count, body, token.line];
    }

    if (kind === "FUNC") {
      this.advance();
      const name = this.expect("NAME", "имя функции после 🎁");
      this.expect("LPAREN", "( со списком аргументов");
      const params = [];
      if (!this.at("RPAREN")) {
        for (;;) {
          params.push(this.expect("NAME", "имя аргумента").value);
          if (!this.match("COMMA")) break;
        }
      }
      this.expect("RPAREN", ")");
      const [body, stopped] = this.parseBlock(["END"]);
      if (stopped !== "END") throw new SmileyError("функция 🎁 не закрыта: не хватает 🔚", token.line);
      this.advance();
      return ["func", name.value, params, body, token.line];
    }

    if (kind === "RETURN") {
      this.advance();
      if (this.at("NEWLINE", "EOF", "END", "ELSE")) return ["return", null, token.line];
      return ["return", this.parseExpression(), token.line];
    }

    if (kind === "BREAK") { this.advance(); return ["break", token.line]; }
    if (kind === "CONTINUE") { this.advance(); return ["continue", token.line]; }

    if (kind === "NAME" && this.peek(1).kind === "ASSIGN") {
      const name = this.advance();
      this.advance();
      return ["assign", name.value, this.parseExpression(), token.line];
    }

    return ["expr", this.parseExpression(), token.line];
  }

  parseIf() {
    const token = this.expect("IF", "❓");
    const condition = this.parseExpression();
    this.match("THEN");
    let [thenBody, stopped] = this.parseBlock(["ELSE", "END"]);
    let elseBody = [];
    if (stopped === "ELSE") {
      this.advance();
      if (this.at("IF")) {
        elseBody = [this.parseIf()];
      } else {
        [elseBody, stopped] = this.parseBlock(["END"]);
        if (stopped !== "END") throw new SmileyError("🙃 не закрыт: не хватает 🔚", token.line);
        this.advance();
      }
    } else if (stopped === "END") {
      this.advance();
    } else {
      throw new SmileyError("❓ не закрыт: не хватает 🔚", token.line);
    }
    return ["if", condition, thenBody, elseBody, token.line];
  }

  parseExpression() { return this.parseOr(); }

  parseOr() {
    let node = this.parseAnd();
    while (this.at("OR")) {
      const { line } = this.advance();
      node = ["binop", "🔀", node, this.parseAnd(), line];
    }
    return node;
  }

  parseAnd() {
    let node = this.parseNot();
    while (this.at("AND")) {
      const { line } = this.advance();
      node = ["binop", "🤝", node, this.parseNot(), line];
    }
    return node;
  }

  parseNot() {
    if (this.at("NOT")) {
      const { line } = this.advance();
      return ["unop", "🙅", this.parseNot(), line];
    }
    return this.parseComparison();
  }

  parseComparison() {
    let node = this.parseTerm();
    const ops = { EQ: "🟰", NE: "🚫", GT: "⬆", LT: "⬇", GE: "⬆🟰", LE: "⬇🟰" };
    while (ops[this.peek().kind]) {
      const token = this.advance();
      node = ["binop", ops[token.kind], node, this.parseTerm(), token.line];
    }
    return node;
  }

  parseTerm() {
    let node = this.parseFactor();
    while (this.at("PLUS", "MINUS")) {
      const token = this.advance();
      node = ["binop", token.kind === "PLUS" ? "➕" : "➖", node, this.parseFactor(), token.line];
    }
    return node;
  }

  parseFactor() {
    let node = this.parseUnary();
    while (this.at("STAR", "SLASH", "MOD")) {
      const token = this.advance();
      const symbol = { STAR: "✖", SLASH: "➗", MOD: "📐" }[token.kind];
      node = ["binop", symbol, node, this.parseUnary(), token.line];
    }
    return node;
  }

  parseUnary() {
    if (this.at("MINUS")) {
      const { line } = this.advance();
      return ["unop", "➖", this.parseUnary(), line];
    }
    return this.parseCall();
  }

  parseCall() {
    let node = this.parsePrimary();
    while (this.at("LPAREN")) {
      const { line } = this.advance();
      const args = [];
      if (!this.at("RPAREN")) {
        for (;;) {
          args.push(this.parseExpression());
          if (!this.match("COMMA")) break;
        }
      }
      this.expect("RPAREN", ")");
      node = ["call", node, args, line];
    }
    return node;
  }

  parsePrimary() {
    const token = this.peek();
    if (token.kind === "NUM") { this.advance(); return ["num", token.value]; }
    if (token.kind === "STR") { this.advance(); return ["str", token.value]; }
    if (token.kind === "TRUE") { this.advance(); return ["bool", true]; }
    if (token.kind === "FALSE") { this.advance(); return ["bool", false]; }
    if (token.kind === "NAME") { this.advance(); return ["var", token.value, token.line]; }
    if (token.kind === "LPAREN") {
      this.advance();
      const inner = this.parseExpression();
      this.expect("RPAREN", ")");
      return inner;
    }
    throw new SmileyError(`ожидалось значение, а тут ${describe(token)}`, token.line);
  }
}

function describe(token) {
  const names = {
    NEWLINE: "конец строки", EOF: "конец программы", NAME: "имя", NUM: "число",
    STR: "строка", ASSIGN: "⬅", LPAREN: "(", RPAREN: ")", COMMA: ",", END: "🔚",
  };
  return names[token.kind] || token.text || token.kind;
}

/* ------------------------------ значения ------------------------------ */

export function toText(value) {
  if (value === true) return "✅";
  if (value === false) return "❌";
  if (value === null || value === undefined) return "🤷";
  if (Array.isArray(value)) return value.map(toText).join(" ");
  if (typeof value === "number" && !Number.isInteger(value)) {
    return String(Number(value.toFixed(10)));
  }
  return String(value);
}

function truthy(value) {
  if (value === null || value === undefined || value === false) return false;
  if (typeof value === "number") return value !== 0;
  if (typeof value === "string") return value.length > 0;
  return true;
}

class Environment {
  constructor(parent = null) {
    this.vars = new Map();
    this.parent = parent;
  }
  define(name, value) { this.vars.set(name, value); }
  has(name) {
    let env = this;
    while (env) { if (env.vars.has(name)) return true; env = env.parent; }
    return false;
  }
  get(name, line) {
    let env = this;
    while (env) {
      if (env.vars.has(name)) return env.vars.get(name);
      env = env.parent;
    }
    throw new SmileyError(`переменная «${name}» не найдена`, line ?? null);
  }
  assign(name, value) {
    let env = this;
    while (env) {
      if (env.vars.has(name)) { env.vars.set(name, value); return; }
      env = env.parent;
    }
    this.vars.set(name, value);
  }
}

class ReturnSignal { constructor(value) { this.value = value; } }
class BreakSignal {}
class ContinueSignal {}
class StepLimit {}
class OutputLimit {}

/* --------------------------- интерпретатор --------------------------- */

export class Interpreter {
  constructor({ maxSteps = DEFAULT_MAX_STEPS, maxOutput = DEFAULT_MAX_OUTPUT, seed = 1, input = [] } = {}) {
    this.output = [];
    this.maxSteps = maxSteps;
    this.maxOutput = maxOutput;
    this.steps = 0;
    this.env = new Environment();
    this.inputs = input.slice();
    this.random = mulberry32(seed);
    this.builtins = new Map();
    this.registerBuiltins();
  }

  registerBuiltins() {
    const add = (names, lo, hi, fn) => names.forEach((n) => this.builtins.set(n, { lo, hi, fn }));
    add(["длина", "len"], 1, 1, (v) => Array.from(this.asText(v)).length);
    add(["строка", "str"], 1, 1, (v) => this.asText(v));
    add(["число", "int", "num"], 1, 1, (v) => this.asNumber(v));
    add(["случайное", "random"], 2, 2, (a, b) => {
      let lo = Math.trunc(this.asNumber(a));
      let hi = Math.trunc(this.asNumber(b));
      if (lo > hi) [lo, hi] = [hi, lo];
      return lo + Math.floor(this.random() * (hi - lo + 1));
    });
    add(["спросить", "input", "вопрос"], 0, 1, (question) => {
      if (this.inputs.length === 0) {
        throw new SmileyError("ввод закончился: добавь строки в поле «Ввод»");
      }
      return this.inputs.shift();
    });
    add(["время", "time"], 0, 0, () => Math.floor(Date.now() / 1000));
    add(["верхний", "upper"], 1, 1, (v) => this.asText(v).toUpperCase());
    add(["нижний", "lower"], 1, 1, (v) => this.asText(v).toLowerCase());
    add(["круглое", "round"], 1, 1, (v) => Math.round(this.asNumber(v)));
  }

  asText(value) {
    if (value === true) return "✅";
    if (value === false) return "❌";
    if (value === null || value === undefined) return "";
    return toText(value);
  }

  asNumber(value) {
    if (typeof value === "boolean") return value ? 1 : 0;
    if (typeof value === "number") return value;
    if (typeof value === "string") {
      const text = value.trim().replace(",", ".");
      if (text === "") throw new SmileyError(`не получается превратить пустую строку в число`);
      const num = Number(text);
      if (Number.isNaN(num)) throw new SmileyError(`не получается превратить «${value}» в число`);
      return num;
    }
    throw new SmileyError(`не получается превратить ${toText(value)} в число`);
  }

  intArg(value, what) {
    const num = this.asNumber(value);
    if (!Number.isInteger(num)) throw new SmileyError(`${what} должно быть целым числом, а не ${toText(value)}`);
    return num;
  }

  tick() {
    this.steps += 1;
    if (this.steps > this.maxSteps) throw new StepLimit();
  }

  emit(text) {
    if (this.output.length >= this.maxOutput) throw new OutputLimit();
    this.output.push(text);
  }

  run(program) {
    try {
      this.execBlock(program, this.env);
    } catch (error) {
      if (error instanceof StepLimit) {
        throw new SmileyError(`🤯 программа слишком долго работает: больше ${this.maxSteps} шагов. Похоже на бесконечный цикл.`);
      }
      if (error instanceof OutputLimit) {
        throw new SmileyError(`🗣 слишком много вывода: больше ${this.maxOutput} строк. Проверь циклы.`);
      }
      if (error instanceof ReturnSignal) throw new SmileyError("↩ можно использовать только внутри 🎁 функции");
      if (error instanceof BreakSignal) throw new SmileyError("💔 можно использовать только внутри цикла");
      if (error instanceof ContinueSignal) throw new SmileyError("⏭ можно использовать только внутри цикла");
      if (error instanceof RangeError) throw new SmileyError("слишком глубокая рекурсия: функция вызывает себя без конца");
      throw error;
    }
  }

  execBlock(body, env) {
    for (const statement of body) this.execStatement(statement, env);
  }

  execStatement(node, env) {
    this.tick();
    const tag = node[0];

    if (tag === "let") { env.define(node[1], this.eval(node[2], env)); return; }
    if (tag === "assign") { env.assign(node[1], this.eval(node[2], env)); return; }
    if (tag === "print") {
      this.emit(node[1].map((part) => toText(this.eval(part, env))).join(" "));
      return;
    }
    if (tag === "expr") { this.eval(node[1], env); return; }

    if (tag === "if") {
      const [, condition, thenBody, elseBody] = node;
      if (truthy(this.eval(condition, env))) this.execBlock(thenBody, new Environment(env));
      else if (elseBody.length) this.execBlock(elseBody, new Environment(env));
      return;
    }

    if (tag === "while") {
      const [, condition, body] = node;
      while (truthy(this.eval(condition, env))) {
        this.tick();
        try {
          this.execBlock(body, new Environment(env));
        } catch (error) {
          if (error instanceof BreakSignal) break;
          if (error instanceof ContinueSignal) continue;
          throw error;
        }
      }
      return;
    }

    if (tag === "repeat") {
      const count = this.intArg(this.eval(node[1], env), "число повторов");
      if (count < 0) throw new SmileyError("число повторов не может быть отрицательным", node[3]);
      for (let i = 0; i < count; i += 1) {
        this.tick();
        try {
          this.execBlock(node[2], new Environment(env));
        } catch (error) {
          if (error instanceof BreakSignal) break;
          if (error instanceof ContinueSignal) continue;
          throw error;
        }
      }
      return;
    }

    if (tag === "func") {
      env.define(node[1], { kind: "function", name: node[1], params: node[2], body: node[3], closure: env });
      return;
    }

    if (tag === "return") throw new ReturnSignal(node[1] === null ? null : this.eval(node[1], env));
    if (tag === "break") throw new BreakSignal();
    if (tag === "continue") throw new ContinueSignal();

    throw new SmileyError(`внутренняя ошибка: неизвестная команда ${tag}`);
  }

  eval(node, env) {
    this.tick();
    const tag = node[0];

    if (tag === "num" || tag === "str" || tag === "bool") return node[1];

    if (tag === "var") {
      const name = node[1];
      if (env.has(name)) return env.get(name);
      if (this.builtins.has(name)) return { kind: "builtin", name };
      throw new SmileyError(`переменная «${name}» не найдена`, node[2]);
    }

    if (tag === "unop") {
      const [, symbol, operand, line] = node;
      const value = this.eval(operand, env);
      if (symbol === "🙅") return !truthy(value);
      if (symbol === "➖") return -this.arithNumber(value, "➖", line);
      throw new SmileyError(`неизвестный оператор ${symbol}`, line);
    }

    if (tag === "binop") {
      const [, symbol, leftNode, rightNode, line] = node;
      if (symbol === "🤝") {
        const left = this.eval(leftNode, env);
        return truthy(left) ? this.eval(rightNode, env) : left;
      }
      if (symbol === "🔀") {
        const left = this.eval(leftNode, env);
        return truthy(left) ? left : this.eval(rightNode, env);
      }
      const left = this.eval(leftNode, env);
      const right = this.eval(rightNode, env);
      return this.binary(symbol, left, right, line);
    }

    if (tag === "call") {
      const callee = this.eval(node[1], env);
      const args = node[2].map((arg) => this.eval(arg, env));
      return this.call(callee, args, node[3]);
    }

    throw new SmileyError(`внутренняя ошибка: неизвестное выражение ${tag}`);
  }

  arithNumber(value, symbol, line) {
    if (typeof value === "boolean") return value ? 1 : 0;
    if (typeof value === "number") return value;
    throw new SmileyError(`оператор ${symbol} ждёт число, а получил ${toText(value)}`, line);
  }

  binary(symbol, left, right, line) {
    if (symbol === "➕") {
      if (typeof left === "string" || typeof right === "string") return this.asText(left) + this.asText(right);
      if (Array.isArray(left) && Array.isArray(right)) return left.concat(right);
      return this.arithNumber(left, symbol, line) + this.arithNumber(right, symbol, line);
    }

    if (symbol === "✖") {
      if (typeof left === "string" && typeof right === "number") return left.repeat(Math.max(0, Math.trunc(right)));
      if (typeof right === "string" && typeof left === "number") return right.repeat(Math.max(0, Math.trunc(left)));
      return this.arithNumber(left, symbol, line) * this.arithNumber(right, symbol, line);
    }

    if (symbol === "➖" || symbol === "➗" || symbol === "📐") {
      const a = this.arithNumber(left, symbol, line);
      const b = this.arithNumber(right, symbol, line);
      if (symbol === "➖") return a - b;
      if (b === 0) throw new SmileyError("деление на ноль — так нельзя 🙃", line);
      if (symbol === "📐") return a % b;
      return a / b;
    }

    if (symbol === "🟰") return valuesEqual(left, right);
    if (symbol === "🚫") return !valuesEqual(left, right);

    if (["⬆", "⬇", "⬆🟰", "⬇🟰"].includes(symbol)) {
      const [a, b] = this.comparable(left, right, symbol, line);
      if (symbol === "⬆") return a > b;
      if (symbol === "⬇") return a < b;
      if (symbol === "⬆🟰") return a >= b;
      return a <= b;
    }

    throw new SmileyError(`неизвестный оператор ${symbol}`, line);
  }

  comparable(left, right, symbol, line) {
    let a = typeof left === "boolean" ? Number(left) : left;
    let b = typeof right === "boolean" ? Number(right) : right;
    if (typeof a === "number" && typeof b === "number") return [a, b];
    if (typeof a === "string" && typeof b === "string") return [a, b];
    throw new SmileyError(`оператор ${symbol} не умеет сравнивать ${toText(left)} и ${toText(right)}`, line);
  }

  call(callee, args, line) {
    if (callee && callee.kind === "builtin") {
      const builtin = this.builtins.get(callee.name);
      if (args.length < builtin.lo || args.length > builtin.hi) {
        throw new SmileyError(`функция ${callee.name} получила неподходящее число аргументов (${args.length})`, line);
      }
      try {
        return builtin.fn(...args);
      } catch (error) {
        if (error instanceof SmileyError && error.line === null) error.line = line;
        throw error;
      }
    }

    if (callee && callee.kind === "function") {
      if (args.length !== callee.params.length) {
        throw new SmileyError(
          `функция «${callee.name}» ждёт ${plural(callee.params.length, "аргумент", "аргумента", "аргументов")}, а получила ${args.length}`,
          line,
        );
      }
      const local = new Environment(callee.closure);
      callee.params.forEach((param, index) => local.define(param, args[index]));
      try {
        this.execBlock(callee.body, local);
      } catch (error) {
        if (error instanceof ReturnSignal) return error.value;
        throw error;
      }
      return null;
    }

    throw new SmileyError(`${toText(callee)} нельзя вызвать как функцию`, line);
  }
}

function plural(count, one, few, many) {
  const mod10 = count % 10;
  const mod100 = count % 100;
  if (mod10 === 1 && mod100 !== 11) return `${count} ${one}`;
  if (mod10 >= 2 && mod10 <= 4 && !(mod100 >= 12 && mod100 <= 14)) return `${count} ${few}`;
  return `${count} ${many}`;
}

function valuesEqual(left, right) {
  if (typeof left === "boolean" || typeof right === "boolean") return left === right;
  if (typeof left === "number" && typeof right === "number") return left === right;
  if (Array.isArray(left) || Array.isArray(right)) return JSON.stringify(left) === JSON.stringify(right);
  return left === right;
}

function mulberry32(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/* --------------------------- удобный запуск --------------------------- */

export function execute(source, options = {}) {
  const tokens = tokenize(source);
  const program = new Parser(tokens).parseProgram();
  const interpreter = new Interpreter(options);
  interpreter.run(program);
  return interpreter;
}
