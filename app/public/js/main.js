/* boot: register apps, mount the desktop */

import { createDesktop } from './desktop.js';
import { applyTheme, state, WALLPAPERS, on } from './store.js';

import terminal, { rootTerminal } from './apps/terminal.js';
import files from './apps/files.js';
import browser from './apps/browser.js';
import editor from './apps/editor.js';
import viewer from './apps/viewer.js';
import monitor from './apps/monitor.js';
import settings from './apps/settings.js';
import calculator from './apps/calculator.js';
import notes from './apps/notes.js';
import archives from './apps/archives.js';
import disk from './apps/disk.js';

const apps = [terminal, rootTerminal, files, browser, editor, notes, viewer, calculator, monitor, disk, archives, settings];

applyTheme();

const wallEl = document.getElementById('wallpaper');
const paintWallpaper = () => {
  const w = WALLPAPERS.find((x) => x.id === state.wallpaper) || WALLPAPERS[0];
  wallEl.style.background = w.css;
};
paintWallpaper();
on('wallpaper', paintWallpaper);

const desktop = createDesktop(apps);

setTimeout(() => {
  desktop.notify(
    'Добро пожаловать в sandboxOS',
    '«Приложения» (или Super) — меню: браузер с вкладками, терминал, файлы, заметки, калькулятор, монитор, анализ диска, архивы. Ctrl+Alt+T/F/B — быстрый запуск.',
    'info',
    8000
  );
}, 700);
