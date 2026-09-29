// Сценарий создания auth_key: клиентская половина протокола + оркестрация шагов.
// Функция runHandshake() — асинхронный генератор, который отдаёт интерфейсу
// по одному «шагу» за раз: описание, TL-схему, сырые байты сообщения и трассировку вычислений.

import { igeDecrypt, igeEncrypt } from './aes.js';
import { bigIntToBytes, bytesToBigInt, factorize, isProbablePrime, modPow } from './bigint.js';
import { concat, equal, hex, randomBytes, xor } from './bytes.js';
import { sha1 } from './hash.js';
import { CTOR, DH_PRIME, SCHEMA } from './protocol.js';
import { rsaPad } from './rsa.js';
import { DemoServer, deriveTmpAes } from './server.js';
import { newMessageId, plainMessage, TLReader, TLWriter } from './tl.js';

const DC_ID = 2;

function ms(t0) {
  return `${(performance.now() - t0).toFixed(1)} мс`;
}

export async function* runHandshake() {
  const server = new DemoServer();

  // ──────────────────────────────── Шаг 0 ────────────────────────────────
  let t0 = performance.now();
  const rsaKey = await server.init();
  yield {
    n: 0,
    id: 'setup',
    actor: 'server',
    kind: 'compute',
    title: 'Подготовка: открытый ключ RSA сервера',
    intro:
      'До начала рукопожатия у клиента уже есть список открытых ключей RSA серверов Telegram (они зашиты в приложение). ' +
      'В этой демонстрации «сервер» генерирует собственную пару RSA-2048 — так расшифровку RSA можно показать по-настоящему, ' +
      'а не только на словах.',
    trace: [
      { label: 'Модуль n (2048 бит)', value: hex(rsaKey.nBytes), long: true },
      { label: 'Экспонента e', value: `0x${hex(rsaKey.eBytes)} = ${rsaKey.e}` },
      { label: 'Отпечаток = 64 младших бита SHA1(rsa_public_key n:string e:string)', value: hex(rsaKey.fingerprint) },
      { label: 'Генерация ключа заняла', value: ms(t0) },
    ],
    reveal: {
      rsa_fingerprint: { value: hex(rsaKey.fingerprint), owner: 'both' },
    },
  };

  // ──────────────────────────────── Шаг 1 ────────────────────────────────
  const nonce = randomBytes(16);
  const reqPqBody = new TLWriter();
  reqPqBody.constructorId('req_pq_multi', CTOR.req_pq_multi);
  reqPqBody.raw('nonce', nonce, 'int128', 'случайное число клиента');
  const reqPqMsg = plainMessage(reqPqBody.result(), newMessageId(), reqPqBody.fields);

  yield {
    n: 1,
    id: 'req_pq_multi',
    actor: 'client',
    kind: 'send',
    title: 'client → server: req_pq_multi',
    intro:
      'Клиент начинает «временную сессию»: генерирует случайный nonce (128 бит) и отправляет его открытым текстом. ' +
      'Сообщение незашифрованное — auth_key_id = 0, потому что общего ключа ещё нет.',
    schema: SCHEMA.req_pq_multi,
    message: { direction: 'up', label: 'req_pq_multi', ...reqPqMsg },
    trace: [{ label: 'nonce', value: hex(nonce) }],
    reveal: { nonce: { value: hex(nonce), owner: 'both' } },
  };

  // ──────────────────────────────── Шаг 2 ────────────────────────────────
  const resPq = await server.handleReqPqMulti(reqPqMsg.bytes);
  const resPqReader = new TLReader(resPq.bytes.slice(20));
  resPqReader.constructorId();
  const nonceEcho = resPqReader.raw(16);
  const serverNonce = resPqReader.raw(16);
  const pqBytes = resPqReader.bytes();
  resPqReader.constructorId(); // vector
  const fpCount = resPqReader.int();
  const fingerprints = [];
  for (let i = 0; i < fpCount; i++) fingerprints.push(resPqReader.long());
  const pq = bytesToBigInt(pqBytes);

  yield {
    n: 2,
    id: 'resPQ',
    actor: 'server',
    kind: 'send',
    title: 'server → client: resPQ',
    intro:
      'Сервер возвращает исходный nonce (чтобы клиент узнал свою сессию), собственный server_nonce, ' +
      'число pq — произведение двух простых — и список отпечатков своих открытых ключей.',
    schema: SCHEMA.resPQ,
    message: { direction: 'down', label: 'resPQ', ...resPq },
    trace: resPq.trace,
    reveal: {
      server_nonce: { value: hex(serverNonce), owner: 'both' },
      pq: { value: `${pq} (0x${pq.toString(16).toUpperCase()})`, owner: 'both' },
    },
  };

  // ──────────────────────────────── Шаг 3 ────────────────────────────────
  t0 = performance.now();
  const { p, q, iterations } = factorize(pq);
  const factorTime = ms(t0);
  const nonceOk = equal(nonceEcho, nonce);
  const fpMatch = fingerprints.find((f) => equal(f, rsaKey.fingerprint));

  yield {
    n: 3,
    id: 'factorize',
    actor: 'client',
    kind: 'compute',
    title: 'Клиент: доказательство работы — раскладывает pq на множители',
    intro:
      'Это «proof of work»: клиент обязан разложить pq на два простых сомножителя p < q. ' +
      'Задача посильна для телефона, но делает массовую рассылку запросов дорогой. Здесь используется ро-алгоритм Полларда в модификации Брента.',
    trace: [
      { label: 'nonce в ответе совпадает с отправленным', value: nonceOk ? 'да ✓' : 'нет ✗' },
      { label: 'Найден нужный открытый ключ по отпечатку', value: fpMatch ? `${hex(fpMatch)} ✓` : 'не найден ✗' },
      { label: 'pq', value: `${pq}` },
      { label: 'p', value: `${p} (0x${p.toString(16).toUpperCase()})` },
      { label: 'q', value: `${q} (0x${q.toString(16).toUpperCase()})` },
      { label: 'Проверка p × q', value: `${p * q === pq ? 'совпало ✓' : 'ошибка ✗'}` },
      { label: 'Итераций алгоритма / время', value: `${iterations} / ${factorTime}` },
    ],
    reveal: {
      p: { value: `${p}`, owner: 'both' },
      q: { value: `${q}`, owner: 'both' },
    },
  };

  // ──────────────────────────────── Шаг 4 ────────────────────────────────
  const newNonce = randomBytes(32);
  const innerData = new TLWriter();
  innerData.constructorId('p_q_inner_data_dc', CTOR.p_q_inner_data_dc);
  innerData.bytes('pq', pqBytes);
  innerData.bytes('p', bigIntToBytes(p));
  innerData.bytes('q', bigIntToBytes(q));
  innerData.raw('nonce', nonce, 'int128');
  innerData.raw('server_nonce', serverNonce, 'int128');
  innerData.raw('new_nonce', newNonce, 'int256', 'главный секрет клиента на этом этапе');
  innerData.int('dc', DC_ID, 'номер дата-центра');
  const innerBytes = innerData.result();

  t0 = performance.now();
  const pad = await rsaPad(innerBytes, rsaKey);
  const rsaTime = ms(t0);

  const reqDhBody = new TLWriter();
  reqDhBody.constructorId('req_DH_params', CTOR.req_DH_params);
  reqDhBody.raw('nonce', nonce, 'int128');
  reqDhBody.raw('server_nonce', serverNonce, 'int128');
  reqDhBody.bytes('p', bigIntToBytes(p));
  reqDhBody.bytes('q', bigIntToBytes(q));
  reqDhBody.long('public_key_fingerprint', rsaKey.fingerprint, 'какой ключ использован');
  reqDhBody.bytes('encrypted_data', pad.encrypted, 'RSA_PAD(data, server_public_key), ровно 256 байт');
  const reqDhMsg = plainMessage(reqDhBody.result(), newMessageId(), reqDhBody.fields);

  yield {
    n: 4,
    id: 'req_DH_params',
    actor: 'client',
    kind: 'send',
    title: 'client → server: req_DH_params (RSA_PAD)',
    intro:
      'Клиент придумывает new_nonce (256 бит) — это главный секрет, который увидят только он и сервер. ' +
      'Структура p_q_inner_data_dc шифруется схемой RSA_PAD: набивка до 192 байт, разворот байтов, шифрование AES-IGE на одноразовом ключе, ' +
      'и только потом «сырое» RSA. Такая обёртка не даёт злоумышленнику играть с детерминированностью RSA.',
    schema: SCHEMA.p_q_inner_data_dc + '\n' + SCHEMA.req_DH_params,
    inner: { label: 'data = сериализованный p_q_inner_data_dc', bytes: innerBytes, fields: innerData.fields },
    message: { direction: 'up', label: 'req_DH_params', ...reqDhMsg },
    trace: [
      { label: 'new_nonce', value: hex(newNonce) },
      { label: 'data (TL-сериализация)', value: `${innerBytes.length} байт`, },
      { label: '1. data_with_padding = data + случайная набивка до 192 байт', value: hex(pad.dataWithPadding), long: true },
      { label: '2. data_pad_reversed = обратный порядок байтов', value: hex(pad.dataPadReversed), long: true },
      { label: '3. temp_key (32 случайных байта)', value: hex(pad.tempKey) },
      { label: '4. data_with_hash = data_pad_reversed + SHA256(temp_key + data_with_padding)', value: '224 байта' },
      { label: '5. aes_encrypted = AES256_IGE(data_with_hash, temp_key, 0)', value: hex(pad.aesEncrypted), long: true },
      { label: '6. temp_key_xor = temp_key XOR SHA256(aes_encrypted)', value: hex(pad.tempKeyXor) },
      { label: '7. key_aes_encrypted = temp_key_xor + aes_encrypted (256 байт, < модуля RSA)', value: `попыток подбора: ${pad.attempts}` },
      { label: '8. encrypted_data = key_aes_encrypted^e mod n', value: hex(pad.encrypted), long: true },
      { label: 'Время на RSA_PAD', value: rsaTime },
    ],
    reveal: {
      new_nonce: { value: hex(newNonce), owner: 'both', secret: true },
    },
  };

  // ──────────────────────────────── Шаг 5 ────────────────────────────────
  const serverDh = await server.handleReqDHParams(reqDhMsg.bytes);
  yield {
    n: 5,
    id: 'server_DH_params_ok',
    actor: 'server',
    kind: 'send',
    title: 'server → client: server_DH_params_ok',
    intro:
      'Сервер расшифровывает RSA своим приватным ключом, убеждается, что клиент честно разложил pq, ' +
      'и отвечает параметрами Диффи — Хеллмана. Ответ зашифрован AES-256-IGE на ключе, выведенном из new_nonce и server_nonce — ' +
      'а значит, прочитать его может только тот, кто знает new_nonce. Это и есть аутентификация сервера.',
    schema: SCHEMA.server_DH_inner_data + '\n' + SCHEMA.server_DH_params_ok,
    inner: { label: 'answer = сериализованный server_DH_inner_data (до шифрования)', ...serverDh.innerAnswer },
    message: { direction: 'down', label: 'server_DH_params_ok', ...serverDh },
    trace: serverDh.trace,
    reveal: {
      g: { value: '3', owner: 'both' },
      dh_prime: { value: hex(DH_PRIME), owner: 'both', long: true },
      a: { value: 'секрет сервера, 2048 бит', owner: 'server', secret: true },
      g_a: { value: hex(bigIntToBytes(serverDh.gA, 256)), owner: 'both', long: true },
    },
  };

  // ──────────────────────────────── Шаг 6 ────────────────────────────────
  const { tmpAesKey, tmpAesIv, h1, h2, h3 } = await deriveTmpAes(newNonce, serverNonce);
  const okReader = new TLReader(serverDh.bytes.slice(20));
  okReader.constructorId();
  okReader.raw(16);
  okReader.raw(16);
  const encryptedAnswer = okReader.bytes();
  const answerWithHash = igeDecrypt(encryptedAnswer, tmpAesKey, tmpAesIv);

  const answerReader = new TLReader(answerWithHash.slice(20));
  answerReader.constructorId();
  answerReader.raw(16);
  answerReader.raw(16);
  const g = answerReader.int();
  const dhPrimeBytes = answerReader.bytes();
  const gABytes = answerReader.bytes();
  const serverTime = answerReader.int();
  const answerLen = answerReader.offset;
  const answer = answerWithHash.slice(20, 20 + answerLen);
  const answerHashOk = equal(await sha1(answer), answerWithHash.slice(0, 20));

  const dhPrime = bytesToBigInt(dhPrimeBytes);
  const gA = bytesToBigInt(gABytes);
  t0 = performance.now();
  const primeOk = isProbablePrime(dhPrime, 15);
  const safeOk = isProbablePrime((dhPrime - 1n) / 2n, 15);
  const primeTime = ms(t0);
  const sizeOk = dhPrime > 1n << 2047n && dhPrime < 1n << 2048n;
  const gOk = g === 3 ? dhPrime % 3n === 2n : g === 2 ? dhPrime % 8n === 7n : true;
  const limit = 1n << 1984n;
  const gARangeOk = gA > 1n && gA < dhPrime - 1n && gA >= limit && gA <= dhPrime - limit;

  yield {
    n: 6,
    id: 'check_dh',
    actor: 'client',
    kind: 'compute',
    title: 'Клиент: расшифровывает ответ и проверяет параметры DH',
    intro:
      'Ключ и IV для AES выводятся из new_nonce и server_nonce тремя вызовами SHA1. ' +
      'После расшифровки клиент обязан убедиться, что dh_prime действительно безопасное простое, а g_a не вырожден: ' +
      'иначе сервер (или тот, кто его подменил) мог бы подсунуть слабую группу и восстановить ключ.',
    trace: [
      { label: 'SHA1(new_nonce + server_nonce)', value: hex(h1) },
      { label: 'SHA1(server_nonce + new_nonce)', value: hex(h2) },
      { label: 'SHA1(new_nonce + new_nonce)', value: hex(h3) },
      { label: 'tmp_aes_key', value: hex(tmpAesKey) },
      { label: 'tmp_aes_iv', value: hex(tmpAesIv) },
      { label: 'SHA1(answer) совпал с префиксом', value: answerHashOk ? 'да ✓' : 'нет ✗' },
      { label: 'g', value: `${g}` },
      { label: 'server_time', value: `${serverTime} (${new Date(serverTime * 1000).toISOString().replace('T', ' ').slice(0, 19)} UTC)` },
      { label: '2^2047 < dh_prime < 2^2048', value: sizeOk ? 'да ✓' : 'нет ✗' },
      { label: 'dh_prime простое (Миллер — Рабин, 15 раундов)', value: primeOk ? 'да ✓' : 'нет ✗' },
      { label: '(dh_prime − 1)/2 простое → безопасное простое', value: safeOk ? 'да ✓' : 'нет ✗' },
      { label: `условие на генератор (для g=${g} требуется dh_prime mod 3 = 2)`, value: gOk ? 'выполнено ✓' : 'нарушено ✗' },
      { label: '2^1984 ≤ g_a ≤ dh_prime − 2^1984', value: gARangeOk ? 'выполнено ✓' : 'нарушено ✗' },
      { label: 'Время проверки простоты', value: primeTime },
    ],
    reveal: {},
  };

  // ──────────────────────────────── Шаг 7 ────────────────────────────────
  const b = bytesToBigInt(randomBytes(256)) % (dhPrime - 2n);
  t0 = performance.now();
  const gB = modPow(BigInt(g), b, dhPrime);
  const gbTime = ms(t0);

  const clientInner = new TLWriter();
  clientInner.constructorId('client_DH_inner_data', CTOR.client_DH_inner_data);
  clientInner.raw('nonce', nonce, 'int128');
  clientInner.raw('server_nonce', serverNonce, 'int128');
  clientInner.long('retry_id', 0n, '0 при первой попытке');
  clientInner.bytes('g_b', bigIntToBytes(gB, 256), 'открытая часть клиента');
  const clientInnerBytes = clientInner.result();

  const clientHash = await sha1(clientInnerBytes);
  const cPad = (16 - ((clientHash.length + clientInnerBytes.length) % 16)) % 16;
  const clientDataWithHash = concat(clientHash, clientInnerBytes, randomBytes(cPad));
  const clientEncrypted = igeEncrypt(clientDataWithHash, tmpAesKey, tmpAesIv);

  const setDhBody = new TLWriter();
  setDhBody.constructorId('set_client_DH_params', CTOR.set_client_DH_params);
  setDhBody.raw('nonce', nonce, 'int128');
  setDhBody.raw('server_nonce', serverNonce, 'int128');
  setDhBody.bytes('encrypted_data', clientEncrypted, 'AES-256-IGE(SHA1(data) + data + padding)');
  const setDhMsg = plainMessage(setDhBody.result(), newMessageId(), setDhBody.fields);

  yield {
    n: 7,
    id: 'set_client_DH_params',
    actor: 'client',
    kind: 'send',
    title: 'client → server: set_client_DH_params',
    intro:
      'Клиент выбирает собственный 2048-битный секрет b и отправляет g_b = g^b mod dh_prime, ' +
      'зашифровав структуру тем же временным ключом AES. Само b не покидает устройство.',
    schema: SCHEMA.client_DH_inner_data + '\n' + SCHEMA.set_client_DH_params,
    inner: { label: 'data = сериализованный client_DH_inner_data (до шифрования)', bytes: clientInnerBytes, fields: clientInner.fields },
    message: { direction: 'up', label: 'set_client_DH_params', ...setDhMsg },
    trace: [
      { label: 'b (секрет клиента, наружу не уходит)', value: hex(bigIntToBytes(b, 256)), long: true },
      { label: 'g_b = g^b mod dh_prime', value: hex(bigIntToBytes(gB, 256)), long: true },
      { label: 'Время возведения в степень', value: gbTime },
      { label: 'data_with_hash = SHA1(data) + data + набивка', value: `${clientDataWithHash.length} байт` },
    ],
    reveal: {
      b: { value: 'секрет клиента, 2048 бит', owner: 'client', secret: true },
      g_b: { value: hex(bigIntToBytes(gB, 256)), owner: 'both', long: true },
    },
  };

  // ──────────────────────────────── Шаг 8 ────────────────────────────────
  t0 = performance.now();
  const clientAuthKeyNum = modPow(gA, b, dhPrime);
  const clientAuthKey = bigIntToBytes(clientAuthKeyNum, 256);
  const clientKeyTime = ms(t0);
  const clientSha1 = await sha1(clientAuthKey);
  const clientAuthKeyId = clientSha1.slice(12, 20);
  const clientAuxHash = clientSha1.slice(0, 8);

  yield {
    n: 8,
    id: 'client_auth_key',
    actor: 'client',
    kind: 'compute',
    title: 'Клиент: вычисляет auth_key',
    intro:
      'Теперь у клиента есть всё: auth_key = (g_a)^b mod dh_prime = g^(ab) mod dh_prime. ' +
      'Это 2048-битное число никогда не передаётся по сети — обе стороны получают его независимо.',
    trace: [
      { label: 'auth_key = g_a^b mod dh_prime', value: hex(clientAuthKey), long: true, highlight: true },
      { label: 'SHA1(auth_key)', value: hex(clientSha1) },
      { label: 'auth_key_id = 64 младших бита', value: hex(clientAuthKeyId) },
      { label: 'auth_key_aux_hash = 64 старших бита', value: hex(clientAuxHash) },
      { label: 'Время возведения в степень', value: clientKeyTime },
    ],
    reveal: {
      auth_key_client: { value: hex(clientAuthKey), owner: 'client', long: true },
      auth_key_id: { value: hex(clientAuthKeyId), owner: 'client' },
    },
  };

  // ──────────────────────────────── Шаг 9 ────────────────────────────────
  const dhGen = await server.handleSetClientDHParams(setDhMsg.bytes);
  yield {
    n: 9,
    id: 'dh_gen_ok',
    actor: 'server',
    kind: 'send',
    title: 'server → client: dh_gen_ok',
    intro:
      'Сервер независимо считает auth_key = (g_b)^a mod dh_prime, проверяет, что такого ключа ещё нет, ' +
      'и подтверждает успех, прислав new_nonce_hash1 — доказательство того, что у него получился тот же ключ.',
    schema: SCHEMA.dh_gen_ok,
    message: { direction: 'down', label: 'dh_gen_ok', ...dhGen },
    trace: dhGen.trace,
    reveal: {
      auth_key_server: { value: hex(dhGen.authKey), owner: 'server', long: true },
    },
  };

  // ──────────────────────────────── Шаг 10 ────────────────────────────────
  const genReader = new TLReader(dhGen.bytes.slice(20));
  genReader.constructorId();
  genReader.raw(16);
  genReader.raw(16);
  const newNonceHash1 = genReader.raw(16);
  const expectedHash1 = (await sha1(newNonce, new Uint8Array([1]), clientAuxHash)).slice(4, 20);
  const hashOk = equal(newNonceHash1, expectedHash1);
  const serverSalt = xor(newNonce.slice(0, 8), serverNonce.slice(0, 8));
  const keysMatch = equal(clientAuthKey, dhGen.authKey);

  yield {
    n: 10,
    id: 'done',
    actor: 'client',
    kind: 'compute',
    title: 'Готово: общий auth_key согласован',
    intro:
      'Клиент пересчитывает new_nonce_hash1 из своего auth_key и сравнивает с ответом сервера. ' +
      'Совпало — значит, ключи одинаковые. Временные данные (p, q, a, b, new_nonce) забываются, ' +
      'остаются auth_key, auth_key_id и первая server_salt.',
    trace: [
      { label: 'new_nonce_hash1 от сервера', value: hex(newNonceHash1) },
      { label: 'Ожидаемое значение у клиента', value: hex(expectedHash1) },
      { label: 'Проверка', value: hashOk ? 'совпало ✓' : 'не совпало ✗' },
      { label: 'server_salt = substr(new_nonce,0,8) XOR substr(server_nonce,0,8)', value: hex(serverSalt) },
    ],
    result: {
      keysMatch,
      authKey: hex(clientAuthKey),
      authKeyId: hex(clientAuthKeyId),
      serverSalt: hex(serverSalt),
      serverAuthKey: hex(dhGen.authKey),
    },
    reveal: {
      server_salt: { value: hex(serverSalt), owner: 'both' },
    },
  };
}
