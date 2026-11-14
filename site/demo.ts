import "./ui.css";
import { OceanEngine, SHARK_KINDS, SHARK_LABELS, makeSharkSpecs, type SharkKind } from "./src";

// ── state ──────────────────────────────────────────────────────────────

type FishLevel = "low" | "auto" | "high";
type Format = "webm" | "mp4";

const FISH_COUNT: Record<FishLevel, number | undefined> = { low: 80, auto: undefined, high: 520 };

const SIZES = [
  { label: "1920×1080", width: 1920, height: 1080, note: "Full HD" },
  { label: "1280×720", width: 1280, height: 720, note: "HD" },
  { label: "1080×1920", width: 1080, height: 1920, note: "вертикаль" },
  { label: "1080×1080", width: 1080, height: 1080, note: "квадрат" },
];

const DURATIONS = [5, 10, 15, 30, 60];

const state = {
  kinds: new Set<SharkKind>(SHARK_KINDS),
  count: 5,
  fish: "auto" as FishLevel,
  seconds: 10,
  size: 0,
  fps: 60,
  format: "webm" as Format,
};

// URL overrides: ?sharks=tiger,mako&count=3&fish=high&seconds=30&size=1920x1080&fps=30
const params = new URLSearchParams(location.search);
const urlKinds = params.get("sharks")?.split(",").map((k) => k.trim()).filter(Boolean) as SharkKind[] | undefined;
if (urlKinds?.length) {
  const valid = urlKinds.filter((k) => (SHARK_KINDS as string[]).includes(k));
  if (valid.length) state.kinds = new Set(valid);
}
const urlCount = Number(params.get("count"));
if (Number.isFinite(urlCount) && urlCount >= 1) state.count = Math.min(24, Math.round(urlCount));
const urlFish = params.get("fish");
if (urlFish === "low" || urlFish === "auto" || urlFish === "high") state.fish = urlFish;
const urlSeconds = Number(params.get("seconds"));
if (Number.isFinite(urlSeconds) && urlSeconds > 0) state.seconds = Math.min(120, Math.round(urlSeconds));
const urlSize = params.get("size")?.split("x").map(Number);
if (urlSize?.length === 2) {
  const i = SIZES.findIndex((s) => s.width === urlSize[0] && s.height === urlSize[1]);
  if (i >= 0) state.size = i;
}
const urlFps = Number(params.get("fps"));
if (urlFps === 30 || urlFps === 60) state.fps = urlFps;

// ── helpers ────────────────────────────────────────────────────────────

const $ = <T extends HTMLElement>(sel: string) => document.querySelector(sel) as T;
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

function pickMime(format: Format) {
  if (typeof MediaRecorder === "undefined") return null;
  const list =
    format === "mp4"
      ? ["video/mp4;codecs=avc1.42E01E", "video/mp4;codecs=h264", "video/mp4"]
      : ["video/webm;codecs=vp9", "video/webm;codecs=vp8", "video/webm"];
  return list.find((m) => MediaRecorder.isTypeSupported(m)) ?? null;
}

function specs() {
  return makeSharkSpecs([...state.kinds], state.count);
}

function timeLabel(ms: number) {
  const total = Math.max(0, Math.round(ms / 1000));
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, "0")}`;
}

// ── markup ─────────────────────────────────────────────────────────────

document.body.innerHTML = `
<canvas id="ocean" aria-hidden="true"></canvas>

<aside class="panel" id="panel">
  <div class="head" id="panelHead" role="button" tabindex="0" aria-expanded="true">
    <h1>ascii sharks</h1>
    <small id="panelToggle">свернуть</small>
  </div>
  <div class="body">
    <div class="group">
      <div class="label">Акулы <b id="countLabel">5</b></div>
      <div class="chips" id="chips"></div>
      <div class="rowline" style="margin-top:7px">
        <button class="mini" id="allKinds">все 14</button>
        <button class="mini" id="noKinds">сбросить</button>
      </div>
    </div>

    <div class="group">
      <label for="count">Сколько акул в кадре</label>
      <input type="range" id="count" min="1" max="16" step="1" value="5" />
    </div>

    <div class="group">
      <label for="fish">Рыбы</label>
      <select id="fish">
        <option value="low">мало</option>
        <option value="auto" selected>как получится</option>
        <option value="high">густая стая</option>
      </select>
    </div>

    <div class="group">
      <div class="label">Длительность видео <b id="secondsLabel">10 с</b></div>
      <div class="rowline" id="durations"></div>
      <input type="number" id="seconds" min="1" max="120" step="1" value="10" style="margin-top:7px" />
    </div>

    <div class="group">
      <label for="size">Разрешение</label>
      <select id="size">
        ${SIZES.map((s, i) => `<option value="${i}"${i === state.size ? " selected" : ""}>${s.label} — ${s.note}</option>`).join("")}
      </select>
    </div>

    <div class="group">
      <div class="label">Формат</div>
      <div class="seg" id="format">
        <button data-format="webm">WebM</button>
        <button data-format="mp4">MP4</button>
      </div>
      <div class="seg" id="fps" style="margin-top:7px">
        <button data-fps="30">30 fps</button>
        <button data-fps="60">60 fps</button>
      </div>
    </div>

    <button class="cta" id="record">Записать видео</button>
    <div class="hint">
      <kbd>клик</kbd> по воде — всплеск, курсор пугает рыб.<br />
      Запись идёт в реальном времени, файл скачивается сразу после конца.
    </div>
  </div>
