/* Ловит сбой при запуске. Вместо пустого экрана игрок видит, что случилось и что делать.
   Файл намеренно написан в старом стиле: он должен выполниться даже там, где остальной код не запустится. */
(function () {
  var shown = false;
  function show(msg) {
    if (shown || window.__kcReady) return; shown = true;
    var d = document.createElement('div'), h = document.createElement('b'), t = document.createElement('p'), s = document.createElement('small');
    d.id = 'fatal';
    h.textContent = 'Игра не запустилась';
    t.textContent = 'Обновите приложения «Android System WebView» и «Chrome» в магазине приложений и откройте игру снова.';
    s.textContent = msg ? String(msg).slice(0, 200) : '';
    d.appendChild(h); d.appendChild(t); d.appendChild(s);
    (document.body || document.documentElement).appendChild(d);
    var l = document.getElementById('loading'); if (l) l.style.display = 'none';
  }
  window.addEventListener('error', function (e) { if (e && e.message) show(e.message); });
  // если за 8 секунд игра так и не доложила о готовности — тоже говорим об этом
  window.addEventListener('load', function () { setTimeout(function () { if (!window.__kcReady) show('нет ответа от игры'); }, 8000); });
})();
