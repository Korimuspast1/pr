import React from 'react';
import { MascotOutfit } from '../types/learning';

interface MascotProps {
  mood?: 'happy' | 'cheering' | 'thinking' | 'oops' | 'ai';
  size?: number;
  className?: string;
  outfit?: MascotOutfit;
}

export const PolishFlagBadge: React.FC<{ className?: string }> = ({ className = 'w-8 h-6' }) => (
  <svg viewBox="0 0 32 24" className={`${className} rounded-md border border-[#E5E5E5] shadow-xs shrink-0`}>
    <rect width="32" height="12" fill="#FFFFFF" />
    <rect y="12" width="32" height="12" fill="#DC143C" />
  </svg>
);

export const MascotOrzel: React.FC<MascotProps> = ({
  mood = 'happy',
  size = 96,
  className = '',
  outfit = 'classic',
}) => {
  const isCheering = mood === 'cheering';
  const isThinking = mood === 'thinking' || mood === 'ai';
  const isOops = mood === 'oops';

  const coatColor =
    outfit === 'hussar'
      ? '#991B1B'
      : outfit === 'coffee'
        ? '#6F4E37'
        : outfit === 'student'
          ? '#1899D6'
          : outfit === 'king'
            ? '#7E22CE'
            : '#DC143C';

  const coatStroke =
    outfit === 'hussar'
      ? '#7F1D1D'
      : outfit === 'coffee'
        ? '#4B3322'
        : outfit === 'student'
          ? '#0F6B99'
          : outfit === 'king'
            ? '#581C87'
            : '#A30D2B';

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 120 120"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={className}
      aria-label="Орлик Белик — маскот PolskiOrzeł"
    >
      {/* Winged Hussar Back Feathers */}
      {outfit === 'hussar' && (
        <g>
          <path d="M28 52 C10 18, 20 8, 36 30" stroke="#94A3B8" strokeWidth="5" strokeLinecap="round" />
          <path d="M92 52 C110 18, 100 8, 84 30" stroke="#94A3B8" strokeWidth="5" strokeLinecap="round" />
          <path d="M20 26 L31 24 M18 34 L31 32 M100 26 L89 24 M102 34 L89 32" stroke="#FFFFFF" strokeWidth="2.5" strokeLinecap="round" />
        </g>
      )}

      {/* Soft shadow */}
      <ellipse cx="60" cy="111" rx="34" ry="6" fill="#000000" fillOpacity="0.08" />

      {/* Left & Right Wings */}
      <path
        d={
          isCheering
            ? 'M26 62 C8 40, 12 24, 28 32 C34 36, 36 52, 34 66 Z'
            : 'M28 58 C14 60, 14 84, 28 88 C34 89, 36 74, 34 62 Z'
        }
        fill="#F1F5F9"
        stroke="#CBD5E1"
        strokeWidth="3"
        strokeLinejoin="round"
      />
      <path
        d={
          isCheering
            ? 'M94 62 C112 40, 108 24, 92 32 C86 36, 84 52, 86 66 Z'
            : 'M92 58 C106 60, 106 84, 92 88 C86 89, 84 74, 86 66 Z'
        }
        fill="#F1F5F9"
        stroke="#CBD5E1"
        strokeWidth="3"
        strokeLinejoin="round"
      />

      {/* Body */}
      <rect x="30" y="42" width="60" height="62" rx="30" fill={coatColor} stroke={coatStroke} strokeWidth="3" />
      <ellipse cx="60" cy="78" rx="20" ry="20" fill="#FFFFFF" />

      {/* King Ermine Spots */}
      {outfit === 'king' && (
        <g fill="#1E293B">
          <circle cx="48" cy="78" r="1.8" />
          <circle cx="72" cy="78" r="1.8" />
          <circle cx="60" cy="88" r="1.8" />
        </g>
      )}

      {/* Head (White Eagle Plumage) */}
      <circle cx="60" cy="46" r="28" fill="#FFFFFF" stroke="#E2E8F0" strokeWidth="3" />

      {/* Headwear per outfit */}
      {outfit === 'student' ? (
        <g>
          <polygon points="60,8 90,18 60,26 30,18" fill="#1E293B" stroke="#0F172A" strokeWidth="2" />
          <rect x="44" y="19" width="32" height="6" rx="2" fill="#334155" />
          <line x1="82" y1="19" x2="86" y2="30" stroke="#FFC800" strokeWidth="2.5" strokeLinecap="round" />
        </g>
      ) : outfit === 'coffee' ? (
        <g>
          <path d="M40 22 C40 12, 80 12, 80 22 Z" fill="#6F4E37" stroke="#4B3322" strokeWidth="2" />
          <rect x="36" y="20" width="48" height="5" rx="2.5" fill="#4B3322" />
        </g>
      ) : outfit === 'hussar' ? (
        <g>
          <path d="M40 24 C42 10, 78 10, 80 24 Z" fill="#94A3B8" stroke="#475569" strokeWidth="2.2" />
          <path d="M58 6 L62 6 L63 18 L57 18 Z" fill="#DC143C" />
        </g>
      ) : (
        /* Golden Polish Crown (Classic & King with gems) */
        <g>
          <path
            d="M44 21 L48 10 L55 18 L60 8 L65 18 L72 10 L76 21 Z"
            fill="#FFC800"
            stroke="#E5A900"
            strokeWidth="2.5"
            strokeLinejoin="round"
          />
          {outfit === 'king' && (
            <>
              <circle cx="60" cy="15" r="2.2" fill="#DC143C" />
              <circle cx="51" cy="17" r="1.7" fill="#1CB0F6" />
              <circle cx="69" cy="17" r="1.7" fill="#58CC02" />
            </>
          )}
        </g>
      )}

      {/* Polish Red-White Scarf */}
      <path
        d="M35 64 Q60 74 85 64 L83 72 Q60 82 37 72 Z"
        fill="#FFFFFF"
        stroke="#CBD5E1"
        strokeWidth="2"
      />
      <path d="M36 68 Q60 77 84 68 L83 74 Q60 83 37 74 Z" fill="#DC143C" />
      <rect x="66" y="70" width="9" height="17" rx="4" fill="#DC143C" stroke="#A30D2B" strokeWidth="2" />

      {/* Eyes */}
      <circle cx="48" cy="45" r="8.5" fill="#FFFFFF" stroke="#E2E8F0" strokeWidth="2" />
      <circle cx="72" cy="45" r="8.5" fill="#FFFFFF" stroke="#E2E8F0" strokeWidth="2" />

      {isOops ? (
        <>
          <path d="M44 43 L52 47 M52 43 L44 47" stroke="#3C3C3C" strokeWidth="2.6" strokeLinecap="round" />
          <path d="M68 43 L76 47 M76 43 L68 47" stroke="#3C3C3C" strokeWidth="2.6" strokeLinecap="round" />
        </>
      ) : (
        <>
          <circle cx={isThinking ? 50 : 49} cy={isThinking ? 44 : 45} r="4.5" fill="#2B2D42" />
          <circle cx={isThinking ? 74 : 71} cy={isThinking ? 44 : 45} r="4.5" fill="#2B2D42" />
          <circle cx="47.5" cy="43" r="1.6" fill="#FFFFFF" />
          <circle cx="69.5" cy="43" r="1.6" fill="#FFFFFF" />
        </>
      )}

      {/* Student Glasses */}
      {outfit === 'student' && (
        <g stroke="#334155" strokeWidth="2" fill="none">
          <circle cx="48" cy="45" r="10" />
          <circle cx="72" cy="45" r="10" />
          <line x1="58" y1="45" x2="62" y2="45" />
        </g>
      )}

      {/* Eyebrows */}
      {isThinking && (
        <>
          <path d="M41 34 Q48 31 54 34" stroke="#64748B" strokeWidth="2.5" strokeLinecap="round" />
          <path d="M66 33 Q72 29 79 33" stroke="#64748B" strokeWidth="2.5" strokeLinecap="round" />
        </>
      )}

      {/* Golden Eagle Beak */}
      <path
        d="M54 50 Q60 47 66 50 L60 61 Z"
        fill="#FF9600"
        stroke="#CC7800"
        strokeWidth="2.2"
        strokeLinejoin="round"
      />

      {/* Coffee Cup prop for coffee outfit */}
      {outfit === 'coffee' && (
        <g>
          <rect x="82" y="76" width="16" height="14" rx="3" fill="#FFFFFF" stroke="#4B3322" strokeWidth="2" />
          <path d="M98 79 C103 79, 103 87, 98 87" stroke="#4B3322" strokeWidth="2" fill="none" />
          <path d="M87 72 Q89 68 87 65 M93 72 Q95 68 93 65" stroke="#94A3B8" strokeWidth="1.6" strokeLinecap="round" />
        </g>
      )}

      {/* AI Sparkle / Monocle for AI mode */}
      {mood === 'ai' && (
        <g>
          <circle cx="72" cy="45" r="11" stroke="#CE82FF" strokeWidth="2.5" fill="#CE82FF" fillOpacity="0.12" />
          <path
            d="M95 18 L97 24 L103 26 L97 28 L95 34 L93 28 L87 26 L93 24 Z"
            fill="#CE82FF"
          />
        </g>
      )}

      {/* Feet */}
      <path d="M44 103 L40 110 M46 103 L46 111 M48 103 L52 110" stroke="#FF9600" strokeWidth="3" strokeLinecap="round" />
      <path d="M72 103 L68 110 M74 103 L74 111 M76 103 L80 110" stroke="#FF9600" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
};
