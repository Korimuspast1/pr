// Большие числа: конвертация байт <-> BigInt, возведение в степень по модулю,
// факторизация pq (Брент–Полард) и тест Миллера–Рабина для проверки dh_prime.

export function bytesToBigInt(bytes) {
  let n = 0n;
  for (const b of bytes) n = (n << 8n) | BigInt(b);
  return n;
}

export function bigIntToBytes(value, length) {
  let hexStr = value.toString(16);
  if (hexStr.length % 2) hexStr = '0' + hexStr;
  let bytes = new Uint8Array(hexStr.length / 2);
  for (let i = 0; i < bytes.length; i++) bytes[i] = parseInt(hexStr.substr(i * 2, 2), 16);
  if (length !== undefined) {
    if (bytes.length > length) {
      // отбрасываем ведущие нули
      const start = bytes.length - length;
      for (let i = 0; i < start; i++) if (bytes[i] !== 0) throw new Error('Число не помещается в заданную длину');
      bytes = bytes.slice(start);
    } else if (bytes.length < length) {
      const padded = new Uint8Array(length);
      padded.set(bytes, length - bytes.length);
      bytes = padded;
    }
  }
  return bytes;
}

/** Быстрое возведение в степень по модулю (двоичный метод справа налево). */
export function modPow(base, exp, mod) {
  let result = 1n;
  base %= mod;
  while (exp > 0n) {
    if (exp & 1n) result = (result * base) % mod;
    base = (base * base) % mod;
    exp >>= 1n;
  }
  return result;
}

export function bitLength(n) {
  return n === 0n ? 0 : n.toString(2).length;
}

function abs(n) {
  return n < 0n ? -n : n;
}

function gcd(a, b) {
  while (b) {
    [a, b] = [b, a % b];
  }
  return a;
}

/**
 * Разложение pq (обычно ~63 бита) на два простых множителя p < q.
 * Алгоритм Брента — вариант ро-алгоритма Полларда.
 */
export function factorize(pq) {
  if (pq % 2n === 0n) return { p: 2n, q: pq / 2n, iterations: 0 };
  let iterations = 0;
  let divisor = 0n;
  for (let c = 1n; c < 100n && divisor === 0n; c++) {
    let y = 2n;
    let x = 2n;
    let ys = 2n;
    let r = 1n;
    let m = 128n;
    let g = 1n;
    let qAcc = 1n;
    while (g === 1n) {
      x = y;
      for (let i = 0n; i < r; i++) y = (y * y + c) % pq;
      let k = 0n;
      while (k < r && g === 1n) {
        ys = y;
        const bound = m < r - k ? m : r - k;
        for (let i = 0n; i < bound; i++) {
          y = (y * y + c) % pq;
          qAcc = (qAcc * abs(x - y)) % pq;
          iterations++;
        }
        g = gcd(qAcc, pq);
        k += bound;
      }
      r *= 2n;
      if (r > 1n << 40n) break;
    }
    if (g === pq) {
      g = 1n;
      do {
        ys = (ys * ys + c) % pq;
        g = gcd(abs(x - ys), pq);
        iterations++;
      } while (g === 1n);
    }
    if (g !== pq && g !== 1n) divisor = g;
  }
  if (divisor === 0n) throw new Error('Не удалось разложить pq');
  const other = pq / divisor;
  const p = divisor < other ? divisor : other;
  const q = divisor < other ? other : divisor;
  return { p, q, iterations };
}

const SMALL_PRIMES = [2n, 3n, 5n, 7n, 11n, 13n, 17n, 19n, 23n, 29n, 31n, 37n, 41n, 43n, 47n];

/** Вероятностный тест простоты Миллера — Рабина. */
export function isProbablePrime(n, rounds = 15) {
  if (n < 2n) return false;
  for (const p of SMALL_PRIMES) {
    if (n === p) return true;
    if (n % p === 0n) return false;
  }
  let d = n - 1n;
  let s = 0n;
  while (d % 2n === 0n) {
    d /= 2n;
    s++;
  }
  const bytes = (bitLength(n) + 7) >> 3;
  for (let i = 0; i < rounds; i++) {
    let a;
    if (i < SMALL_PRIMES.length) {
      a = SMALL_PRIMES[i];
    } else {
      const buf = new Uint8Array(bytes);
      globalThis.crypto.getRandomValues(buf);
      a = (bytesToBigInt(buf) % (n - 4n)) + 2n;
    }
    let x = modPow(a, d, n);
    if (x === 1n || x === n - 1n) continue;
    let composite = true;
    for (let r = 1n; r < s; r++) {
      x = (x * x) % n;
      if (x === n - 1n) {
        composite = false;
        break;
      }
    }
    if (composite) return false;
  }
  return true;
}
