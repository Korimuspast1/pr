(function (root) {
  "use strict";

  /* =========================================================
     terrain.js — рельеф мира (вид сбоку)
     Чистый модуль: можно грузить и в браузере, и в Node.
     ========================================================= */

  const WORLD_W = 6800;
  const WORLD_H = 1600;
  const PPM = 60; // пикселей в метре

  function hash2(i, seed) {
    let h = Math.imul(i ^ seed, 0x27d4eb2d);
    h ^= h >>> 15;
    h = Math.imul(h, 0x85ebca6b);
    h ^= h >>> 13;
    return (h >>> 0) / 4294967296;
  }

  function smooth(t) {
    return t * t * (3 - 2 * t);
  }

  function noise1(x, seed) {
    const i = Math.floor(x);
    const f = smooth(x - i);
    const a = hash2(i, seed);
    const b = hash2(i + 1, seed);
    return a + (b - a) * f;
  }

  function fbm1(x, seed, octaves) {
    let amp = 1;
    let freq = 1;
    let sum = 0;
    let norm = 0;
    for (let o = 0; o < octaves; o++) {
      sum += noise1(x * freq, seed + o * 1013) * amp;
      norm += amp;
      amp *= 0.5;
      freq *= 2.03;
    }
    return sum / norm;
  }

  class Terrain {
    constructor(seed) {
      this.seed = seed || 1337;
      this.width = WORLD_W;
      this.height = WORLD_H;
      this.step = 4;
      this.baseY = 1020;
      this.samples = new Float32Array(Math.ceil(WORLD_W / this.step) + 2);
      this._build();
    }

    _rawHeight(x) {
      const s = this.seed;
      // крупные холмы + средние неровности + мелкая трава/кочки
      let h = 0;
      h += (fbm1(x * 0.00075, s, 4) - 0.5) * 300;
      h += (fbm1(x * 0.0042, s + 77, 3) - 0.5) * 72;
      h += (fbm1(x * 0.02, s + 991, 2) - 0.5) * 10;
      return this.baseY + h;
    }

    _build() {
      const n = this.samples.length;
      for (let i = 0; i < n; i++) {
        const x = i * this.step;
        let y = this._rawHeight(x);

        // плоская зона спавна в начале карты
        const flat = this._range(x, 260, 1250, 160);
        if (flat > 0) {
          const flatY = this._rawHeight(700);
          y = y + (flatY - y) * flat;
        }

        // небольшой котлован-яма для экспериментов
        const pit = this._range(x, 2950, 3420, 180);
        if (pit > 0) {
          const t = this._range(x, 2950, 3420, 0);
          y += pit * (60 + 55 * Math.sin(Math.PI * Math.min(1, Math.max(0, t))));
        }

        this.samples[i] = y;
      }
    }

    _range(x, a, b, feather) {
      if (x < a - feather || x > b + feather) return 0;
      if (x >= a && x <= b) return 1;
      if (x < a) return smooth((x - (a - feather)) / feather);
      return smooth(((b + feather) - x) / feather);
    }

    groundY(x) {
      const fi = x / this.step;
      let i = Math.floor(fi);
      if (i < 0) return this.samples[0];
      if (i >= this.samples.length - 1) return this.samples[this.samples.length - 1];
      const f = fi - i;
      const a = this.samples[i];
      const b = this.samples[i + 1];
      return a + (b - a) * f;
    }

    slope(x) {
      const d = 3;
      return (this.groundY(x + d) - this.groundY(x - d)) / (2 * d);
    }
  }

  root.Terrain = Terrain;
  root.WORLD_CONST = { WORLD_W, WORLD_H, PPM };
})(typeof globalThis !== "undefined" ? globalThis : this);
