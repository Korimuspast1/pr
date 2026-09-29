// RSA для MTProto: «сырое» возведение в степень + схема набивки RSA_PAD
// (вариант OAEP+), описанная в п. 4.1 документа «Creating an Authorization Key».

import { igeDecrypt, igeEncrypt } from './aes.js';
import { bigIntToBytes, bytesToBigInt } from './bigint.js';
import { concat, equal, hex, randomBytes, reverseBytes, xor } from './bytes.js';
import { sha1, sha256 } from './hash.js';
import { TLWriter } from './tl.js';

function b64urlToBytes(str) {
  const b64 = str.replace(/-/g, '+').replace(/_/g, '/');
  const bin = atob(b64.padEnd(Math.ceil(b64.length / 4) * 4, '='));
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

/**
 * Демонстрационная пара RSA-2048 «сервера».
 * Настоящие открытые ключи Telegram зашиты в клиентах, но приватная часть известна
 * только Telegram — поэтому здесь сервер генерирует собственный ключ, и тогда
 * расшифровку RSA в демо можно показать по-настоящему.
 */
export async function generateServerRsaKey() {
  const pair = await globalThis.crypto.subtle.generateKey(
    { name: 'RSA-OAEP', modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' },
    true,
    ['encrypt', 'decrypt']
  );
  const jwk = await globalThis.crypto.subtle.exportKey('jwk', pair.privateKey);
  const nBytes = b64urlToBytes(jwk.n);
  const eBytes = b64urlToBytes(jwk.e);
  const dBytes = b64urlToBytes(jwk.d);
  const key = {
    nBytes,
    eBytes,
    n: bytesToBigInt(nBytes),
    e: bytesToBigInt(eBytes),
    d: bytesToBigInt(dBytes),
  };
  key.fingerprint = await rsaFingerprint(nBytes, eBytes);
  return key;
}

/**
 * Отпечаток ключа: 64 младших бита SHA1 от TL-сериализации
 * rsa_public_key n:string e:string = RSAPublicKey (bare-тип).
 */
export async function rsaFingerprint(nBytes, eBytes) {
  const w = new TLWriter();
  w.bytes('n', nBytes);
  w.bytes('e', eBytes);
  const digest = await sha1(w.result());
  return digest.slice(12, 20); // младшие 64 бита
}

const ZERO_IV_32 = new Uint8Array(32);

/** RSA_PAD(data, server_public_key) — см. п. 4.1 документации. */
export async function rsaPad(data, key) {
  if (data.length > 144) throw new Error('data длиннее 144 байт');
  const padding = randomBytes(192 - data.length);
  const dataWithPadding = concat(data, padding);
  const dataPadReversed = reverseBytes(dataWithPadding);

  let attempts = 0;
  for (;;) {
    attempts++;
    const tempKey = randomBytes(32);
    const dataWithHash = concat(dataPadReversed, await sha256(tempKey, dataWithPadding));
    const aesEncrypted = igeEncrypt(dataWithHash, tempKey, ZERO_IV_32);
    const tempKeyXor = xor(tempKey, await sha256(aesEncrypted));
    const keyAesEncrypted = concat(tempKeyXor, aesEncrypted);
    // Если число ≥ модуля RSA — повторяем с новым temp_key.
    if (bytesToBigInt(keyAesEncrypted) >= key.n) continue;
    const encrypted = bigIntToBytes(
      // «сырое» RSA: m^e mod n
      modPowLocal(bytesToBigInt(keyAesEncrypted), key.e, key.n),
      256
    );
    return { encrypted, dataWithPadding, dataPadReversed, tempKey, aesEncrypted, tempKeyXor, keyAesEncrypted, attempts };
  }
}

/** Обратное преобразование на стороне сервера. */
export async function rsaUnpad(encrypted, key) {
  const keyAesEncrypted = bigIntToBytes(modPowLocal(bytesToBigInt(encrypted), key.d, key.n), 256);
  const tempKeyXor = keyAesEncrypted.slice(0, 32);
  const aesEncrypted = keyAesEncrypted.slice(32);
  const tempKey = xor(tempKeyXor, await sha256(aesEncrypted));
  const dataWithHash = igeDecrypt(aesEncrypted, tempKey, ZERO_IV_32);
  const dataPadReversed = dataWithHash.slice(0, 192);
  const hashPart = dataWithHash.slice(192);
  const dataWithPadding = reverseBytes(dataPadReversed);
  const ok = equal(await sha256(tempKey, dataWithPadding), hashPart);
  if (!ok) throw new Error('RSA_PAD: SHA256 не сошёлся, данные повреждены');
  return { dataWithPadding, tempKey, hashOk: ok, hash: hex(hashPart) };
}

function modPowLocal(base, exp, mod) {
  let result = 1n;
  base %= mod;
  while (exp > 0n) {
    if (exp & 1n) result = (result * base) % mod;
    base = (base * base) % mod;
    exp >>= 1n;
  }
  return result;
}
