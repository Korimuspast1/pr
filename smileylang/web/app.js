import { execute, SmileyError } from "./smiley.js";

const STORAGE_KEY = "smileylang.code.v1";

const EXAMPLES = {
  hello: {
    title: "Привет, мир",
    code: `🙈 Первая программа на смайликах
🗣 "Привет, мир 😊"

📦 имя ⬅ "друг"
🗣 "Привет, " ➕ имя ➕ "!"

🙈 Квадраты чисел от 1 до 5
📦 n ⬅ 1
🔁 n ⬇🟰 5
  🗣 n ✖ n
  n ⬅ n ➕ 1
🔚`,
  },
  fizzbuzz: {
    title: "FizzBuzz",
    code: `🙈 FizzBuzz: 1..20
📦 i ⬅ 1
🔁 i ⬇🟰 20
  ❓ i 📐 15 🟰 0 🤔
    🗣 "FizzBuzz"
  🙃 ❓ i 📐 3 🟰 0 🤔
    🗣 "Fizz"
  🙃 ❓ i 📐 5 🟰 0 🤔
    🗣 "Buzz"
  🙃
    🗣 i
  🔚
  i ⬅ i ➕ 1
🔚`,
  },
  functions: {
    title: "Функции и рекурсия",
    code: `🎁 факториал(n)
  ❓ n ⬇🟰 1 🤔
    ↩ 1
  🔚
  ↩ n ✖ факториал(n ➖ 1)
🔚

🎁 фибоначчи(n)
  ❓ n ⬇ 2 🤔
    ↩ n
  🔚
  ↩ фибоначчи(n ➖ 1) ➕ фибоначчи(n ➖ 2)
🔚

📦 n ⬅ 1
🔁 n ⬇🟰 10
  🗣 n ➕ "! = " ➕ факториал(n)
  n ⬅ n ➕ 1
🔚

🗣 "---"
🗣 "fib(15) = " ➕ фибоначчи(15)`,
  },
  input: {
    title: "Игра: угадай число",
    code: `🙈 Впиши числа в поле «Ввод» справа, по одному на строку
📦 секрет ⬅ случайное(1, 100)
📦 попытка ⬅ 0

🔁 ✅
  📦 ответ ⬅ число(спросить("Твоё число? "))
  попытка ⬅ попытка ➕ 1
  ❓ ответ 🟰 секрет 🤔
    🗣 "🎉 Угадал за " ➕ попытка ➕ " попыток!"
    💔
  🙃 ❓ ответ ⬆ секрет 🤔
    🗣 "⬇ Меньше"
  🙃
    🗣 "⬆ Больше"
  🔚
🔚`,
  },
  emoji: {
    title: "Ёлочка из циклов",
    code: `📦 ряд ⬅ 1
🔁 ряд ⬇🟰 7
  🗣 " " ✖ (7 ➖ ряд) ➕ "🌟" ✖ ряд
  ряд ⬅ ряд ➕ 1
🔚

🔂 3
  🗣 "🎉 🎉 🎉"
🔚`,
  },
  strings: {
    title: "Строки и встроенные функции",
    code: `📦 слово ⬅ "Смайлики"
🗣 "длина = " ➕ длина(слово)
🗣 верхний(слово)
🗣 нижний(слово)
🗣 слово ✖ 3
🗣 "круглое: " ➕ круглое(3.7)

📦 true_слово ⬅ ✅
🗣 "правда? " ➕ true_слово`,
  },
};

