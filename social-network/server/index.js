'use strict';

const http = require('node:http');
const path = require('node:path');
const { Store } = require('./store');
const { createApp } = require('./app');
const { seedBots, startGrowth } = require('./growth');

const PORT = Number(process.env.PORT) || 3000;
const HOST = process.env.HOST || '0.0.0.0';
const DATA_FILE = process.env.DATA_FILE || path.join(__dirname, '..', 'data', 'db.json');
// Как часто бот-симуляция делает шаг (мс). Меньше значение — быстрее виден рост.
const GROWTH_INTERVAL_MS = Number(process.env.GROWTH_INTERVAL_MS) || 10000;

const store = new Store(DATA_FILE);
const seeded = seedBots(store);
if (seeded) console.log(`Создано виртуальных аккаунтов: ${seeded}`);
store.flush();

const app = createApp({ store, publicDir: path.join(__dirname, '..', 'public') });
const server = http.createServer(app);
startGrowth(store, { intervalMs: GROWTH_INTERVAL_MS });

server.listen(PORT, HOST, () => {
  console.log(`Соцсеть запущена: http://localhost:${PORT}`);
});

function shutdown() {
  store.flush();
  server.close(() => process.exit(0));
  setTimeout(() => process.exit(0), 1000).unref();
}
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
