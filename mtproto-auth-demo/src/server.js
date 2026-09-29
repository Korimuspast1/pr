// Модель сервера Telegram: полностью выполняет свою половину протокола
// создания auth_key. Общение с клиентом идёт только «по проводу» — сырыми
// байтами MTProto-сообщений.

import { igeDecrypt, igeEncrypt } from './aes.js';
import { bigIntToBytes, bytesToBigInt, isProbablePrime, modPow } from './bigint.js';
import { concat, equal, hex, randomBytes } from './bytes.js';
import { sha1 } from './hash.js';
import { CTOR, DH_G, DH_PRIME } from './protocol.js';
import { generateServerRsaKey, rsaUnpad } from './rsa.js';
import { newMessageId, plainMessage, TLReader, TLWriter } from './tl.js';

function randomPrime(bits) {
  for (;;) {
    const bytes = randomBytes(Math.ceil(bits / 8));
    let n = bytesToBigInt(bytes);
    n |= 1n << BigInt(bits - 1);
    n |= 1n;
    if (isProbablePrime(n, 12)) return n;
  }
}

export class DemoServer {
  constructor() {
    this.state = {};
    this.trace = [];
  }

  async init() {
    this.rsaKey = await generateServerRsaKey();
    return this.rsaKey;
  }

  /** Шаг 2: resPQ */
  async handleReqPqMulti(request) {
    const trace = [];
    const reader = new TLReader(request.slice(20));
    const ctor = reader.constructorId();
    if (ctor !== CTOR.req_pq_multi) throw new Error('Ожидался req_pq_multi');
    const nonce = reader.raw(16);
    trace.push({ label: 'Принят nonce клиента', value: hex(nonce) });

    const p = randomPrime(31);
    let q = randomPrime(31);
    while (q === p) q = randomPrime(31);
    const [lo, hi] = p < q ? [p, q] : [q, p];
    const pq = lo * hi;
    const serverNonce = randomBytes(16);

    this.state = { ...this.state, nonce, serverNonce, p: lo, q: hi, pq };
    trace.push({ label: 'Выбраны два простых числа', value: `${lo} × ${hi}` });
    trace.push({ label: 'pq (произведение, отправляется клиенту)', value: `${pq} = 0x${pq.toString(16).toUpperCase()}` });
    trace.push({ label: 'server_nonce (случайное число сервера)', value: hex(serverNonce) });

    const body = new TLWriter();
    body.constructorId('resPQ', CTOR.resPQ);
    body.raw('nonce', nonce, 'int128', 'значение, сгенерированное клиентом на шаге 1');
    body.raw('server_nonce', serverNonce, 'int128', 'случайное число сервера');
    body.bytes('pq', bigIntToBytes(pq), `${pq} в big endian, префикс длины и выравнивание`);
    body.vectorLong('server_public_key_fingerprints', [this.rsaKey.fingerprint], '64 младших бита SHA1(server_public_key)');

    const message = plainMessage(body.result(), newMessageId(), body.fields);
    return { ...message, trace };
  }

