/**
 * Real Causal Multi-Head Self-Attention Transformer implemented in pure TypeScript
 * with Float32Array weights, RMSNorm, Residual connections, and full AdamW Backpropagation.
 * Runs & trains live on-device (Android WebView / Browser) with zero external APIs.
 */

export interface TransformerConfig {
  vocabSize: number;
  blockSize: number;
  dModel: number;
  nHead: number;
  nLayer: number;
}

export interface SerializedCheckpoint {
  version: 1;
  config: TransformerConfig;
  vocab: string[];
  weights: number[];
  stepCount: number;
  lastLoss: number;
  lossHistory: number[];
}

function gelu(x: number): number {
  const c = 0.7978845608028654; // sqrt(2/pi)
  return 0.5 * x * (1 + Math.tanh(c * (x + 0.044715 * x * x * x)));
}

function geluGrad(x: number): number {
  const c = 0.7978845608028654;
  const x3 = x * x * x;
  const inner = c * (x + 0.044715 * x3);
  const t = Math.tanh(inner);
  const sech2 = 1 - t * t;
  const dinner = c * (1 + 3 * 0.044715 * x * x);
  return 0.5 * (1 + t) + 0.5 * x * sech2 * dinner;
}

interface LayerSlices {
  ln1G: Float32Array;
  wq: Float32Array;
  wk: Float32Array;
  wv: Float32Array;
  wo: Float32Array;
  ln2G: Float32Array;
  w1: Float32Array;
  w2: Float32Array;
}

export class MiniTransformer {
  config: TransformerConfig;
  vocab: string[];
  charToId: Map<string, number>;

  // Packed parameter buffer + grads + AdamW moments
  params: Float32Array;
  grads: Float32Array;
  m1: Float32Array;
  m2: Float32Array;

  wte: Float32Array;
  wpe: Float32Array;
  layers: LayerSlices[];
  lnFG: Float32Array;
  lmHead: Float32Array;

  stepCount = 0;
  adamStep = 0;
  lastLoss = 4.2;
  lossHistory: number[] = [];

  constructor(vocab: string[], partial?: Partial<TransformerConfig>) {
    this.vocab = [...vocab];
    this.charToId = new Map(this.vocab.map((ch, idx) => [ch, idx]));

    this.config = {
      vocabSize: this.vocab.length,
      blockSize: partial?.blockSize ?? 48,
      dModel: partial?.dModel ?? 48,
      nHead: partial?.nHead ?? 4,
      nLayer: partial?.nLayer ?? 2,
    };

    const { vocabSize, blockSize, dModel, nLayer } = this.config;
    const mlpHidden = dModel * 2;

    const totalSize =
      vocabSize * dModel + // wte
      blockSize * dModel + // wpe
      nLayer * (dModel + 4 * dModel * dModel + dModel + dModel * mlpHidden + mlpHidden * dModel) +
      dModel + // lnFG
      dModel * vocabSize; // lmHead

    this.params = new Float32Array(totalSize);
    this.grads = new Float32Array(totalSize);
    this.m1 = new Float32Array(totalSize);
    this.m2 = new Float32Array(totalSize);

    // Slice views into packed buffer
    let off = 0;
    const alloc = (len: number) => {
      const view = this.params.subarray(off, off + len);
      off += len;
      return view;
    };

    this.wte = alloc(vocabSize * dModel);
    this.wpe = alloc(blockSize * dModel);
    this.layers = [];
    for (let l = 0; l < nLayer; l++) {
      this.layers.push({
        ln1G: alloc(dModel),
        wq: alloc(dModel * dModel),
        wk: alloc(dModel * dModel),
        wv: alloc(dModel * dModel),
        wo: alloc(dModel * dModel),
        ln2G: alloc(dModel),
        w1: alloc(dModel * mlpHidden),
        w2: alloc(mlpHidden * dModel),
      });
    }
    this.lnFG = alloc(dModel);
    this.lmHead = alloc(dModel * vocabSize);

    this.initWeights();
  }

