/* Lazy loader for the vendored CodeMirror 5 (MIT, github.com/codemirror/codemirror5). */

const BASE = '/vendor/codemirror';
let cssLoaded = null;
let coreLoading = null;

function loadCss() {
  if (cssLoaded) return cssLoaded;
  cssLoaded = new Promise((resolve) => {
    const l = document.createElement('link');
    l.rel = 'stylesheet';
    l.href = `${BASE}/codemirror.css`;
    l.onload = resolve;
    l.onerror = resolve;
    document.head.appendChild(l);
  });
  return cssLoaded;
}

import { call } from './api.js';

/** Evaluate vendor code delivered as JSON — bypasses static-asset problems. */
async function loadViaApi(name) {
  const asset = await call('asset', { name }, 20000);
  if (!asset || !asset.code) throw new Error('пустой ассет ' + name);
  // eslint-disable-next-line no-new-func
  new Function(asset.code)();
}

function loadScript(src) {
  if (loadedScripts.has(src)) return Promise.resolve();
  loadedScripts.add(src);
  return new Promise((resolve, reject) => {
    const s = document.createElement('script');
    s.src = src;
    s.async = true;
    s.onload = resolve;
    s.onerror = () => reject(new Error('не загрузился ' + src));
    document.head.appendChild(s);
  });
}

const loadedScripts = new Set();

const MODE_FILES = {
  javascript: ['mode/javascript/javascript.js', 'mode/clike/clike.js'],
  json: ['mode/javascript/javascript.js'],
  python: ['mode/python/python.js'],
  shell: ['mode/shell/shell.js'],
  css: ['mode/css/css.js'],
  html: ['mode/xml/xml.js', 'mode/javascript/javascript.js', 'mode/css/css.js', 'mode/htmlmixed/htmlmixed.js'],
  xml: ['mode/xml/xml.js'],
  markdown: ['mode/markdown/markdown.js', 'mode/xml/xml.js'],
  yaml: ['mode/yaml/yaml.js'],
  sql: ['mode/sql/sql.js'],
};

export const CM_MODES = {
  js: 'javascript', mjs: 'javascript', cjs: 'javascript', json: 'javascript',
  py: 'python', sh: 'shell', bash: 'shell', css: 'css', html: 'html', htm: 'html',
  xml: 'xml', svg: 'xml', md: 'markdown', markdown: 'markdown', yml: 'yaml',
  yaml: 'yaml', sql: 'sql', txt: null, log: null,
};

export const modeFor = (name) => CM_MODES[(String(name || '').split('.').pop() || '').toLowerCase()] ?? null;

/** Loads CodeMirror core (+ the requested modes) and resolves to window.CodeMirror. */
export async function loadCodeMirror(modes = []) {
  await loadCss();
  if (!window.CodeMirror) {
    if (!coreLoading) {
      coreLoading = (async () => {
        try {
          await loadScript(`${BASE}/lib/codemirror.js`);
        } catch {}
        if (!window.CodeMirror) {
          // прокси не отдал статику — тянем тот же файл через JSON-API
          await loadViaApi('codemirror');
        }
        if (!window.CodeMirror) throw new Error('CodeMirror не инициализировался');
        return window.CodeMirror;
      })();
    }
    await coreLoading;
  }
  const CM = window.CodeMirror;
  const wanted = new Set();
  for (const m of modes.filter(Boolean)) {
    for (const f of MODE_FILES[m] || []) wanted.add(f);
  }
  for (const f of wanted) {
    try {
      await loadScript(`${BASE}/${f}`);
    } catch {
      const alias = f.replace('mode/', 'mode-').replace(/\.js$/, '').replace(/\//g, '-');
      // mode/javascript/javascript.js -> mode-javascript
      const key = 'mode-' + f.split('/')[1];
      try { await loadViaApi(key || alias); } catch {}
    }
  }
  return CM;
}
