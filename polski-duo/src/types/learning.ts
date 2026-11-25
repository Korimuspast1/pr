export type SkillId =
  | 'phonetics'
  | 'vocabulary_basic'
  | 'everyday'
  | 'false_friends'
  | 'verbs'
  | 'cases'
  | 'syntax'
  | 'idioms_advanced';

export type CefrLevel = 'A0' | 'A1' | 'A1+' | 'A2' | 'B1' | 'B2' | 'C1';

export type PolishCase =
  | 'Mianownik'   // Именительный (kto? co?)
  | 'Dopełniacz'  // Родительный (kogo? czego?)
  | 'Biernik'     // Винительный (kogo? co?)
  | 'Narzędnik'   // Творительный (z kim? z czym?)
  | 'Miejscownik'; // Предложный (o kim? o czym? w czym?)

export type Gender = 'm_anim' | 'm_inan' | 'f' | 'n';

export interface LexiconItem {
  id: string;
  pl: string;
  ru: string;
  phoneticRu: string;
  category: string;
  skill: SkillId;
  tier: number; // 1..10
  partOfSpeech: 'noun' | 'verb' | 'adj' | 'phrase' | 'adv' | 'num';
  gender?: Gender;
  isFalseFriend?: boolean;
  falseFriendTrapRu?: string;
  falseFriendNote?: string;
  declension?: {
    gen?: string; // Dopełniacz
    acc?: string; // Biernik
    ins?: string; // Narzędnik
    loc?: string; // Miejscownik
  };
  adjForms?: {
    m: string;
    m_acc_anim: string;
    f: string;
    f_acc: string;
    n: string;
    ins_m: string;
    ins_f: string;
  };
  examplePl: string;
  exampleRu: string;
}

export interface VerbItem {
  id: string;
  infinitive: string;
  ruInfinitive: string;
  phoneticRu: string;
  tier: number;
  takesCase: PolishCase | 'none';
  prep?: string;
  conj: {
    ja: string;
    ty: string;
    on: string; // on/ona/ono
    my: string;
    wy: string;
    oni: string;
  };
  ruConj: {
    ja: string;
    ty: string;
    on: string;
    my: string;
    wy: string;
    oni: string;
  };
  pastM: string; // ja (m)
  pastF: string; // ja (f)
  pastRu: string;
}

export type ExerciseType =
  | 'multiple_choice'
  | 'word_tiles'
  | 'type_translation'
  | 'cloze_grammar'
  | 'match_pairs'
  | 'listening'
  | 'speaking';

export interface MatchPair {
  id: string;
  pl: string;
  ru: string;
  phoneticRu?: string;
}

export interface Exercise {
  id: string;
  type: ExerciseType;
  skill: SkillId;
  tier: number;
  promptRu: string;
  questionPl?: string;
  questionRu?: string;
  phoneticHint?: string;
  audioTextPl?: string;
  options?: string[];
  correctAnswer: string;
  acceptedAnswers?: string[];
  tiles?: string[];
  clozeBefore?: string;
  clozeAfter?: string;
  pairs?: MatchPair[];
  itemIds: string[];
  aiNote: string;
  grammarTag?: string;
  isFalseFriendTrap?: boolean;
}

export interface ItemMemory {
  id: string;
  strength: number; // 0..1
  attempts: number;
  mistakes: number;
  streak: number;
  lastSeenAt: number;
  nextReviewAt: number;
  avgResponseMs: number;
}

export type ErrorKind =
  | 'diacritic_slip'
  | 'false_friend_trap'
  | 'case_error'
  | 'conjugation_error'
  | 'word_order'
  | 'vocabulary_miss';

export type MascotOutfit = 'classic' | 'hussar' | 'coffee' | 'student' | 'king';
export type AppThemeId = 'poland' | 'krakow' | 'baltic';

export interface SessionLog {
  id: string;
  timestamp: number;
  mode: 'path' | 'infinite' | 'placement' | 'review' | 'sprint' | 'roleplay' | 'cards';
  unitTitle: string;
  xpEarned: number;
  accuracy: number;
  tierBefore: number;
  tierAfter: number;
  questionsCount: number;
  aiSummary: string;
}

export interface AiThought {
  id: string;
  timestamp: number;
  message: string;
  kind: 'boost' | 'support' | 'warning' | 'insight';
}

export interface LearnerAIState {
  xp: number;
  gems: number;
  streakDays: number;
  hearts: number;
  maxHearts: number;
  dailyXp: number;
  dailyGoalXp: number;
  lessonsCompleted: number;
  totalExercises: number;
  totalCorrect: number;
  currentTier: number; // 1.0 .. 12.0 continuous adaptive difficulty
  skillMastery: Record<SkillId, number>; // 0..1
  skillAttempts: Record<SkillId, number>;
  errorCounts: Record<ErrorKind, number>;
  itemMemory: Record<string, ItemMemory>;
  completedNodes: Record<string, number>; // nodeId -> crowns (0..3)
  sessions: SessionLog[];
  aiThoughts: AiThought[];
  placementDone: boolean;
  soundEnabled: boolean;
  showPhoneticsOverride: 'auto' | 'always' | 'off';
  equippedOutfit: MascotOutfit;
  unlockedOutfits: MascotOutfit[];
  equippedTheme: AppThemeId;
  unlockedThemes: AppThemeId[];
  xpBoostUntil: number;
  sprintBestScore: number;
  reminderEnabled: boolean;
  reminderTime: string;
}

export interface PathNode {
  id: string;
  sectionId: string;
  title: string;
  subtitle: string;
  skillFocus: SkillId;
  targetTier: number;
  icon: string;
  isBoss?: boolean;
  isInfinite?: boolean;
  isSpeaking?: boolean;
}

export interface PathSection {
  id: string;
  title: string;
  subtitle: string;
  cefr: CefrLevel;
  color: string;
  shadowColor: string;
  accentBg: string;
  nodes: PathNode[];
}

export interface AnswerEvaluation {
  isCorrect: boolean;
  isMinorDiacriticSlip: boolean;
  errorKind?: ErrorKind;
  feedbackTitle: string;
  feedbackExplanation: string;
  correctDisplay: string;
}