  private initWeights() {
    // Deterministic seeded PRNG for reproducible neural initialization
    let seed = 133742;
    const randn = (std: number) => {
      seed = (seed * 1664525 + 1013904223) >>> 0;
      const u1 = Math.max(1e-7, seed / 4294967296);
      seed = (seed * 1664525 + 1013904223) >>> 0;
      const u2 = seed / 4294967296;
      return std * Math.sqrt(-2 * Math.log(u1)) * Math.cos(2 * Math.PI * u2);
    };

    const { dModel, nLayer } = this.config;
    const baseStd = 1 / Math.sqrt(dModel);
    const projStd = baseStd / Math.sqrt(2 * nLayer);

    for (let i = 0; i < this.wte.length; i++) this.wte[i] = randn(0.04);
    for (let i = 0; i < this.wpe.length; i++) this.wpe[i] = randn(0.04);

    for (const layer of this.layers) {
      layer.ln1G.fill(1);
      layer.ln2G.fill(1);
      for (let i = 0; i < layer.wq.length; i++) layer.wq[i] = randn(baseStd);
      for (let i = 0; i < layer.wk.length; i++) layer.wk[i] = randn(baseStd);
      for (let i = 0; i < layer.wv.length; i++) layer.wv[i] = randn(baseStd);
      for (let i = 0; i < layer.wo.length; i++) layer.wo[i] = randn(projStd);
      for (let i = 0; i < layer.w1.length; i++) layer.w1[i] = randn(baseStd);
      for (let i = 0; i < layer.w2.length; i++) layer.w2[i] = randn(projStd);
    }

    this.lnFG.fill(1);
    for (let i = 0; i < this.lmHead.length; i++) this.lmHead[i] = randn(baseStd);
  }

  getParamCount(): number {
    return this.params.length;
  }

  encode(text: string): number[] {
    const out: number[] = [];
    for (const ch of text) {
      const id = this.charToId.get(ch);
      if (id !== undefined) out.push(id);
    }
    return out;
  }

  decode(ids: number[]): string {
    return ids.map((i) => this.vocab[i] ?? '').join('');
  }

  static buildVocab(text: string): string[] {
    const baseChars =
      '\n !?.,:;()-"\'/0123456789' +
      'абвгдеёжзийклмнопрстуфхцчшщъыьэюя' +
      'АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ' +
      'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ';
    const set = new Set<string>(Array.from(baseChars));
    for (const ch of text) set.add(ch);
    return Array.from(set);
  }

