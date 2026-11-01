"use strict";

// Клик по иконке расширения открывает терминал в новой вкладке.
// ВАЖНО: здесь намеренно нет externally_connectable и content_scripts —
// поэтому ни один сайт не может отправлять сообщения этому расширению.
chrome.action.onClicked.addListener(() => {
  chrome.tabs.create({ url: chrome.runtime.getURL("terminal.html") });
});
