import { Model, ModelValidationStatus, Wllama } from '@wllama/wllama';

export interface CachedModelInfo {
  url: string;
  name: string;
  sizeMb: number;
  valid: boolean;
  modelRef: Model;
}

export interface HFSearchHit {
  repo: string;
  downloads: number;
  likes: number;
}

const WASM_ASSETS = {
  default: './wasm/wllama.wasm',
  'single-thread/wllama.wasm': './wasm/wllama.wasm',
  'multi-thread/wllama.wasm': './wasm/wllama.wasm',
};

class WllamaEngineService {
  private instance: Wllama | null = null;
  private loadedModelLabel: string | null = null;

  private getOrCreate(): Wllama {
    if (!this.instance) {
      this.instance = new Wllama(WASM_ASSETS, {
        allowOffline: true,
        parallelDownloads: 3,
        suppressNativeLog: true,
      });
    }
    return this.instance;
  }

  isLoaded(): boolean {
    return Boolean(this.instance?.isModelLoaded());
  }

  getLoadedModelName(): string | null {
    return this.isLoaded() ? this.loadedModelLabel : null;
  }

  async unload(): Promise<void> {
    if (this.instance?.isModelLoaded()) {
      await this.instance.exit();
    }
    this.loadedModelLabel = null;
  }

  async loadFromHuggingFace(
    repo: string,
    quant: string,
    label: string,
    contextSize: number,
    onProgress: (loadedBytes: number, totalBytes: number) => void,
  ): Promise<void> {
    const wllama = this.getOrCreate();
    if (wllama.isModelLoaded()) {
      await wllama.exit();
    }

    await wllama.loadModelFromHF(
      { repo, quant },
      {
        n_ctx: contextSize,
        useCache: true,
        progressCallback: ({ loaded, total }) => {
          onProgress(loaded, total);
        },
      },
    );
    this.loadedModelLabel = label;
  }

  async loadFromLocalFiles(files: File[], contextSize: number): Promise<string> {
    if (!files.length) throw new Error('Файл не выбран');
    const wllama = this.getOrCreate();
    if (wllama.isModelLoaded()) {
      await wllama.exit();
    }

    await wllama.loadModel(files, {
      n_ctx: contextSize,
    });
    const label = files[0].name.replace(/\.gguf$/i, '');
    this.loadedModelLabel = label;
    return label;
  }

  async listCachedModels(): Promise<CachedModelInfo[]> {
    const wllama = this.getOrCreate();
    try {
      const models = await wllama.modelManager.getModels({ includeInvalid: false });
      return models.map((m) => {
        const rawName = decodeURIComponent(m.url.split('/').pop() || m.url).replace(/\.gguf$/i, '');
        return {
          url: m.url,
          name: rawName,
          sizeMb: Math.max(1, Math.round(m.size / (1024 * 1024))),
          valid: m.validate() === ModelValidationStatus.VALID,
          modelRef: m,
        };
      });
    } catch {
      return [];
    }
  }

  async loadCachedModel(item: CachedModelInfo, contextSize: number): Promise<void> {
    const wllama = this.getOrCreate();
    if (wllama.isModelLoaded()) {
      await wllama.exit();
    }
    await wllama.loadModel(item.modelRef, {
      n_ctx: contextSize,
    });
    this.loadedModelLabel = item.name;
  }

  async deleteCachedModel(item: CachedModelInfo): Promise<void> {
    if (this.loadedModelLabel === item.name) {
      await this.unload();
    }
    await item.modelRef.remove();
  }

  async streamChat(
    messages: Array<{ role: 'system' | 'user' | 'assistant'; content: string }>,
    opts: { temperature: number; maxTokens: number; abortSignal?: AbortSignal },
    onDelta: (fullText: string, tokensPerSec: number) => void,
  ): Promise<{ fullText: string; tokensPerSec: number }> {
    const wllama = this.getOrCreate();
    if (!wllama.isModelLoaded()) {
      throw new Error('Модель GGUF не загружена');
    }

    const t0 = performance.now();
    let tokenCount = 0;
    let latestText = '';

    const stream = await wllama.createChatCompletion({
      messages,
      stream: true,
      temperature: opts.temperature,
      max_tokens: opts.maxTokens,
      abortSignal: opts.abortSignal,
    });

    for await (const chunk of stream) {
      const delta = chunk.choices?.[0]?.delta?.content ?? '';
      if (delta) {
        latestText += delta;
        tokenCount += 1;
        const elapsedSec = Math.max(0.05, (performance.now() - t0) / 1000);
        onDelta(latestText, Number((tokenCount / elapsedSec).toFixed(1)));
      }
    }

    const elapsedSec = Math.max(0.05, (performance.now() - t0) / 1000);
    return {
      fullText: latestText,
      tokensPerSec: Number((Math.max(1, tokenCount) / elapsedSec).toFixed(1)),
    };
  }
}

export const wllamaService = new WllamaEngineService();

export async function searchHuggingFaceGGUF(query: string): Promise<HFSearchHit[]> {
  const q = query.trim() || 'gemma gguf';
  const searchTerm = q.toLowerCase().includes('gguf') ? q : `${q} gguf`;
  const url = `https://huggingface.co/api/models?search=${encodeURIComponent(searchTerm)}&sort=downloads&direction=-1&limit=8`;
  const res = await fetch(url, { headers: { Accept: 'application/json' } });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  const data = (await res.json()) as Array<{ id: string; downloads?: number; likes?: number }>;
  return data.map((d) => ({
    repo: d.id,
    downloads: d.downloads ?? 0,
    likes: d.likes ?? 0,
  }));
}
