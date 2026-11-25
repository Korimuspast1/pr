export type AppTab = 'chat' | 'models' | 'enhancer' | 'trainer';

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  thinking?: string;
  ragTitles?: string[];
  usedAdapterCount?: number;
  modelName: string;
  tokensPerSec?: number;
  feedback?: 'up' | 'down';
  enhancedMode?: boolean;
  timestamp: number;
}

export interface ModelCatalogItem {
  id: string;
  name: string;
  family: string;
  sizeLabel: string;
  repo: string;
  quant: string;
  description: string;
  isGemma?: boolean;
  recommended?: boolean;
}

export interface KnowledgeNote {
  id: string;
  title: string;
  content: string;
  createdAt: number;
}

export interface FeedbackRule {
  id: string;
  prompt: string;
  badReply?: string;
  idealReply: string;
  createdAt: number;
}

export interface PersonaPreset {
  id: string;
  name: string;
  description: string;
  systemPrompt: string;
}

export interface EnhancerConfig {
  deepThink: boolean;
  useRag: boolean;
  useAdapter: boolean;
  personaId: string;
  systemPrompt: string;
  temperature: number;
  topP: number;
  maxTokens: number;
  contextSize: number;
}

export interface BenchmarkRun {
  prompt: string;
  baseReply: string;
  enhancedReply: string;
  enhancedThinking?: string;
  baseMs: number;
  enhancedMs: number;
  baseScore: number;
  enhancedScore: number;
}