  private forwardSequence(tokens: number[], targets?: number[]) {
    const { vocabSize, dModel, nHead, nLayer } = this.config;
    const headDim = dModel / nHead;
    const mlpHidden = dModel * 2;
    const T = tokens.length;

    // Activation cache for backward pass
    const xBuf: Float32Array[] = [];
    const layerCaches: Array<{
      xIn: Float32Array;
      norm1: Float32Array;
      rms1: Float32Array;
      q: Float32Array;
      k: Float32Array;
      v: Float32Array;
      attProbs: Float32Array;
      attOut: Float32Array;
      xMid: Float32Array;
      norm2: Float32Array;
      rms2: Float32Array;
      hPre: Float32Array;
      hAct: Float32Array;
    }> = [];

    const x0 = new Float32Array(T * dModel);
    for (let t = 0; t < T; t++) {
      const tok = tokens[t];
      for (let d = 0; d < dModel; d++) {
        x0[t * dModel + d] = this.wte[tok * dModel + d] + this.wpe[t * dModel + d];
      }
    }
    xBuf.push(x0);

    let x = x0;
    for (let l = 0; l < nLayer; l++) {
      const L = this.layers[l];
      const norm1 = new Float32Array(T * dModel);
      const rms1 = new Float32Array(T);
      for (let t = 0; t < T; t++) {
        let ss = 0;
        for (let d = 0; d < dModel; d++) {
          const val = x[t * dModel + d];
          ss += val * val;
        }
        const invRms = 1 / Math.sqrt(ss / dModel + 1e-5);
        rms1[t] = invRms;
        for (let d = 0; d < dModel; d++) {
          norm1[t * dModel + d] = x[t * dModel + d] * invRms * L.ln1G[d];
        }
      }

      const q = new Float32Array(T * dModel);
      const k = new Float32Array(T * dModel);
      const v = new Float32Array(T * dModel);
      for (let t = 0; t < T; t++) {
        const nOff = t * dModel;
        for (let o = 0; o < dModel; o++) {
          let sq = 0;
          let sk = 0;
          let sv = 0;
          const wOff = o * dModel;
          for (let i = 0; i < dModel; i++) {
            const ni = norm1[nOff + i];
            sq += ni * L.wq[wOff + i];
            sk += ni * L.wk[wOff + i];
            sv += ni * L.wv[wOff + i];
          }
          q[nOff + o] = sq;
          k[nOff + o] = sk;
          v[nOff + o] = sv;
        }
      }

      const attProbs = new Float32Array(nHead * T * T);
      const attOut = new Float32Array(T * dModel);
      const scale = 1 / Math.sqrt(headDim);

      for (let h = 0; h < nHead; h++) {
        const hOff = h * headDim;
        for (let t = 0; t < T; t++) {
          let maxLogit = -1e9;
          const rowOff = (h * T + t) * T;
          for (let s = 0; s <= t; s++) {
            let dot = 0;
            for (let d = 0; d < headDim; d++) {
              dot += q[t * dModel + hOff + d] * k[s * dModel + hOff + d];
            }
            const logit = dot * scale;
            attProbs[rowOff + s] = logit;
            if (logit > maxLogit) maxLogit = logit;
          }
          let sumExp = 0;
          for (let s = 0; s <= t; s++) {
            const e = Math.exp(attProbs[rowOff + s] - maxLogit);
            attProbs[rowOff + s] = e;
            sumExp += e;
          }
          const invSum = 1 / (sumExp + 1e-9);
          for (let s = 0; s <= t; s++) attProbs[rowOff + s] *= invSum;

          for (let d = 0; d < headDim; d++) {
            let acc = 0;
            for (let s = 0; s <= t; s++) {
              acc += attProbs[rowOff + s] * v[s * dModel + hOff + d];
            }
            attOut[t * dModel + hOff + d] = acc;
          }
        }
      }

      const xMid = new Float32Array(T * dModel);
      for (let t = 0; t < T; t++) {
        const off = t * dModel;
        for (let o = 0; o < dModel; o++) {
          let acc = x[off + o];
          const wOff = o * dModel;
          for (let i = 0; i < dModel; i++) {
            acc += attOut[off + i] * L.wo[wOff + i];
          }
          xMid[off + o] = acc;
        }
      }

      const norm2 = new Float32Array(T * dModel);
      const rms2 = new Float32Array(T);
      for (let t = 0; t < T; t++) {
        let ss = 0;
        for (let d = 0; d < dModel; d++) {
          const val = xMid[t * dModel + d];
          ss += val * val;
        }
        const invRms = 1 / Math.sqrt(ss / dModel + 1e-5);
        rms2[t] = invRms;
        for (let d = 0; d < dModel; d++) {
          norm2[t * dModel + d] = xMid[t * dModel + d] * invRms * L.ln2G[d];
        }
      }

      const hPre = new Float32Array(T * mlpHidden);
      const hAct = new Float32Array(T * mlpHidden);
      for (let t = 0; t < T; t++) {
        for (let m = 0; m < mlpHidden; m++) {
          let acc = 0;
          const wOff = m * dModel;
          for (let i = 0; i < dModel; i++) {
            acc += norm2[t * dModel + i] * L.w1[wOff + i];
          }
          hPre[t * mlpHidden + m] = acc;
          hAct[t * mlpHidden + m] = gelu(acc);
        }
      }

      const xNext = new Float32Array(T * dModel);
      for (let t = 0; t < T; t++) {
        for (let o = 0; o < dModel; o++) {
          let acc = xMid[t * dModel + o];
          const wOff = o * mlpHidden;
          for (let m = 0; m < mlpHidden; m++) {
            acc += hAct[t * mlpHidden + m] * L.w2[wOff + m];
          }
          xNext[t * dModel + o] = acc;
        }
      }

      layerCaches.push({
        xIn: x,
        norm1,
        rms1,
        q,
        k,
        v,
        attProbs,
        attOut,
        xMid,
        norm2,
        rms2,
        hPre,
        hAct,
      });
      x = xNext;
      xBuf.push(x);
    }

    // Final RMSNorm + LM head logits
    const finalNorm = new Float32Array(T * dModel);
    const finalRms = new Float32Array(T);
    for (let t = 0; t < T; t++) {
      let ss = 0;
      for (let d = 0; d < dModel; d++) {
        const val = x[t * dModel + d];
        ss += val * val;
      }
      const invRms = 1 / Math.sqrt(ss / dModel + 1e-5);
      finalRms[t] = invRms;
      for (let d = 0; d < dModel; d++) {
        finalNorm[t * dModel + d] = x[t * dModel + d] * invRms * this.lnFG[d];
      }
    }

    const logits = new Float32Array(T * vocabSize);
    for (let t = 0; t < T; t++) {
      const fOff = t * dModel;
      const lOff = t * vocabSize;
      for (let vIdx = 0; vIdx < vocabSize; vIdx++) {
        let acc = 0;
        const wOff = vIdx * dModel;
        for (let d = 0; d < dModel; d++) {
          acc += finalNorm[fOff + d] * this.lmHead[wOff + d];
        }
        logits[lOff + vIdx] = acc;
      }
    }

    if (!targets) {
      return { logits, loss: 0 };
    }

    // Cross-entropy loss + backward pass
    let loss = 0;
    const dLogits = new Float32Array(T * vocabSize);
    const invT = 1 / T;

    for (let t = 0; t < T; t++) {
      const lOff = t * vocabSize;
      let maxL = -1e9;
      for (let vIdx = 0; vIdx < vocabSize; vIdx++) {
        if (logits[lOff + vIdx] > maxL) maxL = logits[lOff + vIdx];
      }
      let sumExp = 0;
      for (let vIdx = 0; vIdx < vocabSize; vIdx++) {
        const e = Math.exp(logits[lOff + vIdx] - maxL);
        dLogits[lOff + vIdx] = e;
        sumExp += e;
      }
      const invSum = 1 / (sumExp + 1e-9);
      const tgt = targets[t];
      const pTgt = Math.max(1e-9, dLogits[lOff + tgt] * invSum);
      loss -= Math.log(pTgt) * invT;
      for (let vIdx = 0; vIdx < vocabSize; vIdx++) {
        const p = dLogits[lOff + vIdx] * invSum;
        dLogits[lOff + vIdx] = (p - (vIdx === tgt ? 1 : 0)) * invT;
      }
    }

    // Backprop through LM Head + Final RMSNorm
    const gStartLm = this.lmHead.byteOffset / 4;
    const gStartLnF = this.lnFG.byteOffset / 4;
    const dFinalNorm = new Float32Array(T * dModel);

    for (let t = 0; t < T; t++) {
      const lOff = t * vocabSize;
      const fOff = t * dModel;
      for (let vIdx = 0; vIdx < vocabSize; vIdx++) {
        const dl = dLogits[lOff + vIdx];
        const wOff = vIdx * dModel;
        for (let d = 0; d < dModel; d++) {
          this.grads[gStartLm + wOff + d] += dl * finalNorm[fOff + d];
          dFinalNorm[fOff + d] += dl * this.lmHead[wOff + d];
        }
      }
    }

    let dx = new Float32Array(T * dModel);
    for (let t = 0; t < T; t++) {
      const fOff = t * dModel;
      const invRms = finalRms[t];
      let dot = 0;
      for (let d = 0; d < dModel; d++) {
        this.grads[gStartLnF + d] += dFinalNorm[fOff + d] * x[fOff + d] * invRms;
        dot += dFinalNorm[fOff + d] * this.lnFG[d] * x[fOff + d];
      }
      const scaleDot = (dot * invRms * invRms * invRms) / dModel;
      for (let d = 0; d < dModel; d++) {
        dx[fOff + d] = dFinalNorm[fOff + d] * this.lnFG[d] * invRms - x[fOff + d] * scaleDot;
      }
    }

    // Backprop through Transformer layers
    for (let l = nLayer - 1; l >= 0; l--) {
      const L = this.layers[l];
      const C = layerCaches[l];
      const gW2 = L.w2.byteOffset / 4;
      const gW1 = L.w1.byteOffset / 4;
      const gLn2 = L.ln2G.byteOffset / 4;
      const gWo = L.wo.byteOffset / 4;
      const gWq = L.wq.byteOffset / 4;
      const gWk = L.wk.byteOffset / 4;
      const gWv = L.wv.byteOffset / 4;
      const gLn1 = L.ln1G.byteOffset / 4;

      const dxMid = new Float32Array(dx); // residual branch
      const dHAct = new Float32Array(T * mlpHidden);
      for (let t = 0; t < T; t++) {
        for (let o = 0; o < dModel; o++) {
          const g = dx[t * dModel + o];
          const wOff = o * mlpHidden;
          for (let m = 0; m < mlpHidden; m++) {
            this.grads[gW2 + wOff + m] += g * C.hAct[t * mlpHidden + m];
            dHAct[t * mlpHidden + m] += g * L.w2[wOff + m];
          }
        }
      }

      const dNorm2 = new Float32Array(T * dModel);
      for (let t = 0; t < T; t++) {
        for (let m = 0; m < mlpHidden; m++) {
          const dh = dHAct[t * mlpHidden + m] * geluGrad(C.hPre[t * mlpHidden + m]);
          const wOff = m * dModel;
          for (let i = 0; i < dModel; i++) {
            this.grads[gW1 + wOff + i] += dh * C.norm2[t * dModel + i];
            dNorm2[t * dModel + i] += dh * L.w1[wOff + i];
          }
        }
      }

      for (let t = 0; t < T; t++) {
        const off = t * dModel;
        const invRms = C.rms2[t];
        let dot = 0;
        for (let d = 0; d < dModel; d++) {
          this.grads[gLn2 + d] += dNorm2[off + d] * C.xMid[off + d] * invRms;
          dot += dNorm2[off + d] * L.ln2G[d] * C.xMid[off + d];
        }
        const scaleDot = (dot * invRms * invRms * invRms) / dModel;
        for (let d = 0; d < dModel; d++) {
          dxMid[off + d] += dNorm2[off + d] * L.ln2G[d] * invRms - C.xMid[off + d] * scaleDot;
        }
      }

      // Attention output projection
      const dxIn = new Float32Array(dxMid); // residual branch
      const dAttOut = new Float32Array(T * dModel);
      for (let t = 0; t < T; t++) {
        const off = t * dModel;
        for (let o = 0; o < dModel; o++) {
          const g = dxMid[off + o];
          const wOff = o * dModel;
          for (let i = 0; i < dModel; i++) {
            this.grads[gWo + wOff + i] += g * C.attOut[off + i];
            dAttOut[off + i] += g * L.wo[wOff + i];
          }
        }
      }

      // Multi-head attention backward
      const dQ = new Float32Array(T * dModel);
      const dK = new Float32Array(T * dModel);
      const dV = new Float32Array(T * dModel);
      const scale = 1 / Math.sqrt(headDim);

      for (let h = 0; h < nHead; h++) {
        const hOff = h * headDim;
        for (let t = 0; t < T; t++) {
          const rowOff = (h * T + t) * T;
          const dP = new Float32Array(t + 1);
          for (let s = 0; s <= t; s++) {
            const p = C.attProbs[rowOff + s];
            let dp = 0;
            for (let d = 0; d < headDim; d++) {
              const dao = dAttOut[t * dModel + hOff + d];
              dV[s * dModel + hOff + d] += p * dao;
              dp += dao * C.v[s * dModel + hOff + d];
            }
            dP[s] = dp;
          }
          let sumPdP = 0;
          for (let s = 0; s <= t; s++) sumPdP += C.attProbs[rowOff + s] * dP[s];
          for (let s = 0; s <= t; s++) {
            const dLogit = C.attProbs[rowOff + s] * (dP[s] - sumPdP) * scale;
            for (let d = 0; d < headDim; d++) {
              dQ[t * dModel + hOff + d] += dLogit * C.k[s * dModel + hOff + d];
              dK[s * dModel + hOff + d] += dLogit * C.q[t * dModel + hOff + d];
            }
          }
        }
      }

      const dNorm1 = new Float32Array(T * dModel);
      for (let t = 0; t < T; t++) {
        const off = t * dModel;
        for (let o = 0; o < dModel; o++) {
          const gq = dQ[off + o];
          const gk = dK[off + o];
          const gv = dV[off + o];
          const wOff = o * dModel;
          for (let i = 0; i < dModel; i++) {
            const ni = C.norm1[off + i];
            this.grads[gWq + wOff + i] += gq * ni;
            this.grads[gWk + wOff + i] += gk * ni;
            this.grads[gWv + wOff + i] += gv * ni;
            dNorm1[off + i] += gq * L.wq[wOff + i] + gk * L.wk[wOff + i] + gv * L.wv[wOff + i];
          }
        }
      }

      for (let t = 0; t < T; t++) {
        const off = t * dModel;
        const invRms = C.rms1[t];
        let dot = 0;
        for (let d = 0; d < dModel; d++) {
          this.grads[gLn1 + d] += dNorm1[off + d] * C.xIn[off + d] * invRms;
          dot += dNorm1[off + d] * L.ln1G[d] * C.xIn[off + d];
        }
        const scaleDot = (dot * invRms * invRms * invRms) / dModel;
        for (let d = 0; d < dModel; d++) {
          dxIn[off + d] += dNorm1[off + d] * L.ln1G[d] * invRms - C.xIn[off + d] * scaleDot;
        }
      }

      dx = dxIn;
    }

    // Backprop through token & position embeddings
    const gWte = this.wte.byteOffset / 4;
    const gWpe = this.wpe.byteOffset / 4;
    for (let t = 0; t < T; t++) {
      const tok = tokens[t];
      for (let d = 0; d < dModel; d++) {
        const g = dx[t * dModel + d];
        this.grads[gWte + tok * dModel + d] += g;
        this.grads[gWpe + t * dModel + d] += g;
      }
    }

    return { logits, loss };
  }

