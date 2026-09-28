/*
 * Premium Look — визуальный YouTube Premium (только оформление).
 *
 * Расширение НЕ включает настоящие функции Premium (без рекламы,
 * скачивание, фоновое воспроизведение и т.п.) — оно лишь меняет
 * внешний вид страниц YouTube.
 */
(() => {
  "use strict";

  const DEFAULTS = {
    enabled: true,  // общий выключатель
    logo: true,     // логотип «YouTube Premium» в шапке
    avatar: true,   // золотое кольцо + значок на аватаре
    account: true,  // карточка Premium в меню аккаунта
    verified: true, // галочка «верифицирован» как у MrBeast
    favicon: true,  // значок вкладки
  };

  const FEATURES = ["logo", "avatar", "account", "verified", "favicon"];

  // Красная «кнопка проигрывания» из логотипа YouTube Premium
  const SVG_PLAY =
    '<svg viewBox="0 0 28 20" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">' +
    '<rect width="28" height="20" rx="6.2" fill="#f00"/>' +
    '<path d="M11.4 5.9v8.2L18.6 10z" fill="#fff"/></svg>';

  let settings = { ...DEFAULTS };
  let started = false;
  let queued = false;

  /* ---------------- настройки ---------------- */

  function storageApi() {
    if (typeof chrome !== "undefined" && chrome.storage && chrome.storage.local) return chrome.storage;
    if (typeof browser !== "undefined" && browser.storage && browser.storage.local) return browser.storage;
    return null;
  }

  // Быстрое чтение из localStorage: позволяет применить стили
  // до первой отрисовки страницы, чтобы логотип не «мигал».
  function readCached() {
    try {
      const raw = localStorage.getItem("ytpv-settings");
      if (raw) return { ...DEFAULTS, ...JSON.parse(raw) };
    } catch (e) { /* ignore */ }
    return null;
  }

  function writeCached() {
    try { localStorage.setItem("ytpv-settings", JSON.stringify(settings)); } catch (e) { /* ignore */ }
  }

  function loadSettings() {
    const api = storageApi();
    if (!api) return;
    try {
      api.local.get(DEFAULTS, (res) => {
        if (res && typeof res === "object") {
          settings = { ...DEFAULTS, ...res };
          writeCached();
          boot();
        }
      });
    } catch (e) { /* ignore */ }
  }

  function watchSettings() {
    const api = storageApi();
    if (!api || !api.onChanged) return;
    api.onChanged.addListener((changes, area) => {
      if (area && area !== "local") return;
      let hit = false;
      for (const key of Object.keys(DEFAULTS)) {
        if (key in changes) { settings[key] = changes[key].newValue; hit = true; }
      }
      if (hit) {
        writeCached();
        applyClasses();
        applyFavicon();
        decorate();
      }
    });
  }

  /* ---------------- классы на <html> ---------------- */

  function applyClasses() {
    const root = document.documentElement;
    if (!root) return;
    root.classList.toggle("ytpv-on", !!settings.enabled);
    for (const f of FEATURES) {
      root.classList.toggle("ytpv-f-" + f, !!settings.enabled && !!settings[f]);
    }
  }

  /* ---------------- логотип Premium ---------------- */

  function decorateLogo() {
    const anchors = document.querySelectorAll("ytd-logo a");
    for (const a of anchors) {
      let node = a.querySelector(":scope > .ytpv-logo");
      if (!node) {
        node = document.createElement("span");
        node.className = "ytpv-logo";
        node.setAttribute("aria-label", "YouTube Premium (визуально)");
        node.innerHTML = SVG_PLAY + '<span class="ytpv-logo-word">Premium</span>';
        a.appendChild(node);
      }
      // Если YouTube сейчас показывает иконку без надписи
      // (узкий логотип, viewBox примерно 28x32) — скрываем и текст.
      const svg = a.querySelector("svg");
      const box = svg ? (svg.getAttribute("viewBox") || "") : "";
      const width = parseFloat(box.trim().split(/\s+/)[2]);
      node.classList.toggle("ytpv-mini", !Number.isNaN(width) && width <= 32);
    }
  }

  /* ---------------- меню аккаунта ---------------- */

  const ACCOUNT_NAME_SELECTORS = [
    "ytd-account-item-renderer #account-name",
    "ytd-account-item-renderer .account-name",
    "ytd-account-item-renderer yt-formatted-string",
    "ytd-active-account-header-renderer #account-name",
    "ytd-active-account-header-renderer yt-formatted-string",
  ];

  function decorateAccountMenu() {
    const dialog = document.querySelector("ytd-popup-container tp-yt-paper-dialog");
    if (!dialog) return;

    const isAccountMenu = !!dialog.querySelector(
      "ytd-account-item-renderer, ytd-active-account-header-renderer"
    );

    // 1) Карточка «YouTube Premium — подписка активна» в начале меню
    if (isAccountMenu && !dialog.querySelector(".ytpv-card")) {
      const host =
        dialog.querySelector("ytd-multi-page-menu-renderer #menu") ||
        dialog.querySelector("ytd-multi-page-menu-renderer") ||
        dialog;
      const card = document.createElement("div");
      card.className = "ytpv-card";
      card.innerHTML =
        '<span class="ytpv-card-icon">' + SVG_PLAY + "</span>" +
        '<span class="ytpv-card-text">' +
        '<span class="ytpv-card-title">YouTube Premium</span>' +
        '<span class="ytpv-card-sub">Подписка активна</span>' +
        "</span>" +
        '<span class="ytpv-card-check"></span>';
      host.insertBefore(card, host.firstChild);
    }

    // 2) Галочка «верифицирован» рядом с именем аккаунта
    if (settings.verified) {
      for (const sel of ACCOUNT_NAME_SELECTORS) {
        for (const name of dialog.querySelectorAll(sel)) {
          if (name.querySelector(":scope > .ytpv-badge")) continue;
          const badge = document.createElement("span");
          badge.className = "ytpv-badge";
          badge.title = "Подтверждённый канал (визуально)";
          name.appendChild(badge);
        }
      }
    }
  }

  /* ---------------- значок вкладки ---------------- */

  let faviconLink = null;
  let originalHref = null;

  function applyFavicon() {
    if (!document.head) return;
    const want = settings.enabled && settings.favicon;
    if (want) {
      let url = null;
      try {
        if (typeof chrome !== "undefined" && chrome.runtime && chrome.runtime.getURL) {
          url = chrome.runtime.getURL("icons/icon32.png");
        } else if (typeof browser !== "undefined" && browser.runtime && browser.runtime.getURL) {
          url = browser.runtime.getURL("icons/icon32.png");
        }
      } catch (e) { /* ignore */ }
      if (!url) return;
      if (!faviconLink) {
        faviconLink = document.querySelector('link[rel="icon"], link[rel="shortcut icon"]');
        if (faviconLink) {
          originalHref = faviconLink.getAttribute("href");
        } else {
          faviconLink = document.createElement("link");
          faviconLink.rel = "icon";
        }
      }
      if (!faviconLink.parentNode) document.head.appendChild(faviconLink);
      faviconLink.href = url;
    } else if (faviconLink) {
      if (originalHref != null) {
        faviconLink.href = originalHref;
      } else if (faviconLink.parentNode) {
        faviconLink.parentNode.removeChild(faviconLink);
      }
    }
  }

  /* ---------------- движок ---------------- */

  function decorate() {
    try { decorateLogo(); } catch (e) { /* ignore */ }
    try { decorateAccountMenu(); } catch (e) { /* ignore */ }
    try { applyFavicon(); } catch (e) { /* ignore */ }
  }

  const observer = new MutationObserver(() => {
    if (queued) return;
    queued = true;
    requestAnimationFrame(() => {
      queued = false;
      decorate();
    });
  });

  function boot() {
    applyClasses();
    applyFavicon();
    decorate();
    if (!started && document.documentElement) {
      started = true;
      observer.observe(document.documentElement, { childList: true, subtree: true });
    }
  }

  function init() {
    const cached = readCached();
    if (cached) settings = cached; // мгновенно, до первой отрисовки
    boot();
    loadSettings(); // затем актуальные значения из хранилища расширения
    watchSettings();

    if (!started) {
      // <html> ещё не создан — дождёмся его появления
      const early = new MutationObserver(() => {
        if (document.documentElement) {
          early.disconnect();
          boot();
        }
      });
      early.observe(document, { childList: true });
    }
  }

  init();
})();