  /** Шаг 5: расшифровка RSA, ответ server_DH_params_ok */
  async handleReqDHParams(request) {
    const trace = [];
    const reader = new TLReader(request.slice(20));
    if (reader.constructorId() !== CTOR.req_DH_params) throw new Error('Ожидался req_DH_params');
    const nonce = reader.raw(16);
    const serverNonce = reader.raw(16);
    const p = reader.bytes();
    const q = reader.bytes();
    const fingerprint = reader.long();
    const encryptedData = reader.bytes();

    if (!equal(nonce, this.state.nonce) || !equal(serverNonce, this.state.serverNonce)) {
      throw new Error('nonce/server_nonce не совпадают — чужая сессия');
    }
    if (!equal(fingerprint, this.rsaKey.fingerprint)) throw new Error('Неизвестный отпечаток ключа');
    trace.push({ label: 'Отпечаток ключа совпал', value: hex(fingerprint) });

    const pNum = bytesToBigInt(p);
    const qNum = bytesToBigInt(q);
    const factorsOk = pNum * qNum === this.state.pq && pNum < qNum;
    trace.push({
      label: 'Проверка доказательства работы (p × q = pq)',
      value: `${pNum} × ${qNum} = ${pNum * qNum} ${factorsOk ? '✓' : '✗'}`,
    });
    if (!factorsOk) throw new Error('Клиент неверно разложил pq');

    const { dataWithPadding } = await rsaUnpad(encryptedData, this.rsaKey);
    trace.push({ label: 'RSA расшифрован приватным ключом', value: 'SHA256-контроль RSA_PAD сошёлся ✓' });

    const inner = new TLReader(dataWithPadding);
    if (inner.constructorId() !== CTOR.p_q_inner_data_dc) throw new Error('Ожидался p_q_inner_data_dc');
    inner.bytes(); // pq
    inner.bytes(); // p
    inner.bytes(); // q
    inner.raw(16); // nonce
    inner.raw(16); // server_nonce
    const newNonce = inner.raw(32);
    const dc = inner.int();
    trace.push({ label: 'Извлечён new_nonce (теперь секрет двоих)', value: hex(newNonce) });
    trace.push({ label: 'Запрошенный дата-центр', value: `DC ${dc}` });

    // Диффи — Хеллман: секрет a и g_a
    const dhPrime = bytesToBigInt(DH_PRIME);
    const a = bytesToBigInt(randomBytes(256)) % (dhPrime - 2n);
    const gA = modPow(BigInt(DH_G), a, dhPrime);
    const serverTime = Math.floor(Date.now() / 1000);
    this.state = { ...this.state, newNonce, a, gA, dhPrime, serverTime };
    trace.push({ label: 'Сгенерирован секрет a (2048 бит, наружу не уходит)', value: hex(bigIntToBytes(a, 256)) });
    trace.push({ label: 'g_a = g^a mod dh_prime', value: hex(bigIntToBytes(gA, 256)) });

    const answer = new TLWriter();
    answer.constructorId('server_DH_inner_data', CTOR.server_DH_inner_data);
    answer.raw('nonce', nonce, 'int128');
    answer.raw('server_nonce', serverNonce, 'int128');
    answer.int('g', DH_G, 'генератор группы');
    answer.bytes('dh_prime', DH_PRIME, '2048-битное безопасное простое');
    answer.bytes('g_a', bigIntToBytes(gA, 256), 'открытая часть сервера');
    answer.int('server_time', serverTime, 'время сервера, unixtime');
    const answerBytes = answer.result();

    const { tmpAesKey, tmpAesIv } = await deriveTmpAes(newNonce, serverNonce);
    const hashPrefix = await sha1(answerBytes);
    const padLen = (16 - ((hashPrefix.length + answerBytes.length) % 16)) % 16;
    const answerWithHash = concat(hashPrefix, answerBytes, randomBytes(padLen));
    const encryptedAnswer = igeEncrypt(answerWithHash, tmpAesKey, tmpAesIv);

    trace.push({ label: 'tmp_aes_key = SHA1(new_nonce+server_nonce) + substr(SHA1(server_nonce+new_nonce),0,12)', value: hex(tmpAesKey) });
    trace.push({ label: 'tmp_aes_iv', value: hex(tmpAesIv) });
    trace.push({ label: 'answer_with_hash = SHA1(answer) + answer + padding', value: `${answerWithHash.length} байт` });

    const body = new TLWriter();
    body.constructorId('server_DH_params_ok', CTOR.server_DH_params_ok);
    body.raw('nonce', nonce, 'int128');
    body.raw('server_nonce', serverNonce, 'int128');
    body.bytes('encrypted_answer', encryptedAnswer, 'AES-256-IGE(answer_with_hash, tmp_aes_key, tmp_aes_iv)');

    const message = plainMessage(body.result(), newMessageId(), body.fields);
    return { ...message, trace, innerAnswer: { bytes: answerBytes, fields: answer.fields }, tmpAesKey, tmpAesIv, gA, dhPrime, serverTime };
  }