</aside>
`;

// ── live scene ─────────────────────────────────────────────────────────

const ocean = $<HTMLCanvasElement>("#ocean");
let engine = new OceanEngine(ocean, specs(), { fishCount: FISH_COUNT[state.fish] });

function reloadLive() {
  const old = engine;
  engine = new OceanEngine(ocean, specs(), { fishCount: FISH_COUNT[state.fish] });
  old.destroy();
  // Keep pointer input working on the fresh canvas.
  ocean.style.pointerEvents = "auto";
}

// ── panel wiring ───────────────────────────────────────────────────────

const chips = $("#chips");
chips.innerHTML = SHARK_KINDS.map(
  (kind) => `<button class="chip" data-kind="${kind}" aria-pressed="${state.kinds.has(kind)}">${SHARK_LABELS[kind]}</button>`,
).join("");

function syncChips() {
  for (const el of chips.querySelectorAll<HTMLButtonElement>(".chip")) {
    el.setAttribute("aria-pressed", String(state.kinds.has(el.dataset.kind as SharkKind)));
  }
  const countEl = $("#countLabel");
  countEl.textContent = state.kinds.size ? String(state.count) : "—";
}

chips.addEventListener("click", (e) => {
  const btn = (e.target as HTMLElement).closest<HTMLButtonElement>(".chip");
  if (!btn) return;
  const kind = btn.dataset.kind as SharkKind;
  if (state.kinds.has(kind)) state.kinds.delete(kind);
  else state.kinds.add(kind);
  syncChips();
  reloadLive();
});

$("#allKinds").addEventListener("click", () => {
  state.kinds = new Set(SHARK_KINDS);
  syncChips();
  reloadLive();
});

$("#noKinds").addEventListener("click", () => {
  state.kinds = new Set<SharkKind>(["classic"]);
  state.count = 4;
  ($<HTMLInputElement>("#count")).value = "4";
  syncChips();
  reloadLive();
});

const countInput = $<HTMLInputElement>("#count");
countInput.value = String(state.count);
countInput.addEventListener("input", () => {
  state.count = Number(countInput.value);
  $("#countLabel").textContent = String(state.count);
});
countInput.addEventListener("change", reloadLive);

$<HTMLSelectElement>("#fish").value = state.fish;
$("#fish").addEventListener("change", (e) => {
  state.fish = (e.target as HTMLSelectElement).value as FishLevel;
  reloadLive();
});

const durations = $("#durations");
durations.innerHTML = DURATIONS.map((s) => `<button class="mini" data-sec="${s}">${s} с</button>`).join("");
durations.addEventListener("click", (e) => {
  const btn = (e.target as HTMLElement).closest<HTMLButtonElement>("[data-sec]");
  if (!btn) return;
  setSeconds(Number(btn.dataset.sec));
});

const secondsInput = $<HTMLInputElement>("#seconds");
function setSeconds(value: number) {
  state.seconds = Math.min(120, Math.max(1, Math.round(value) || 1));
  secondsInput.value = String(state.seconds);
  $("#secondsLabel").textContent = `${state.seconds} с`;
  for (const b of durations.querySelectorAll<HTMLButtonElement>("[data-sec]")) {
    b.style.borderStyle = Number(b.dataset.sec) === state.seconds ? "solid" : "dashed";
    b.style.color = Number(b.dataset.sec) === state.seconds ? "var(--accent)" : "";
  }
}
setSeconds(state.seconds);
secondsInput.addEventListener("change", () => setSeconds(Number(secondsInput.value)));

$<HTMLSelectElement>("#size").addEventListener("change", (e) => {
  state.size = Number((e.target as HTMLSelectElement).value);
});

// Formats the browser can actually mux.
const formatWrap = $("#format");
const mp4Mime = pickMime("mp4");
const webmMime = pickMime("webm");
if (!webmMime) {
  formatWrap.querySelector<HTMLButtonElement>('[data-format="webm"]')!.disabled = true;
}
if (!mp4Mime) {
  formatWrap.querySelector<HTMLButtonElement>('[data-format="mp4"]')!.disabled = true;
}
if (!webmMime && mp4Mime) state.format = "mp4";

function syncFormat() {
  for (const b of formatWrap.querySelectorAll<HTMLButtonElement>("[data-format]")) {
    b.setAttribute("aria-pressed", String(b.dataset.format === state.format));
  }
}
formatWrap.addEventListener("click", (e) => {
  const btn = (e.target as HTMLElement).closest<HTMLButtonElement>("[data-format]");
  if (!btn || btn.disabled) return;
  state.format = btn.dataset.format as Format;
  syncFormat();
});
syncFormat();

const fpsWrap = $("#fps");
function syncFps() {
  for (const b of fpsWrap.querySelectorAll<HTMLButtonElement>("[data-fps]")) {
    b.setAttribute("aria-pressed", String(Number(b.dataset.fps) === state.fps));
  }
}
fpsWrap.addEventListener("click", (e) => {
  const btn = (e.target as HTMLElement).closest<HTMLButtonElement>("[data-fps]");
  if (!btn) return;
  state.fps = Number(btn.dataset.fps);
  syncFps();
});
syncFps();

const panel = $("#panel");
$("#panelHead").addEventListener("click", () => {
  panel.classList.toggle("collapsed");
  const open = !panel.classList.contains("collapsed");
  $("#panelHead").setAttribute("aria-expanded", String(open));
  $("#panelToggle").textContent = open ? "свернуть" : "настройки";
});

function onKey(e: KeyboardEvent) {
  if (e.key.toLowerCase() === "h" && !recording) {
    panel.classList.toggle("collapsed");
    const open = !panel.classList.contains("collapsed");
    $("#panelHead").setAttribute("aria-expanded", String(open));
    $("#panelToggle").textContent = open ? "свернуть" : "настройки";
  }
}
window.addEventListener("keydown", onKey);

syncChips();

// ── video export ───────────────────────────────────────────────────────

let recording = false;
let abortRecording = false;

async function record() {
  if (recording) return;
  const mime = pickMime(state.format);
  if (!mime) {
    alert("Этот браузер не умеет записывать видео в выбранном формате. Попробуй другой формат или Chrome.");
    return;
  }

  const { width, height } = SIZES[state.size];
  const seconds = state.seconds;
  const fps = state.fps;
  const specsForVideo = specs();
  const fish = FISH_COUNT[state.fish];

  recording = true;
  abortRecording = false;
  const recordBtn = $<HTMLButtonElement>("#record");
  recordBtn.disabled = true;

  const overlay = document.createElement("div");
  overlay.className = "overlay";
  overlay.innerHTML = `
    <div class="stage">
      <canvas id="stageCanvas"></canvas>
      <div class="stage-bar">
        <span class="rec-dot" hidden></span>
        <span class="stage-status" id="stageStatus">Подготовка…</span>
        <span class="progress"><i id="stageProgress"></i></span>
        <button id="stageCancel">Отмена</button>
        <a class="download" id="stageDownload" hidden download>Скачать файл</a>
        <button id="stageAgain" hidden>Ещё раз</button>
        <button class="primary" id="stageClose" hidden>Готово</button>
      </div>
      <div class="note" id="stageNote">${width}×${height} · ${fps} fps · ${state.format.toUpperCase()} · не переключай вкладку во время записи</div>
    </div>
  `;
  document.body.appendChild(overlay);

  const stageCanvas = overlay.querySelector<HTMLCanvasElement>("#stageCanvas")!;
  const statusEl = overlay.querySelector<HTMLElement>("#stageStatus")!;
  const barEl = overlay.querySelector<HTMLElement>("#stageProgress")!;
  const dotEl = overlay.querySelector<HTMLElement>(".rec-dot")!;
  const cancelBtn = overlay.querySelector<HTMLButtonElement>("#stageCancel")!;
  const downloadEl = overlay.querySelector<HTMLAnchorElement>("#stageDownload")!;
  const againBtn = overlay.querySelector<HTMLButtonElement>("#stageAgain")!;
  const closeBtn = overlay.querySelector<HTMLButtonElement>("#stageClose")!;
  const noteEl = overlay.querySelector<HTMLElement>("#stageNote")!;

  // Live scene sleeps while we render the video, so recording keeps a steady frame rate.
  ocean.style.display = "none";

  const stageEngine = new OceanEngine(stageCanvas, specsForVideo, { fishCount: fish, size: { width, height } });
  stageEngine.refresh();

  let objectUrl: string | null = null;
  let cancelled = false;

  const cleanup = () => {
    stageEngine.destroy();
    if (objectUrl) setTimeout(() => URL.revokeObjectURL(objectUrl!), 20_000);
    overlay.remove();
    ocean.style.display = "";
    ocean.style.pointerEvents = "auto";
    recording = false;
    recordBtn.disabled = false;
  };

  const cancel = () => {
    cancelled = true;
    abortRecording = true;
    cleanup();
  };
  cancelBtn.addEventListener("click", cancel);
  const escHandler = (e: KeyboardEvent) => {
    if (e.key === "Escape") cancel();
  };
  window.addEventListener("keydown", escHandler);

  await sleep(1100);
  if (cancelled) return;

  let stream: MediaStream;
  try {
    stream = stageCanvas.captureStream(fps);
  } catch {
    alert("Браузер не дал захватить холст. Попробуй Chrome.");
    cancel();
    return;
  }

  const bitrate = width * height * fps * 0.18;
  const rec = new MediaRecorder(stream, { mimeType: mime, videoBitsPerSecond: bitrate });
  const chunks: BlobPart[] = [];
  rec.ondataavailable = (e) => {
    if (e.data.size) chunks.push(e.data);
  };

  const finished = new Promise<Blob>((resolve) => {
    rec.onstop = () => resolve(new Blob(chunks, { type: mime }));
  });

  dotEl.hidden = false;
  statusEl.innerHTML = `Запись <b>0:00</b> / ${timeLabel(seconds * 1000)}`;
  noteEl.textContent = `${width}×${height} · ${fps} fps · ${state.format.toUpperCase()} · ${specsForVideo.length} акул${state.kinds.size ? ` (${[...state.kinds].map((k) => SHARK_LABELS[k]).join(", ")})` : ""}`;

  rec.start(250);
  const started = performance.now();

  await new Promise<void>((resolve) => {
    const tick = () => {
      if (abortRecording) {
        resolve();
        return;
      }
      const elapsed = performance.now() - started;
      const progress = Math.min(1, elapsed / (seconds * 1000));
      barEl.style.width = `${progress * 100}%`;
      statusEl.innerHTML = `Запись <b>${timeLabel(elapsed)}</b> / ${timeLabel(seconds * 1000)}`;
      if (progress >= 1) resolve();
      else requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  });

  try {
    if (rec.state !== "inactive") rec.stop();
  } catch {
    /* ignore */
  }
  for (const track of stream.getTracks()) track.stop();

  if (cancelled) {
    window.removeEventListener("keydown", escHandler);
    return;
  }

  const blob = await finished;
  if (!blob.size) {
    statusEl.textContent = "Не получилось — попробуй другой формат";
    cancelBtn.hidden = true;
    dotEl.hidden = true;
    closeBtn.hidden = false;
    closeBtn.addEventListener("click", cleanup);
    return;
  }

  objectUrl = URL.createObjectURL(blob);
  const ext = state.format === "mp4" ? "mp4" : "webm";
  const name = `ascii-sharks-${specsForVideo.length}sharks-${seconds}s-${width}x${height}.${ext}`;

  // Play the result back in place of the live stage.
  const video = document.createElement("video");
  video.src = objectUrl;
  video.controls = true;
  video.loop = true;
  video.autoplay = true;
  video.muted = true;
  video.playsInline = true;
  video.style.cssText =
    "display:block;width:100%;max-height:72vh;margin:0 auto;background:#000;border:1px solid var(--line);border-radius:10px";
  stageCanvas.replaceWith(video);

  dotEl.hidden = true;
  cancelBtn.hidden = true;
  downloadEl.hidden = false;
  downloadEl.href = objectUrl;
  downloadEl.download = name;
  downloadEl.textContent = `Скачать ${(blob.size / 1e6).toFixed(1)} МБ`;
  againBtn.hidden = false;
  closeBtn.hidden = false;
  statusEl.innerHTML = `Готово · <b>${timeLabel(seconds * 1000)}</b>`;
  barEl.style.width = "100%";
  noteEl.textContent = `${name} · ${(blob.size / 1e6).toFixed(1)} МБ`;
  window.removeEventListener("keydown", escHandler);

  closeBtn.addEventListener("click", cleanup);
  againBtn.addEventListener("click", () => {
    cleanup();
    void record();
  });
}

$("#record").addEventListener("click", () => void record());

// ── boot ───────────────────────────────────────────────────────────────

// Redraw the live scene when the window changes shape.
window.addEventListener("resize", () => engine.refresh());
