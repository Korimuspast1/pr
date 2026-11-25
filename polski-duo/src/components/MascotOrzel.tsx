import React from 'react';

interface MascotProps {
  mood?: 'happy' | 'cheering' | 'thinking' | 'oops' | 'ai';
  size?: number;
  className?: string;
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
}) => {
  const isCheering = mood === 'cheering';
  const isThinking = mood === 'thinking' || mood === 'ai';
  const isOops = mood === 'oops';

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

      {/* Body (Crimson Polish Coat + White Chest) */}
      <rect x="30" y="42" width="60" height="62" rx="30" fill="#DC143C" stroke="#A30D2B" strokeWidth="3" />
      <ellipse cx="60" cy="78" rx="20" ry="20" fill="#FFFFFF" />

      {/* Head (White Eagle Plumage) */}
      <circle cx="60" cy="46" r="28" fill="#FFFFFF" stroke="#E2E8F0" strokeWidth="3" />

      {/* Golden Polish Crown */}
      <path
        d="M44 21 L48 10 L55 18 L60 8 L65 18 L72 10 L76 21 Z"
        fill="#FFC800"
        stroke="#E5A900"
        strokeWidth="2.5"
        strokeLinejoin="round"
      />

      {/* Polish Red-White Scarf */}
      <path
        d="M35 64 Q60 74 85 64 L83 72 Q60 82 37 72 Z"
        fill="#FFFFFF"
        stroke="#CBD5E1"
        strokeWidth="2"
      />
      <path
        d="M36 68 Q60 77 84 68 L83 74 Q60 83 37 74 Z"
        fill="#DC143C"
      />
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
