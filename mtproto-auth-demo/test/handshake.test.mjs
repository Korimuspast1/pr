// Сквозной прогон рукопожатия без браузера: клиент и модель сервера должны
// получить одинаковый auth_key.
import { runHandshake } from '../src/handshake.js';

let last = null;
const t0 = performance.now();
for await (const step of runHandshake()) {
  const flags = (step.trace || [])
    .map((t) => t.value)
    .join(' ');
  if (flags.includes('✗')) {
    console.error(`FAIL на шаге ${step.n} (${step.id}):`);
    console.error(step.trace);
    process.exit(1);
  }
  console.log(`шаг ${String(step.n).padStart(2)} — ${step.title}` + (step.message ? ` [${step.message.bytes.length} байт]` : ''));
  last = step;
}
const elapsed = (performance.now() - t0).toFixed(0);

if (!last?.result?.keysMatch) {
  console.error('FAIL: auth_key клиента и сервера различаются');
  console.error(last?.result);
  process.exit(1);
}
console.log('\nauth_key    =', last.result.authKey.slice(0, 64) + '…');
console.log('auth_key_id =', last.result.authKeyId);
console.log('server_salt =', last.result.serverSalt);
console.log(`\nВсе шаги пройдены за ${elapsed} мс, ключи совпадают ✓`);