const CHEATSHEET = [
  { emoji: "📦", name: "Переменная", code: '📦 x ⬅ 5', alt: "let x = 5 · пусть x = 5", insert: "📦 x ⬅ 5\n" },
  { emoji: "⬅", name: "Присваивание", code: "x ⬅ x ➕ 1", alt: "x = x + 1", insert: "x ⬅ x ➕ 1\n" },
  { emoji: "🗣", name: "Печать", code: '🗣 "Привет"', alt: "print · сказать", insert: '🗣 "Привет"\n' },
  { emoji: "❓", name: "Условие", code: '❓ x ⬆ 3 🤔\n  🗣 "много"\n🙃\n  🗣 "мало"\n🔚', alt: "if / else / end", insert: '❓ x ⬆ 3 🤔\n  🗣 "много"\n🙃\n  🗣 "мало"\n🔚\n' },
  { emoji: "🔁", name: "Цикл «пока»", code: "🔁 x ⬇ 10\n  x ⬅ x ➕ 1\n🔚", alt: "while", insert: "🔁 x ⬇ 10\n  x ⬅ x ➕ 1\n🔚\n" },
  { emoji: "🔂", name: "Повторить N раз", code: '🔂 3\n  🗣 "😊"\n🔚', alt: "repeat", insert: '🔂 3\n  🗣 "😊"\n🔚\n' },
  { emoji: "🎁", name: "Функция", code: "🎁 квадрат(n)\n  ↩ n ✖ n\n🔚\n🗣 квадрат(7)", alt: "func / return", insert: "🎁 квадрат(n)\n  ↩ n ✖ n\n🔚\n" },
  { emoji: "💔", name: "Выйти из цикла", code: "💔", alt: "break · стоп", insert: "💔\n" },
  { emoji: "⏭", name: "Пропустить итерацию", code: "⏭", alt: "continue · дальше", insert: "⏭\n" },
  { emoji: "➕ ➖ ✖ ➗ 📐", name: "Математика", code: "2 ➕ 3 ✖ 4\n7 📐 3", alt: "+ - * / %", insert: "🗣 2 ➕ 3 ✖ 4\n" },
  { emoji: "🟰 🚫 ⬆ ⬇ ⬆🟰 ⬇🟰", name: "Сравнения", code: "x 🟰 3 🤝 y ⬆ 1", alt: "== != > < >= <=", insert: "🗣 x 🟰 3\n" },
  { emoji: "🤝 🔀 🙅", name: "Логика", code: "✅ 🤝 ❌\n✅ 🔀 ❌\n🙅 ❌", alt: "and or not · и или не", insert: "🗣 ✅ 🤝 ❌\n" },
  { emoji: "✅ ❌", name: "Правда и ложь", code: '❓ ✅ 🤔 🗣 "да" 🔚', alt: "true / false", insert: "🗣 ✅\n" },
  { emoji: "🙈", name: "Комментарий", code: "🙈 это комментарий\n# и это", alt: "# или //", insert: "🙈 комментарий\n" },
  { emoji: "🧰", name: "Встроенные функции", code: 'длина("ёжик")\nспросить("Имя? ")\nслучайное(1, 6)\nчисло("42")\nстрока(7)\nверхний("abc")\nкруглое(2.6)\nвремя()', alt: "len / input / random / int / str", insert: '🗣 длина("ёжик")\n' },
];

const codeEl = document.getElementById("code");
const consoleEl = document.getElementById("console");
const stdinEl = document.getElementById("stdin");
const gutterEl = document.getElementById("gutter");
const statsEl = document.getElementById("stats");
const badgeEl = document.getElementById("exit-badge");
const examplesEl = document.getElementById("examples");
const runButton = document.getElementById("run");

let outputTimer = null;
let queuedLines = [];

/* ------------------------------ редактор ------------------------------ */

function updateGutter() {
  const lines = codeEl.value.split("\n").length;
  gutterEl.textContent = Array.from({ length: lines }, (_, i) => i + 1).join("\n");
  gutterEl.scrollTop = codeEl.scrollTop;
}

function saveCode() {
  try {
    localStorage.setItem(STORAGE_KEY, codeEl.value);
  } catch {
    /* приватный режим — не страшно */
  }
}

function insertSnippet(text) {
  const start = codeEl.selectionStart;
  const end = codeEl.selectionEnd;
  codeEl.value = codeEl.value.slice(0, start) + text + codeEl.value.slice(end);
  const caret = start + text.length;
  codeEl.setSelectionRange(caret, caret);
  codeEl.focus();
  updateGutter();
  saveCode();
}

codeEl.addEventListener("input", () => {
  updateGutter();
  saveCode();
});
codeEl.addEventListener("scroll", () => {
  gutterEl.scrollTop = codeEl.scrollTop;
});
codeEl.addEventListener("keydown", (event) => {
  if (event.key === "Tab") {
    event.preventDefault();
    insertSnippet("  ");
  }
  if ((event.ctrlKey || event.metaKey) && event.key === "Enter") {
    event.preventDefault();
    runProgram();
  }
});

/* -------------------------------- вывод -------------------------------- */

function resetConsole() {
  consoleEl.innerHTML = "";
  queuedLines = [];
  if (outputTimer) {
    clearTimeout(outputTimer);
    outputTimer = null;
  }
}

function paintQueue() {
  outputTimer = null;
  const fragment = document.createDocumentFragment();
  for (const [text, isError] of queuedLines) {
    const line = document.createElement("span");
    line.className = isError ? "line err" : "line";
    line.textContent = text === "" ? " " : text;
    fragment.appendChild(line);
  }
  queuedLines = [];
  consoleEl.appendChild(fragment);
  consoleEl.scrollTop = consoleEl.scrollHeight;
}

