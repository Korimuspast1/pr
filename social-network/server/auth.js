'use strict';

const { scryptSync, randomBytes, timingSafeEqual } = require('node:crypto');

// Пароли хешируются scrypt с уникальной солью для каждого пользователя.
function hashPassword(password) {
  const salt = randomBytes(16).toString('hex');
  const hash = scryptSync(password, salt, 64).toString('hex');
  return { salt, passwordHash: hash };
}

function verifyPassword(password, salt, passwordHash) {
  // Аккаунты-боты (без пароля) войти не могут.
  if (!salt || !passwordHash) return false;
  const actual = scryptSync(password, salt, 64);
  const expected = Buffer.from(passwordHash, 'hex');
  return expected.length === actual.length && timingSafeEqual(actual, expected);
}

function parseCookies(header = '') {
  const out = {};
  for (const part of header.split(';')) {
    const idx = part.indexOf('=');
    if (idx === -1) continue;
    const key = part.slice(0, idx).trim();
    const value = part.slice(idx + 1).trim();
    if (key) out[key] = decodeURIComponent(value);
  }
  return out;
}

module.exports = { hashPassword, verifyPassword, parseCookies };
