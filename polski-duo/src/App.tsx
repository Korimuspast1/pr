import React, { useEffect, useState } from 'react';
import {
  assembleAdaptiveLesson,
  createInitialAIState,
  loadAIState,
  saveAIState,
} from './ai/localAiEngine';
import { generateAdaptiveExercise, generatePlacementQuestion } from './ai/polishGenerator';
import { GuidebookView } from './components/GuidebookView';
import { InfiniteAiStudio } from './components/InfiniteAiStudio';
import { LearningPathView } from './components/LearningPathView';
import { LessonConfig, LessonPlayer } from './components/LessonPlayer';
import { RightPanel } from './components/RightPanel';
import { NavTab, SidebarNav } from './components/SidebarNav';
import { StatsDashboard } from './components/StatsDashboard';
import { LearnerAIState, PathNode, SkillId } from './types/learning';

export const App: React.FC = () => {
  const [aiState, setAiState] = useState<LearnerAIState>(() => loadAIState());
  const [activeTab, setActiveTab] = useState<NavTab>('learn');
  const [activeLesson, setActiveLesson] = useState<LessonConfig | null>(null);

  useEffect(() => {
    saveAIState(aiState);
  }, [aiState]);

  const handleStartNodeLesson = (node: PathNode) => {
    const exercises = assembleAdaptiveLesson(aiState, {
      mode: 'path',
      skillFocus: node.skillFocus,
      targetTier: node.targetTier,
      count: 6,
      forceSpeaking: node.isSpeaking,
    });
    setActiveLesson({
      mode: 'path',
      title: node.title,
      nodeId: node.id,
      skillFocus: node.skillFocus,
      initialExercises: exercises,
      forceSpeaking: node.isSpeaking,
    });
  };

  const handleStartSmartLesson = () => {
    const exercises = assembleAdaptiveLesson(aiState, {
      mode: 'review',
      count: 7,
    });
    setActiveLesson({
      mode: 'review',
      title: 'Умный адаптивный урок ИИ',
      initialExercises: exercises,
    });
  };

  const handleStartPlacement = () => {
    const first = generatePlacementQuestion(3.2, []);
    setActiveLesson({
      mode: 'placement',
      title: 'ИИ-калибровка уровня польского',
      initialExercises: [first],
      isPlacement: true,
    });
  };

  const handleStartInfiniteStream = (
    skillFocus?: SkillId,
    customStartTier?: number,
    forceSpeaking = false,
  ) => {
    let baseState = aiState;
    if (customStartTier !== undefined) {
      baseState = { ...aiState, currentTier: customStartTier };
      setAiState(baseState);
    }
    const first = generateAdaptiveExercise(
      baseState,
      baseState.currentTier,
      skillFocus,
      [],
      forceSpeaking,
    );
    setActiveLesson({
      mode: 'infinite',
      title: forceSpeaking ? 'Бесконечный микрофон · ИИ-фонетист' : 'Бесконечный поток ИИ',
      skillFocus,
      initialExercises: [first],
      isInfinite: true,
      forceSpeaking,
    });
  };

  const handleResetProgress = () => {
    const fresh = createInitialAIState();
    setAiState(fresh);
    saveAIState(fresh);
  };

  return (
    <div className="min-h-screen bg-white text-[#3C3C3C] flex">
      <SidebarNav
        activeTab={activeTab}
        onSelectTab={setActiveTab}
        soundEnabled={aiState.soundEnabled}
        onToggleSound={() =>
          setAiState((prev) => ({ ...prev, soundEnabled: !prev.soundEnabled }))
        }
      />

      <main className="flex-1 min-w-0">
        {activeTab === 'learn' && (
          <LearningPathView
            state={aiState}
            onStartNodeLesson={handleStartNodeLesson}
            onStartPlacement={handleStartPlacement}
            onStartInfinite={() => setActiveTab('infinite')}
            onOpenGuide={() => setActiveTab('guide')}
          />
        )}

        {activeTab === 'infinite' && (
          <InfiniteAiStudio
            state={aiState}
            onStartInfiniteStream={handleStartInfiniteStream}
            onChangePhoneticsMode={(mode) =>
              setAiState((prev) => ({ ...prev, showPhoneticsOverride: mode }))
            }
          />
        )}

        {activeTab === 'stats' && (
          <StatsDashboard
            state={aiState}
            onStartReviewLesson={handleStartSmartLesson}
            onResetProgress={handleResetProgress}
          />
        )}

        {activeTab === 'guide' && <GuidebookView state={aiState} />}
      </main>

      <RightPanel
        state={aiState}
        onStartSmartLesson={handleStartSmartLesson}
        onOpenInfinite={() => setActiveTab('infinite')}
        onOpenStats={() => setActiveTab('stats')}
      />

      {activeLesson && (
        <LessonPlayer
          config={activeLesson}
          aiState={aiState}
          onUpdateState={setAiState}
          onExit={() => setActiveLesson(null)}
        />
      )}
    </div>
  );
};

export default App;
