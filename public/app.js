const STORAGE_KEY = 'deepseek-v3-studio-state';
const SETTINGS_KEY = 'deepseek-v3-studio-settings';

const $ = (selector) => document.querySelector(selector);
const welcomeView = $('#welcomeView');
const messagesView = $('#messagesView');
const messageInput = $('#messageInput');
const sendButton = $('#sendButton');
const toast = $('#toast');
const settingsModal = $('#settingsModal');
const sidebar = $('#sidebar');

let isLoading = false;
let toastTimer;
let config = { configured: false, model: 'deepseek-chat', mode: 'demo' };
let settings = loadSettings();
let store = loadStore();

function uid() {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 7)}`;
}

function loadSettings() {
  try {
    return { model: 'deepseek-chat', temperature: 0.7, max_tokens: 2048, ...JSON.parse(localStorage.getItem(SETTINGS_KEY) || '{}') };
  } catch {
    return { model: 'deepseek-chat', temperature: 0.7, max_tokens: 2048 };
  }
}

function loadStore() {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
    if (Array.isArray(saved.conversations) && saved.conversations.length) {
      const activeId = saved.activeId || saved.conversations[0].id;
      if (saved.conversations.some((conversation) => conversation.id === activeId)) return { conversations: saved.conversations, activeId };
    }
  } catch {
    // Start with a clean local session when storage is unavailable or malformed.
  }
  const initial = { id: uid(), title: 'Новый диалог', messages: [] };
  return { conversations: [initial], activeId: initial.id };
}

function persist() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(store));
  localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings));
}

function activeConversation() {
  return store.conversations.find((conversation) => conversation.id === store.activeId) || store.conversations[0];
}

function activeMessages() {
  return activeConversation().messages;
}

function showToast(message) {
  toast.textContent = message;
  toast.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toast.classList.remove('show'), 3000);
}

function createMessageElement(message, index) {
  const row = document.createElement('article');
  row.className = `message ${message.role}`;
  const avatar = document.createElement('div');
  avatar.className = 'message-avatar';
  avatar.textContent = message.role === 'user' ? 'Вы' : '✦';
  const body = document.createElement('div');
  body.className = 'message-body';
  const meta = document.createElement('div');
  meta.className = 'message-meta';
  const name = document.createElement('strong');
  name.textContent = message.role === 'user' ? 'Вы' : 'DeepSeek V3';
  const time = document.createElement('span');
  time.textContent = message.role === 'user' ? 'только что' : (message.demo ? 'демо-ответ' : 'ответ модели');
  meta.append(name, time);
  const content = document.createElement('div');
  content.className = 'message-content';
  content.textContent = message.content;
  body.append(meta, content);
  if (message.role === 'assistant') {
    const actions = document.createElement('div');
    actions.className = 'message-actions';
    const copy = document.createElement('button');
    copy.className = 'message-action';
    copy.type = 'button';
    copy.textContent = 'Скопировать';
    copy.addEventListener('click', () => copyText(message.content));
    const useful = document.createElement('button');
    useful.className = 'message-action';
    useful.type = 'button';
    useful.textContent = 'Полезно';
    useful.addEventListener('click', () => showToast('Спасибо за обратную связь'));
    actions.append(copy, useful);
    body.append(actions);
  }
  row.append(avatar, body);
  return row;
}

function render() {
  const messages = activeMessages();
  const hasMessages = messages.length > 0;
  welcomeView.classList.toggle('hidden', hasMessages);
  messagesView.classList.toggle('visible', hasMessages);
  messagesView.replaceChildren();
  messages.forEach((message, index) => messagesView.appendChild(createMessageElement(message, index)));
  renderHistory();
  if (hasMessages) requestAnimationFrame(() => messagesView.lastElementChild?.scrollIntoView({ behavior: 'smooth', block: 'end' }));
}

function renderHistory() {
  const list = $('#historyList');
  list.replaceChildren();
  const visible = [...store.conversations].reverse().filter((conversation) => conversation.messages.length || conversation.id === store.activeId).slice(0, 8);
  if (!visible.length) {
    const empty = document.createElement('div');
    empty.className = 'history-empty';
    empty.textContent = 'Здесь появятся ваши диалоги';
    list.appendChild(empty);
    return;
  }
  visible.forEach((conversation) => {
    const button = document.createElement('button');
    button.className = `history-item${conversation.id === store.activeId ? ' active' : ''}`;
    button.type = 'button';
    button.textContent = conversation.title;
    button.title = conversation.title;
    button.addEventListener('click', () => {
      store.activeId = conversation.id;
      persist();
      render();
      closeSidebar();
    });
    list.appendChild(button);
  });
}

function setConnectionState() {
  const dot = $('#connectionDot');
  const label = $('#connectionLabel');
  const copy = $('#connectionCopy');
  const action = $('#connectionAction');
  dot.classList.toggle('connected', config.configured);
  label.textContent = config.configured ? 'API подключён' : 'Демо-режим';
  copy.textContent = config.configured ? `Подключено · ${config.model}` : 'Подключите API-ключ, чтобы общаться с моделью.';
  action.textContent = config.configured ? 'Изменить настройки →' : 'Открыть настройки →';
  const settingsStatus = $('#settingsStatus');
  settingsStatus.className = `settings-status${config.configured ? ' is-connected' : ''}`;
  settingsStatus.innerHTML = config.configured
    ? '<span class="status-dot connected"></span><div><strong>API подключён</strong><p>Запросы отправляются через защищённый серверный прокси.</p></div>'
    : '<span class="status-dot"></span><div><strong>Демо-режим активен</strong><p>Интерфейс работает локально. API-ключ можно добавить на сервере.</p></div>';
}

function openSettings() {
  syncSettingsForm();
  settingsModal.classList.add('open');
  settingsModal.setAttribute('aria-hidden', 'false');
  $('#closeSettingsButton').focus();
}

function closeSettings() {
  settingsModal.classList.remove('open');
  settingsModal.setAttribute('aria-hidden', 'true');
}

function syncSettingsForm() {
  $('#modelSelect').value = settings.model;
  $('#temperatureRange').value = settings.temperature;
  $('#temperatureValue').value = Number(settings.temperature).toFixed(1);
  $('#temperatureValue').textContent = Number(settings.temperature).toFixed(1);
  $('#tokensInput').value = settings.max_tokens;
}

function saveSettings() {
  settings = {
    model: $('#modelSelect').value,
    temperature: Number($('#temperatureRange').value),
    max_tokens: Math.min(8192, Math.max(64, Number($('#tokensInput').value) || 2048)),
  };
  persist();
  closeSettings();
  showToast('Настройки сохранены');
}

function resetSettings() {
  settings = { model: 'deepseek-chat', temperature: 0.7, max_tokens: 2048 };
  syncSettingsForm();
  persist();
  showToast('Настройки сброшены');
}

function autoGrow() {
  messageInput.style.height = 'auto';
  messageInput.style.height = `${Math.min(messageInput.scrollHeight, 150)}px`;
}

function toggleLoading(loading) {
  isLoading = loading;
  sendButton.disabled = loading;
  messageInput.disabled = loading;
  if (loading) {
    const typing = document.createElement('article');
    typing.className = 'message assistant typing-message';
    typing.innerHTML = '<div class="message-avatar">✦</div><div class="message-body"><div class="message-meta"><strong>DeepSeek V3</strong><span>печатает</span></div><div class="typing-dots"><i></i><i></i><i></i></div></div>';
    messagesView.appendChild(typing);
    typing.scrollIntoView({ behavior: 'smooth', block: 'end' });
  } else {
    document.querySelector('.typing-message')?.remove();
  }
}

async function sendMessage(prefilled) {
  if (isLoading) return;
  const content = (prefilled ?? messageInput.value).trim();
  if (!content) return;
  const conversation = activeConversation();
  conversation.messages.push({ role: 'user', content });
  if (conversation.title === 'Новый диалог') conversation.title = content.length > 38 ? `${content.slice(0, 38)}…` : content;
  messageInput.value = '';
  autoGrow();
  persist();
  render();
  toggleLoading(true);
  try {
    const response = await fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ messages: conversation.messages, model: settings.model, temperature: settings.temperature, max_tokens: settings.max_tokens }),
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(data.error || 'Не удалось получить ответ');
    conversation.messages.push({ role: 'assistant', content: data.answer, demo: data.demo });
    persist();
    if (data.demo && config.configured) showToast('Сервер вернул демо-ответ');
  } catch (error) {
    showToast(error.message || 'Что-то пошло не так');
  } finally {
    toggleLoading(false);
    render();
    messageInput.focus();
  }
}

function copyText(text) {
  navigator.clipboard?.writeText(text).then(() => showToast('Скопировано в буфер обмена')).catch(() => showToast('Не удалось скопировать'));
}

function closeSidebar() { sidebar.classList.remove('open'); }

async function initConfig() {
  try {
    const response = await fetch('/api/config');
    if (response.ok) config = await response.json();
  } catch {
    // The static UI remains useful even if the local API is unavailable.
  }
  setConnectionState();
}

$('#newChatButton').addEventListener('click', () => {
  if (!activeMessages().length) { closeSidebar(); messageInput.focus(); return; }
  const fresh = { id: uid(), title: 'Новый диалог', messages: [] };
  store.conversations.push(fresh);
  store.activeId = fresh.id;
  persist();
  render();
  closeSidebar();
  messageInput.focus();
});
$('#clearHistoryButton').addEventListener('click', () => {
  const active = activeConversation();
  store.conversations = [{ id: uid(), title: 'Новый диалог', messages: [] }];
  store.activeId = store.conversations[0].id;
  persist();
  render();
  showToast(active.messages.length ? 'История очищена' : 'История уже пуста');
});
$('#settingsButton').addEventListener('click', openSettings);
$('#topSettingsButton').addEventListener('click', openSettings);
$('#connectionAction').addEventListener('click', openSettings);
$('#closeSettingsButton').addEventListener('click', closeSettings);
$('#saveSettingsButton').addEventListener('click', saveSettings);
$('#resetSettingsButton').addEventListener('click', resetSettings);
$('#settingsModal').addEventListener('click', (event) => { if (event.target === settingsModal) closeSettings(); });
$('#temperatureRange').addEventListener('input', (event) => { $('#temperatureValue').textContent = Number(event.target.value).toFixed(1); });
$('#sendButton').addEventListener('click', () => sendMessage());
messageInput.addEventListener('input', autoGrow);
messageInput.addEventListener('keydown', (event) => {
  if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); sendMessage(); }
});
$('#suggestions').addEventListener('click', (event) => {
  const card = event.target.closest('[data-prompt]');
  if (card) sendMessage(card.dataset.prompt);
});
$('#attachButton').addEventListener('click', () => $('#fileInput').click());
$('#fileInput').addEventListener('change', (event) => {
  const files = [...event.target.files];
  if (files.length) showToast(`${files.length} ${files.length === 1 ? 'файл выбран' : 'файла выбрано'} · загрузка в следующей версии`);
  event.target.value = '';
});
$('#libraryButton').addEventListener('click', () => showToast('Библиотека скоро будет доступна'));
$('#shareButton').addEventListener('click', () => copyText(window.location.href));
$('#mobileMenuButton').addEventListener('click', () => sidebar.classList.toggle('open'));
$('#chatModeButton').addEventListener('click', () => { $('#chatModeButton').classList.add('selected'); $('#thinkModeButton').classList.remove('selected'); });
$('#thinkModeButton').addEventListener('click', () => { $('#thinkModeButton').classList.add('selected'); $('#chatModeButton').classList.remove('selected'); showToast('DeepThink Beta включён для следующего запроса'); });
document.addEventListener('keydown', (event) => {
  if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') { event.preventDefault(); $('#newChatButton').click(); }
  if (event.key === 'Escape') { closeSettings(); closeSidebar(); }
});

syncSettingsForm();
render();
initConfig();
messageInput.focus();
