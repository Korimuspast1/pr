// AES-256 (чистый JS, блочный примитив) + режим IGE, который использует MTProto.
// WebCrypto не умеет ни «сырой» ECB, ни IGE, поэтому блочный шифр реализован вручную.

const SBOX = new Uint8Array(256);
const INV_SBOX = new Uint8Array(256);
const RCON = new Uint8Array([0x8d, 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x1b, 0x36, 0x6c, 0xd8, 0xab, 0x4d]);

(function initTables() {
  // Построение S-box через мультипликативную инверсию в GF(2^8).
  const p = new Uint8Array(256);
  const log = new Uint8Array(256);
  let x = 1;
  for (let i = 0; i < 255; i++) {
    p[i] = x;
    log[x] = i;
    x ^= (x << 1) ^ (x & 0x80 ? 0x11b : 0);
    x &= 0xff;
  }
  const inv = (a) => (a === 0 ? 0 : p[(255 - log[a]) % 255]);
  for (let i = 0; i < 256; i++) {
    let s = inv(i);
    let y = s;
    for (let j = 0; j < 4; j++) {
      y = ((y << 1) | (y >>> 7)) & 0xff;
      s ^= y;
    }
    s ^= 0x63;
    SBOX[i] = s;
    INV_SBOX[s] = i;
  }
})();

function xtime(a) {
  return ((a << 1) ^ (a & 0x80 ? 0x1b : 0)) & 0xff;
}

function mul(a, b) {
  let r = 0;
  while (b) {
    if (b & 1) r ^= a;
    a = xtime(a);
    b >>= 1;
  }
  return r & 0xff;
}

/** Развёртка ключа AES-256 (Nk = 8, Nr = 14). */
function expandKey(key) {
  if (key.length !== 32) throw new Error('Ожидается 256-битный ключ AES');
  const Nk = 8;
  const Nr = 14;
  const w = new Uint8Array(16 * (Nr + 1));
  w.set(key, 0);
  for (let i = Nk; i < 4 * (Nr + 1); i++) {
    let t = [w[(i - 1) * 4], w[(i - 1) * 4 + 1], w[(i - 1) * 4 + 2], w[(i - 1) * 4 + 3]];
    if (i % Nk === 0) {
      t = [SBOX[t[1]] ^ RCON[i / Nk], SBOX[t[2]], SBOX[t[3]], SBOX[t[0]]];
    } else if (i % Nk === 4) {
      t = [SBOX[t[0]], SBOX[t[1]], SBOX[t[2]], SBOX[t[3]]];
    }
    for (let j = 0; j < 4; j++) w[i * 4 + j] = w[(i - Nk) * 4 + j] ^ t[j];
  }
  return { w, Nr };
}

function addRoundKey(state, w, round) {
  for (let i = 0; i < 16; i++) state[i] ^= w[round * 16 + i];
}

function encryptBlock(state, w, Nr) {
  addRoundKey(state, w, 0);
  for (let round = 1; round <= Nr; round++) {
    for (let i = 0; i < 16; i++) state[i] = SBOX[state[i]];
    shiftRows(state);
    if (round !== Nr) mixColumns(state);
    addRoundKey(state, w, round);
  }
}

function decryptBlock(state, w, Nr) {
  addRoundKey(state, w, Nr);
  for (let round = Nr - 1; round >= 0; round--) {
    invShiftRows(state);
    for (let i = 0; i < 16; i++) state[i] = INV_SBOX[state[i]];
    addRoundKey(state, w, round);
    if (round !== 0) invMixColumns(state);
  }
}

function shiftRows(s) {
  let t;
  t = s[1]; s[1] = s[5]; s[5] = s[9]; s[9] = s[13]; s[13] = t;
  t = s[2]; s[2] = s[10]; s[10] = t;
  t = s[6]; s[6] = s[14]; s[14] = t;
  t = s[15]; s[15] = s[11]; s[11] = s[7]; s[7] = s[3]; s[3] = t;
}