  /**
   * Executes one AdamW training step over a batch of text chunks.
   */
  trainStep(corpusTokens: number[], lr = 0.008, batchSize = 4): number {
    const seqLen = Math.min(this.config.blockSize, Math.max(8, corpusTokens.length - 1));
    if (corpusTokens.length <= 2) return this.lastLoss;

    this.grads.fill(0);
    let totalLoss = 0;

    for (let b = 0; b < batchSize; b++) {
      const maxStart = Math.max(0, corpusTokens.length - seqLen - 1);
      const start = Math.floor(Math.random() * (maxStart + 1));
      const x = corpusTokens.slice(start, start + seqLen);
      const y = corpusTokens.slice(start + 1, start + seqLen + 1);
      const { loss } = this.forwardSequence(x, y);
      totalLoss += loss;
    }

    const avgLoss = totalLoss / batchSize;
    const invB = 1 / batchSize;

    // Gradient clipping (norm <= 1.0) + AdamW update
    let normSq = 0;
    for (let i = 0; i < this.grads.length; i++) {
      this.grads[i] *= invB;
      normSq += this.grads[i] * this.grads[i];
    }
    const norm = Math.sqrt(normSq);
    const clipScale = norm > 1.0 ? 1.0 / norm : 1.0;

    this.stepCount += 1;
    this.adamStep += 1;
    const beta1 = 0.9;
    const beta2 = 0.99;
    const eps = 1e-8;
    const bc1 = 1 - Math.pow(beta1, this.adamStep);
    const bc2 = 1 - Math.pow(beta2, this.adamStep);

    for (let i = 0; i < this.params.length; i++) {
      const g = this.grads[i] * clipScale;
      this.m1[i] = beta1 * this.m1[i] + (1 - beta1) * g;
      this.m2[i] = beta2 * this.m2[i] + (1 - beta2) * g * g;
      const mHat = this.m1[i] / bc1;
      const vHat = this.m2[i] / bc2;
      this.params[i] -= lr * (mHat / (Math.sqrt(vHat) + eps) + 0.005 * this.params[i]);
    }

    this.lastLoss = Number(avgLoss.toFixed(4));
    this.lossHistory.push(this.lastLoss);
    if (this.lossHistory.length > 120) this.lossHistory.shift();

    return this.lastLoss;
  }

