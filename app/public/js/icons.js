/* Line icons (24×24, stroke = currentColor). */

const wrap = (body, extra = '') =>
  `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" ${extra}>${body}</svg>`;

export const icons = {
  archive: wrap('<rect x="3" y="4" width="18" height="5" rx="1.6"/><path d="M4.5 9v10.5h15V9"/><path d="M10 13h4"/>'),
  notes: wrap('<path d="M5 3.5h10l4 4v13H5z"/><path d="M15 3.5v4h4"/><path d="M8 11h8M8 14.5h8M8 18h5"/>'),
  calculator: wrap('<rect x="4" y="2.5" width="16" height="19" rx="2.5"/><rect x="7" y="5.5" width="10" height="3.5" rx="1"/><path d="M8 13h1M11.5 13h1M15 13h1M8 17h1M11.5 17h1M15 17h1"/>'),
  disk: wrap('<ellipse cx="12" cy="6" rx="8" ry="3.2"/><path d="M4 6v6c0 1.8 3.6 3.2 8 3.2s8-1.4 8-3.2V6"/><path d="M4 12v6c0 1.8 3.6 3.2 8 3.2s8-1.4 8-3.2v-6"/>'),
  terminal: wrap('<rect x="2.5" y="4" width="19" height="16" rx="3"/><path d="M6.5 9.5 9.5 12l-3 2.5"/><path d="M12.5 15h5"/>'),
  files: wrap('<path d="M3 7.5A2 2 0 0 1 5 5.5h3.6a2 2 0 0 1 1.5.7l1 1.3H19a2 2 0 0 1 2 2v7.5a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>'),
  browser: wrap('<circle cx="12" cy="12" r="9"/><path d="M3.2 9.5h17.6M3.2 14.5h17.6"/><path d="M12 3c2.6 3 2.6 15 0 18M12 3c-2.6 3-2.6 15 0 18"/>'),
  editor: wrap('<path d="M5 3.5h9l5 5v12H5z"/><path d="M14 3.5v5h5"/><path d="M8.5 12.5h7M8.5 16h4.5"/>'),
  monitor: wrap('<path d="M3 4.5h18v11H3z"/><path d="M9 19.5h6M12 15.5v4"/><path d="M6.5 12l2.5-3 2 2.5 2.5-4 2 4.5"/>'),
  settings: wrap('<circle cx="12" cy="12" r="3.2"/><path d="M12 2.6v2.6M12 18.8v2.6M4.4 4.4l1.8 1.8M17.8 17.8l1.8 1.8M2.6 12h2.6M18.8 12h2.6M4.4 19.6l1.8-1.8M17.8 6.2l1.8-1.8"/>'),
  image: wrap('<rect x="3" y="4.5" width="18" height="15" rx="2.5"/><circle cx="8.5" cy="10" r="1.8"/><path d="m4 17 5-4.5 4 3.5 3-2.5 4 3.5"/>'),
  launcher: wrap('<rect x="3.5" y="3.5" width="7" height="7" rx="2"/><rect x="13.5" y="3.5" width="7" height="7" rx="2"/><rect x="3.5" y="13.5" width="7" height="7" rx="2"/><rect x="13.5" y="13.5" width="7" height="7" rx="2"/>'),
  user: wrap('<circle cx="12" cy="8.5" r="3.6"/><path d="M4.8 20c.7-3.6 3.7-5.6 7.2-5.6s6.5 2 7.2 5.6"/>'),
  power: wrap('<path d="M12 3v8"/><path d="M6.3 6.6a8 8 0 1 0 11.4 0"/>'),
  cpu: wrap('<rect x="7" y="7" width="10" height="10" rx="2"/><rect x="3.5" y="3.5" width="17" height="17" rx="3"/><path d="M3.5 10h-2M3.5 14h-2M22.5 10h-2M22.5 14h-2M10 3.5v-2M14 3.5v-2M10 22.5v-2M14 22.5v-2"/>'),
  network: wrap('<path d="M2.5 8.5a15 15 0 0 1 19 0"/><path d="M5.8 12.2a10 10 0 0 1 12.4 0"/><path d="M9.2 15.8a5 5 0 0 1 5.6 0"/><circle cx="12" cy="19.2" r="1.1" fill="currentColor" stroke="none"/>'),
  volume: wrap('<path d="M4 9.5h3l4-3.5v12l-4-3.5H4z"/><path d="M15 9a4 4 0 0 1 0 6"/><path d="M17.8 6.4a7.5 7.5 0 0 1 0 11.2"/>'),
  battery: wrap('<rect x="2.5" y="8" width="17" height="8" rx="2.5"/><path d="M21.5 11v2"/><path d="M5 11h8v2H5z" fill="currentColor" stroke="none"/>'),
  search: wrap('<circle cx="10.5" cy="10.5" r="6"/><path d="m15 15 4.5 4.5"/>'),
  chevronLeft: wrap('<path d="m14 6-6 6 6 6"/>'),
  chevronRight: wrap('<path d="m10 6 6 6-6 6"/>'),
  refresh: wrap('<path d="M20 11a8 8 0 1 0-2.6 6.6"/><path d="M20 4.5V11h-6"/>'),
  home: wrap('<path d="M4 10.5 12 4l8 6.5V20H4z"/><path d="M9.5 20v-5.5h5V20"/>'),
  trash: wrap('<path d="M4 6.5h16"/><path d="M9 6.5V4h6v2.5"/><path d="M6.5 6.5 7.5 20h9l1-13.5"/>'),
  plus: wrap('<path d="M12 5v14M5 12h14"/>'),
  download: wrap('<path d="M12 3.5v11"/><path d="m7.5 10.5 4.5 4.5 4.5-4.5"/><path d="M4.5 19.5h15"/>'),
  save: wrap('<path d="M5 3.5h11l3.5 3.5v13H5z"/><path d="M8.5 3.5v5h7v-5"/><path d="M8.5 20v-6h7v6"/>'),
  close: wrap('<path d="m6 6 12 12M18 6 6 18"/>'),
  minimize: wrap('<path d="M5 12h14"/>'),
  maximize: wrap('<rect x="4.5" y="4.5" width="15" height="15" rx="2"/>'),
  restore: wrap('<rect x="4" y="8" width="12" height="12" rx="2"/><path d="M8 8V5.5A1.5 1.5 0 0 1 9.5 4H18a2 2 0 0 1 2 2v8.5A1.5 1.5 0 0 1 18.5 16H16"/>'),
  lock: wrap('<rect x="5" y="10.5" width="14" height="10" rx="2.5"/><path d="M8.5 10.5V8a3.5 3.5 0 0 1 7 0v2.5"/>'),
  root: wrap('<path d="M4 19 12 5l8 14"/><path d="M8.5 14h7"/>'),
  folder: wrap('<path d="M3 7a2 2 0 0 1 2-2h3.4l1.6 2H19a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>'),
  file: wrap('<path d="M6 3.5h8l4 4V20.5H6z"/><path d="M14 3.5v4h4"/>'),
  code: wrap('<path d="m9 8-4 4 4 4M15 8l4 4-4 4"/>'),
  pin: wrap('<path d="M12 21s7-6.2 7-11a7 7 0 1 0-14 0c0 4.8 7 11 7 11z"/><circle cx="12" cy="10" r="2.6"/>'),
  arrowUp: wrap('<path d="M12 19V5"/><path d="m6 11 6-6 6 6"/>'),
  check: wrap('<path d="m5 12.5 4.5 4.5L19 7"/>'),
  warning: wrap('<path d="M12 4 2.8 20h18.4z"/><path d="M12 9.5v5M12 17.2v.1"/>'),
  info: wrap('<circle cx="12" cy="12" r="9"/><path d="M12 11v5.5M12 7.8v.1"/>'),
};

/** Icon element helper. */
export function icon(name, size = 18) {
  const el = document.createElement('span');
  el.className = 'ico';
  el.style.width = size + 'px';
  el.style.height = size + 'px';
  el.innerHTML = icons[name] || icons.file;
  return el;
}

export function iconHTML(name, size = 18) {
  return `<span class="ico" style="width:${size}px;height:${size}px">${icons[name] || icons.file}</span>`;
}
