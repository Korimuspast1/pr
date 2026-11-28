import { useEffect, useState } from 'react';

export function BackToTop() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const onScroll = () => setVisible(window.scrollY > 1400);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  return (
    <button
      type="button"
      className={`back-to-top ${visible ? 'is-visible' : ''}`}
      onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
      aria-label="Back to top"
      tabIndex={visible ? 0 : -1}
    >
      <svg width="13" height="13" viewBox="0 0 16 16" aria-hidden="true">
        <path d="M8 13V3M4 7l4-4 4 4" stroke="currentColor" strokeWidth="1.2" fill="none" strokeLinecap="round" />
      </svg>
      <span>Top</span>
    </button>
  );
}