  /** Шаг 9: получение g_b, вычисление auth_key, ответ dh_gen_ok */
  async handleSetClientDHParams(request) {
    const trace = [];
    const reader = new TLReader(request.slice(20));
    if (reader.constructorId() !== CTOR.set_client_DH_params) throw new Error('Ожидался set_client_DH_params');
    const nonce = reader.raw(16);
    const serverNonce = reader.raw(16);
    const encryptedData = reader.bytes();

    const { tmpAesKey, tmpAesIv } = await deriveTmpAes(this.state.newNonce, serverNonce);
    const dataWithHash = igeDecrypt(encryptedData, tmpAesKey, tmpAesIv);
    const inner = new TLReader(dataWithHash.slice(20));
    if (inner.constructorId() !== CTOR.client_DH_inner_data) throw new Error('Ожидался client_DH_inner_data');
    inner.raw(16);
    inner.raw(16);
    const retryId = inner.long();
    const gB = inner.bytes();
    const dataLen = 4 + 16 + 16 + 8 + (gB.length <= 253 ? 1 : 4) + gB.length;
    const padded = dataLen + ((4 - (dataLen % 4)) % 4);
    const hashOk = equal(await sha1(dataWithHash.slice(20, 20 + padded)), dataWithHash.slice(0, 20));
    trace.push({ label: 'Расшифровано AES-256-IGE, SHA1 проверен', value: hashOk ? 'совпал ✓' : 'не совпал ✗' });
    trace.push({ label: 'retry_id', value: hex(retryId) });
    trace.push({ label: 'Получено g_b', value: hex(gB) });

    const gBNum = bytesToBigInt(gB);
    const limit = 1n << 1984n; // 2^(2048-64)
    const rangeOk = gBNum > 1n && gBNum < this.state.dhPrime - 1n && gBNum >= limit && gBNum <= this.state.dhPrime - limit;
    trace.push({ label: 'Проверка 1 < g_b < dh_prime-1 и 2^1984 ≤ g_b ≤ dh_prime−2^1984', value: rangeOk ? 'выполнено ✓' : 'нарушено ✗' });

    const authKeyNum = modPow(gBNum, this.state.a, this.state.dhPrime);
    const authKey = bigIntToBytes(authKeyNum, 256);
    const authKeySha1 = await sha1(authKey);
    const authKeyAuxHash = authKeySha1.slice(0, 8);
    const authKeyId = authKeySha1.slice(12, 20);
    this.state = { ...this.state, authKey, authKeyId, authKeyAuxHash };
    trace.push({ label: 'auth_key = g_b^a mod dh_prime', value: hex(authKey) });
    trace.push({ label: 'auth_key_id = 64 младших бита SHA1(auth_key)', value: hex(authKeyId) });

    const newNonceHash1 = (await sha1(this.state.newNonce, new Uint8Array([1]), authKeyAuxHash)).slice(4, 20);
    const body = new TLWriter();
    body.constructorId('dh_gen_ok', CTOR.dh_gen_ok);
    body.raw('nonce', nonce, 'int128');
    body.raw('server_nonce', serverNonce, 'int128');
    body.raw('new_nonce_hash1', newNonceHash1, 'int128', '128 младших бит SHA1(new_nonce + 0x01 + auth_key_aux_hash)');

    const message = plainMessage(body.result(), newMessageId(), body.fields);
    return { ...message, trace, authKey, authKeyId, authKeyAuxHash, gB: gBNum };
  }
}

/** Вывод временного ключа AES из new_nonce и server_nonce (общий для клиента и сервера). */
export async function deriveTmpAes(newNonce, serverNonce) {
  const h1 = await sha1(newNonce, serverNonce);
  const h2 = await sha1(serverNonce, newNonce);
  const h3 = await sha1(newNonce, newNonce);
  const tmpAesKey = concat(h1, h2.slice(0, 12));
  const tmpAesIv = concat(h2.slice(12, 20), h3, newNonce.slice(0, 4));
  return { tmpAesKey, tmpAesIv, h1, h2, h3 };
}