function schedulePaint() {
  if (outputTimer) return;
  outputTimer = setTimeout(paintQueue, 40);
}

function printLine(text, isError = false) {
  queuedLines.push([text, isError]);
  schedulePaint();
}

function setBadge(text, state) {
  badgeEl.textContent = text;
  badgeEl.className = "badge" + (state ? " " + state : "");
}

/* -------------------------------- запуск ------------------------------- */

function runProgram() {
  resetConsole();
  const source = codeEl.value;
  const inputLines = stdinEl.value.split("\n").filter((line) => line.trim() !== "");
  const started = performance.now();

  let interpreter = null;
  let error = null;

  try {
    interpreter = execute(source, {
      input: inputLines,
      maxSteps: 300000,
      maxOutput: 2000,
    });
    for (const line of interpreter.output) printLine(line);
  } catch (caught) {
    error = caught;
    if (caught instanceof SmileyError) {
      printLine(caught.line ? `😵 Строка ${caught.line}: ${caught.smileyMessage}` : `😵 ${caught.smileyMessage}`, true);
    } else {
      printLine(`😵 внутренняя ошибка: ${caught.message}`, true);
    }
  }

  const elapsed = Math.max(0, performance.now() - started);
  paintQueue();
  if (consoleEl.children.length === 0) {
    const empty = document.createElement("div");
    empty.className = "placeholder";
    empty.textContent = "Программа ничего не вывела 🤐";
    consoleEl.appendChild(empty);
  }

  if (error) {
    setBadge("ошибка", "err");
    statsEl.textContent = `${elapsed.toFixed(1)} мс · шагов: ${interpreter ? interpreter.steps : "—"}`;
    return;
  }

  setBadge("готово", "ok");
  const lineCount = interpreter.output.length;
  statsEl.textContent = `${lineCount} стр. вывода · ${elapsed.toFixed(1)} мс · шагов: ${interpreter.steps}`;
}

runButton.addEventListener("click", runProgram);

document.getElementById("clear").addEventListener("click", () => {
  resetConsole();
  const placeholder = document.createElement("div");
  placeholder.className = "placeholder";
  placeholder.textContent = "Нажми «Запустить» — результат появится тут 🙂";
  consoleEl.appendChild(placeholder);
  setBadge("готов", "");
  statsEl.textContent = "";
});

document.getElementById("reset").addEventListener("click", () => {
  codeEl.value = EXAMPLES.hello.code;
  updateGutter();
  saveCode();
});

/* ---------------------------- примеры и карточки ----------------------- */

function setupExamples() {
  const placeholder = document.createElement("option");
  placeholder.value = "";
  placeholder.textContent = "Примеры…";
  examplesEl.appendChild(placeholder);

  for (const [key, example] of Object.entries(EXAMPLES)) {
    const option = document.createElement("option");
    option.value = key;
    option.textContent = example.title;
    examplesEl.appendChild(option);
  }

  examplesEl.addEventListener("change", () => {
    const chosen = EXAMPLES[examplesEl.value];
    if (!chosen) return;
    codeEl.value = chosen.code;
    examplesEl.value = "";
    updateGutter();
    saveCode();
    codeEl.focus();
  });
}

function setupCards() {
  const container = document.getElementById("cards");
  for (const item of CHEATSHEET) {
    const card = document.createElement("button");
    card.type = "button";
    card.className = "card";

    const emoji = document.createElement("span");
    emoji.className = "emoji";
    emoji.textContent = item.emoji;

    const name = document.createElement("span");
    name.className = "name";
    name.textContent = item.name;

    const pre = document.createElement("pre");
    pre.textContent = item.code;

    const alt = document.createElement("span");
    alt.className = "alt";
    alt.textContent = item.alt;

    card.append(emoji, name, pre, alt);
    card.addEventListener("click", () => insertSnippet(item.insert));
    container.appendChild(card);
  }
}

/* --------------------------------- старт ------------------------------- */

function boot() {
  setupExamples();
  setupCards();

  let saved = null;
  try {
    saved = localStorage.getItem(STORAGE_KEY);
  } catch {
    saved = null;
  }
  codeEl.value = saved || EXAMPLES.hello.code;
  updateGutter();
  setBadge("готов", "");

  // приветственный запуск
  runProgram();
}

boot();