  /**
   * Generates continuation text autoregressively from a prompt using temperature,
   * top-k sampling, and repetition penalty to avoid character loops.
   */
  generate(prompt: string, maxNewTokens = 140, temperature = 0.65, topK = 12): string {
    let ids = this.encode(prompt);
    if (ids.length === 0) ids = [0];

    const out: number[] = [];
    for (let step = 0; step < maxNewTokens; step++) {
      const ctx = ids.slice(-this.config.blockSize);
      const { logits } = this.forwardSequence(ctx);
      const lastOff = (ctx.length - 1) * this.config.vocabSize;
      const temp = Math.max(0.15, temperature);

      // Apply repetition penalty over the recent 18 generated tokens
      const recent = out.slice(-18);
      const counts = new Map<number, number>();
      for (const id of recent) {
        counts.set(id, (counts.get(id) ?? 0) + 1);
      }

      const pairs: Array<{ id: number; val: number }> = [];
      for (let v = 0; v < this.config.vocabSize; v++) {
        let logit = logits[lastOff + v];
        const freq = counts.get(v) ?? 0;
        if (freq > 0) {
          logit = logit > 0 ? logit / Math.pow(1.22, freq) : logit * Math.pow(1.22, freq);
        }
        pairs.push({ id: v, val: logit / temp });
      }
      pairs.sort((a, b) => b.val - a.val);
      const top = pairs.slice(0, Math.min(topK, pairs.length));

      const maxV = top[0].val;
      let sum = 0;
      const probs = top.map((p) => {
        const e = Math.exp(p.val - maxV);
        sum += e;
        return e;
      });

      let r = Math.random() * sum;
      let chosen = top[0].id;
      for (let i = 0; i < top.length; i++) {
        r -= probs[i];
        if (r <= 0) {
          chosen = top[i].id;
          break;
        }
      }

      ids.push(chosen);
      out.push(chosen);

      // Stop naturally if model emits a newline or starts a new turn
      const partial = this.decode(out);
      if (partial.includes('\n\n') || partial.includes('Вопрос:')) {
        return partial.replace(/(\n\n|Вопрос:).*$/s, '').trim();
      }
      if (prompt.endsWith('Ответ:') && partial.length > 18 && partial.endsWith('\n')) {
        return partial.trim();
      }
    }

    return this.decode(out).trim();
  }

  exportCheckpoint(): SerializedCheckpoint {
    return {
      version: 1,
      config: { ...this.config },
      vocab: [...this.vocab],
      weights: Array.from(this.params, (v) => Number(v.toFixed(4))),
      stepCount: this.stepCount,
      lastLoss: this.lastLoss,
      lossHistory: [...this.lossHistory],
    };
  }

  static fromCheckpoint(cp: SerializedCheckpoint): MiniTransformer {
    const model = new MiniTransformer(cp.vocab, cp.config);
    for (let i = 0; i < Math.min(model.params.length, cp.weights.length); i++) {
      model.params[i] = cp.weights[i];
    }
    model.stepCount = cp.stepCount;
    model.lastLoss = cp.lastLoss;
    model.lossHistory = [...(cp.lossHistory ?? [])];
    return model;
  }
}
