/* Панель настроек Premium Look */
(() => {
  "use strict";

  const DEFAULTS = {
    enabled: true,
    logo: true,
    avatar: true,
    account: true,
    verified: true,
    favicon: true,
  };

  const OPTIONS = [
    {
      key: "logo",
      title: "Логотип Premium",
      desc: "«YouTube Premium» вместо обычного логотипа в шапке",
    },
    {
      key: "avatar",
      title: "Золотой аватар",
      desc: "Золотая рамка и значок Premium на аватарке",
    },
    {
      key: "account",
      title: "Карточка в меню аккаунта",
      desc: "Блок «YouTube Premium — подписка активна»",
    },
    {
      key: "verified",
      title: "Галочка «верифицирован»",
      desc: "Как у MrBeast: рядом с каналами под видео и в комментариях",
    },
    {
      key: "favicon",
      title: "Значок вкладки",
      desc: "Красная иконка Premium на вкладке браузера",
    },
  ];

  const api =
    (typeof chrome !== "undefined" && chrome.storage && chrome.storage.local)
      ? chrome.storage
      : browser.storage;

  let settings = { ...DEFAULTS };

  const masterEl = document.getElementById("master");
  const optionsEl = document.getElementById("options");
  const statusEl = document.getElementById("status");
  const versionEl = document.getElementById("version");

  const toggles = {};

  function build() {
    for (const opt of OPTIONS) {
      const row = document.createElement("label");
      row.className = "option";

      const text = document.createElement("span");
      text.className = "option__text";

      const title = document.createElement("span");
      title.className = "option__title";
      title.textContent = opt.title;

      const desc = document.createElement("span");
      desc.className = "option__desc";
      desc.textContent = opt.desc;

      text.append(title, desc);

      const sw = document.createElement("span");
      sw.className = "switch";

      const input = document.createElement("input");
      input.type = "checkbox";
      input.addEventListener("change", () => {
        settings[opt.key] = input.checked;
        save();
        render();
      });

      const track = document.createElement("span");
      track.className = "switch__track";

      sw.append(input, track);
      row.append(text, sw);
      optionsEl.append(row);

      toggles[opt.key] = input;
    }
  }

  function save() {
    api.local.set(settings);
  }

  function render() {
    masterEl.checked = !!settings.enabled;
    optionsEl.classList.toggle("is-off", !settings.enabled);
    for (const opt of OPTIONS) {
      toggles[opt.key].checked = !!settings[opt.key];
    }
    statusEl.textContent = settings.enabled
      ? "Включено — изменения на YouTube применяются сразу"
      : "Выключено — YouTube выглядит как обычно";
  }

  masterEl.addEventListener("change", () => {
    settings.enabled = masterEl.checked;
    save();
    render();
  });

  try {
    versionEl.textContent = "v" + chrome.runtime.getManifest().version;
  } catch (e) {
    versionEl.textContent = "";
  }

  build();

  api.local.get(DEFAULTS, (res) => {
    settings = { ...DEFAULTS, ...(res || {}) };
    render();
  });

  render();
})();
