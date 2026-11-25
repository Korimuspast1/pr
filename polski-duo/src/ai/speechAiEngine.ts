export interface WordSpeechAnalysis {
  targetWord: string;
  spokenWord: string | null;
  score: number; // 0..100
  status: 'exact' | 'accent' | 'missed';
  tip?: string;
}

export interface SpeechAiReport {
  bestTranscript: string;
  overallScore: number; // 0..100
  pronunciationScore: number; // 0..100
  fluencyScore: number; // 0..100
  acousticConfidence: number; // 0..100
  verdict: 'excellent' | 'good' | 'almost' | 'retry';
  headlineRu: string;
  summaryRu: string;
  coachingTips: string[];
  words: WordSpeechAnalysis[];
  isPass: boolean;
}

const POLISH_NUMBER_WORDS: Record<string, string> = {
  '0': 'zero',
  '1': 'jeden',
  '2': 'dwa',
  '3': 'trzy',
  '4': 'cztery',
  '5': 'pięć',
  '6': 'sześć',
  '7': 'siedem',
  '8': 'osiem',
  '9': 'dziewięć',
  '10': 'dziesięć',
};

function cleanPolishText(raw: string): string {
  return raw
    .toLowerCase()
    .replace(/[.,!?;:"'«»()„”—–-]/g, ' ')
    .split(/\s+/)
    .filter(Boolean)
    .map((w) => POLISH_NUMBER_WORDS[w] ?? w)
    .join(' ');
}

/**
 * Polish Grapheme-to-Phoneme (G2P) engine modeling real Polish phonology:
 * digraphs, palatalization (si/zi/ci/dzi/ni), nasal vowels (ą/ę),
 * word-final denasalization of -ę, and word-final obstruent devoicing.
 */
export function polishToPhonetic(word: string, loose = false): string {
  let w = word.toLowerCase().trim();
  if (!w) return '';

  // Word-final -ę naturally denasalizes to -e in colloquial Polish
  if (w.endsWith('ę')) {
    w = w.slice(0, -1) + 'e';
  }

  // Multi-character clusters first
  w = w
    .replace(/dzi(?=[aeiouyąęó])/g, 'dʑ')
    .replace(/dzi/g, 'dʑi')
    .replace(/dź/g, 'dʑ')
    .replace(/dż/g, 'dʐ')
    .replace(/rz/g, 'ʐ')
    .replace(/sz/g, 'ʂ')
    .replace(/cz/g, 'tʂ')
    .replace(/ch/g, 'x')
    .replace(/si(?=[aeiouyąęó])/g, 'ɕ')
    .replace(/si/g, 'ɕi')
    .replace(/zi(?=[aeiouyąęó])/g, 'ʑ')
    .replace(/zi/g, 'ʑi')
    .replace(/ci(?=[aeiouyąęó])/g, 'tɕ')
    .replace(/ci/g, 'tɕi')
    .replace(/ni(?=[aeiouyąęó])/g, 'ɲ')
    .replace(/ni/g, 'ɲi')
    .replace(/dz/g, 'dz');

  // Single-character Polish phonemes
  w = w
    .replace(/ó/g, 'u')
    .replace(/h/g, 'x')
    .replace(/ż/g, 'ʐ')
    .replace(/ś/g, 'ɕ')
    .replace(/ź/g, 'ʑ')
    .replace(/ć/g, 'tɕ')
    .replace(/ń/g, 'ɲ')
    .replace(/c/g, 'ts')
    .replace(/j/g, 'j')
    .replace(/ł/g, loose ? 'l' : 'w')
    .replace(/w/g, loose ? 'v' : 'v')
    .replace(/ą/g, loose ? 'on' : 'ɔ̃')
    .replace(/ę/g, loose ? 'en' : 'ɛ̃');

  // Word-final obstruent devoicing in Polish (chleb -> chlep, raz -> ras, ząb -> zomp)
  w = w
    .replace(/b$/g, 'p')
    .replace(/d$/g, 't')
    .replace(/g$/g, 'k')
    .replace(/v$/g, 'f')
    .replace(/z$/g, 's')
    .replace(/ʐ$/g, 'ʂ')
    .replace(/ʑ$/g, 'ɕ')
    .replace(/dz$/g, 'ts');

  if (loose) {
    // Collapse soft/hard sibilants slightly to tolerate mild Slavic accent
    w = w
      .replace(/ʐ/g, 'ʂ')
      .replace(/ʑ/g, 'ɕ')
      .replace(/tɕ/g, 'tʂ')
      .replace(/ɔ̃/g, 'on')
      .replace(/ɛ̃/g, 'en')
      .replace(/y/g, 'i')
      .replace(/v/g, 'f');
  }

  return w;
}

function levenshtein(a: string, b: string): number {
  const m = a.length;
  const n = b.length;
  if (m === 0) return n;
  if (n === 0) return m;
  const dp: number[][] = Array.from({ length: m + 1 }, () => Array(n + 1).fill(0));
  for (let i = 0; i <= m; i++) dp[i][0] = i;
  for (let j = 0; j <= n; j++) dp[0][j] = j;
  for (let i = 1; i <= m; i++) {
    for (let j = 1; j <= n; j++) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      dp[i][j] = Math.min(dp[i - 1][j] + 1, dp[i][j - 1] + 1, dp[i - 1][j - 1] + cost);
    }
  }
  return dp[m][n];
}

function wordSimilarity(targetWord: string, spokenWord: string): {
  score: number;
  tip?: string;
} {
  const tClean = targetWord.toLowerCase();
  const sClean = spokenWord.toLowerCase();
  if (tClean === sClean) {
    return { score: 100 };
  }

  const tPh = polishToPhonetic(tClean, false);
  const sPh = polishToPhonetic(sClean, false);
  if (tPh === sPh) {
    return { score: 97 };
  }

  const strictDist = levenshtein(tPh, sPh);
  const strictMax = Math.max(tPh.length, sPh.length, 1);
  const strictSim = Math.max(0, 1 - strictDist / strictMax);

  const tLoose = polishToPhonetic(tClean, true);
  const sLoose = polishToPhonetic(sClean, true);
  const looseDist = levenshtein(tLoose, sLoose);
  const looseMax = Math.max(tLoose.length, sLoose.length, 1);
  const looseSim = Math.max(0, 1 - looseDist / looseMax);

  // Blend strict Polish phonology (70%) + accent-tolerant phonology (30%)
  const blended = Math.round((strictSim * 0.7 + looseSim * 0.3) * 100);

  let tip: string | undefined;
  if (blended < 92) {
    if (tClean.includes('ł') && !sClean.includes('ł')) {
      tip = 'Польское «ł» звучит как краткое [у/w], а не твёрдое русское «л».';
    } else if (/[ąę]/.test(tClean)) {
      tip = 'Обратите внимание на носовые «ą / ę» (в конце слова «ę» звучит мягко, почти как «е»).';
    } else if (/(rz|sz|cz|ż)/.test(tClean)) {
      tip = 'Шипящие «sz / cz / rz / ż» в польском всегда твёрдые.';
    } else if (/[śźśćń]|(si|ci|zi|ni)/.test(tClean)) {
      tip = 'Звуки «ś / ć / ź / ń» произносятся очень мягко, почти с лёгким свистом.';
    }
  }

  return { score: Math.min(100, blended), tip };
}

function evaluateSingleHypothesis(
  targetPhrase: string,
  spokenPhrase: string,
  rawConfidence: number,
): SpeechAiReport {
  const targetWords = cleanPolishText(targetPhrase).split(' ').filter(Boolean);
  const spokenWords = cleanPolishText(spokenPhrase).split(' ').filter(Boolean);

  if (targetWords.length === 0 || spokenWords.length === 0) {
    return {
      bestTranscript: spokenPhrase,
      overallScore: 0,
      pronunciationScore: 0,
      fluencyScore: 0,
      acousticConfidence: Math.round(rawConfidence * 100),
      verdict: 'retry',
      headlineRu: 'Речь не распознана',
      summaryRu: 'ИИ не услышал польских слов. Нажмите на микрофон и произнесите фразу чётко.',
      coachingTips: ['Послушайте образец через динамик и повторите в спокойном темпе.'],
      words: targetWords.map((w) => ({
        targetWord: w,
        spokenWord: null,
        score: 0,
        status: 'missed',
      })),
      isPass: false,
    };
  }

  // Greedy monotonic best-match alignment for spoken words onto target words
  const usedSpoken = new Set<number>();
  const words: WordSpeechAnalysis[] = targetWords.map((tw) => {
    let bestIdx = -1;
    let bestScore = -1;
    let bestTip: string | undefined;

    for (let j = 0; j < spokenWords.length; j++) {
      if (usedSpoken.has(j)) continue;
      const res = wordSimilarity(tw, spokenWords[j]);
      if (res.score > bestScore) {
        bestScore = res.score;
        bestIdx = j;
        bestTip = res.tip;
      }
    }

    if (bestIdx >= 0 && bestScore >= 42) {
      usedSpoken.add(bestIdx);
      const status: WordSpeechAnalysis['status'] =
        bestScore >= 85 ? 'exact' : bestScore >= 60 ? 'accent' : 'missed';
      return {
        targetWord: tw,
        spokenWord: spokenWords[bestIdx],
        score: bestScore,
        status,
        tip: bestTip,
      };
    }

    return {
      targetWord: tw,
      spokenWord: null,
      score: Math.max(0, bestScore),
      status: 'missed',
      tip: 'Слово не прозвучало или исказилось — попробуйте выделить его отчётливее.',
    };
  });

  const avgWordScore = words.reduce((acc, w) => acc + w.score, 0) / words.length;
  const matchedWords = words.filter((w) => w.status !== 'missed').length;
  const completeness = matchedWords / words.length;

  // Penalize large extra noise words gently
  const extraCount = Math.max(0, spokenWords.length - targetWords.length);
  const extraPenalty = Math.min(18, extraCount * 6);

  const acousticPct = rawConfidence > 0 ? Math.round(rawConfidence * 100) : Math.round(avgWordScore);
  const pronunciationScore = Math.max(0, Math.min(100, Math.round(avgWordScore - extraPenalty * 0.4)));
  const fluencyScore = Math.max(
    0,
    Math.min(100, Math.round(completeness * 72 + (acousticPct / 100) * 28 - extraPenalty * 0.6)),
  );
  const overallScore = Math.max(
    0,
    Math.min(100, Math.round(pronunciationScore * 0.68 + fluencyScore * 0.32)),
  );

  // Collect specific Polish phonetics coaching tips
  const tipSet = new Set<string>();
  for (const w of words) {
    if (w.tip && w.score < 90) {
      tipSet.add(w.tip);
    }
  }

  let verdict: SpeechAiReport['verdict'];
  let headlineRu: string;
  let summaryRu: string;

  if (overallScore >= 85) {
    verdict = 'excellent';
    headlineRu = 'Отличное произношение! Как у носителя';
    summaryRu = `ИИ оценил точность речи на ${overallScore}%. Польские звуки и интонация считаны чисто.`;
  } else if (overallScore >= 65) {
    verdict = 'good';
    headlineRu = 'Хорошо! Поляк вас легко поймёт';
    summaryRu = `Точность ${overallScore}% — фраза распознана уверенно, есть небольшой акцент в отдельных звуках.`;
  } else if (overallScore >= 46) {
    verdict = 'almost';
    headlineRu = 'Почти получилось — можно чётче';
    summaryRu = `ИИ понял часть фразы (${overallScore}%). Обратите внимание на выделенные слова и попробуйте ещё раз.`;
  } else {
    verdict = 'retry';
    headlineRu = 'Пока слабо похоже на польскую фразу';
    summaryRu = `Совпадение ${overallScore}%. Прослушайте образец ещё раз (или на 0.65x) и повторите в микрофон.`;
  }

  if (tipSet.size === 0 && overallScore >= 85) {
    tipSet.add('Темп, ударение на предпоследний слог и польская артикуляция — в норме.');
  } else if (tipSet.size === 0 && overallScore < 85) {
    tipSet.add('В польском языке ударение почти всегда падает на предпоследний слог.');
  }

  return {
    bestTranscript: spokenPhrase,
    overallScore,
    pronunciationScore,
    fluencyScore,
    acousticConfidence: acousticPct,
    verdict,
    headlineRu,
    summaryRu,
    coachingTips: Array.from(tipSet).slice(0, 3),
    words,
    isPass: overallScore >= 62,
  };
}

/**
 * Evaluates N-best speech recognition hypotheses with acoustic confidences
 * and selects the strongest phonetic alignment for Polish.
 */
export function analyzePolishSpeech(
  targetPhrase: string,
  hypotheses: string[],
  confidences: number[] = [],
): SpeechAiReport {
  const valid = hypotheses.filter((h) => h && h.trim().length > 0);
  if (valid.length === 0) {
    return evaluateSingleHypothesis(targetPhrase, '', 0);
  }

  let bestReport: SpeechAiReport | null = null;
  for (let i = 0; i < valid.length; i++) {
    const conf = confidences[i] ?? Math.max(0.55, 0.9 - i * 0.08);
    const report = evaluateSingleHypothesis(targetPhrase, valid[i], conf);
    if (!bestReport || report.overallScore > bestReport.overallScore) {
      bestReport = report;
    }
  }

  return bestReport!;
}
