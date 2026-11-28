import { useEffect, useRef, useState, type ElementType, type ReactNode } from 'react';
import { classNames } from '../lib/format';

interface RevealProps {
  children: ReactNode;
  as?: ElementType;
  delay?: 0 | 1 | 2 | 3 | 4;
  className?: string;
  threshold?: number;
}

export function Reveal({ children, as: Tag = 'div', delay = 0, className, threshold = 0.12 }: RevealProps) {
  const ref = useRef<HTMLElement | null>(null);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const node = ref.current;
    if (!node) return;
    if (typeof IntersectionObserver === 'undefined') {
      setVisible(true);
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            setVisible(true);
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold, rootMargin: '0px 0px -8% 0px' },
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, [threshold]);

  return (
    <Tag
      ref={ref as never}
      className={classNames('reveal', delay > 0 && `reveal-d${delay}`, visible && 'is-visible', className)}
    >
      {children}
    </Tag>
  );
}
