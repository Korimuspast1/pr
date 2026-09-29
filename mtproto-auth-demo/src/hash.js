// Хеш-функции поверх WebCrypto (доступны и в браузере, и в Node 18+).

import { concat } from './bytes.js';

const subtle = globalThis.crypto.subtle;

export async function sha1(...parts) {
  const data = concat(...parts);
  const digest = await subtle.digest('SHA-1', data);
  return new Uint8Array(digest);
}

export async function sha256(...parts) {
  const data = concat(...parts);
  const digest = await subtle.digest('SHA-256', data);
  return new Uint8Array(digest);
}