function invShiftRows(s) {
  let t;
  t = s[13]; s[13] = s[9]; s[9] = s[5]; s[5] = s[1]; s[1] = t;
  t = s[2]; s[2] = s[10]; s[10] = t;
  t = s[6]; s[6] = s[14]; s[14] = t;
  t = s[3]; s[3] = s[7]; s[7] = s[11]; s[11] = s[15]; s[15] = t;
}

function mixColumns(s) {
  for (let c = 0; c < 4; c++) {
    const i = c * 4;
    const a0 = s[i], a1 = s[i + 1], a2 = s[i + 2], a3 = s[i + 3];
    s[i] = xtime(a0) ^ (xtime(a1) ^ a1) ^ a2 ^ a3;
    s[i + 1] = a0 ^ xtime(a1) ^ (xtime(a2) ^ a2) ^ a3;
    s[i + 2] = a0 ^ a1 ^ xtime(a2) ^ (xtime(a3) ^ a3);
    s[i + 3] = (xtime(a0) ^ a0) ^ a1 ^ a2 ^ xtime(a3);
  }
}

function invMixColumns(s) {
  for (let c = 0; c < 4; c++) {
    const i = c * 4;
    const a0 = s[i], a1 = s[i + 1], a2 = s[i + 2], a3 = s[i + 3];
    s[i] = mul(a0, 14) ^ mul(a1, 11) ^ mul(a2, 13) ^ mul(a3, 9);
    s[i + 1] = mul(a0, 9) ^ mul(a1, 14) ^ mul(a2, 11) ^ mul(a3, 13);
    s[i + 2] = mul(a0, 13) ^ mul(a1, 9) ^ mul(a2, 14) ^ mul(a3, 11);
    s[i + 3] = mul(a0, 11) ^ mul(a1, 13) ^ mul(a2, 9) ^ mul(a3, 14);
  }
}

/**
 * AES-256-IGE (Infinite Garble Extension) — режим, применяемый в MTProto.
 * iv — 32 байта: первые 16 — «предыдущий шифроблок», вторые 16 — «предыдущий блок открытого текста».
 *   шифрование:  c_i = E(p_i XOR c_{i-1}) XOR p_{i-1}
 *   расшифровка: p_i = D(c_i XOR p_{i-1}) XOR c_{i-1}
 */
export function igeEncrypt(data, key, iv) {
  return ige(data, key, iv, true);
}

export function igeDecrypt(data, key, iv) {
  return ige(data, key, iv, false);
}

function ige(data, key, iv, encrypt) {
  if (data.length % 16 !== 0) throw new Error('Длина данных для IGE должна быть кратна 16 байтам');
  if (iv.length !== 32) throw new Error('IGE требует 32-байтовый IV');
  const { w, Nr } = expandKey(key);
  const out = new Uint8Array(data.length);
  let iv1 = encrypt ? iv.slice(0, 16) : iv.slice(16, 32);
  let iv2 = encrypt ? iv.slice(16, 32) : iv.slice(0, 16);
  const block = new Uint8Array(16);
  for (let i = 0; i < data.length; i += 16) {
    const chunk = data.subarray(i, i + 16);
    for (let j = 0; j < 16; j++) block[j] = chunk[j] ^ iv1[j];
    if (encrypt) encryptBlock(block, w, Nr);
    else decryptBlock(block, w, Nr);
    for (let j = 0; j < 16; j++) block[j] ^= iv2[j];
    out.set(block, i);
    iv1 = block.slice();
    iv2 = chunk.slice();
  }
  return out;
}

/** Один «сырой» блок AES-256 — используется в тестах. */
export function aesEncryptBlock(block, key) {
  const { w, Nr } = expandKey(key);
  const s = block.slice();
  encryptBlock(s, w, Nr);
  return s;
}
