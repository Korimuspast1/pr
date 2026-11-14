import { classNames } from '../lib/format';

interface StarsProps {
  rating: number;
  size?: number;
  className?: string;
  showValue?: boolean;
  count?: number;
}

export function Stars({ rating, size = 13, className, showValue = false, count }: StarsProps) {
  const pct = Math.max(0, Math.min(100, (rating / 5) * 100));
  return (
    <span
      className={classNames('stars', className)}
      role="img"
      aria-label={`Rated ${rating.toFixed(1)} out of 5`}
    >
      <span className="stars__track" style={{ height: size }}>
        <span className="stars__row stars__row--empty">
          {[0, 1, 2, 3, 4].map((i) => (
            <Star key={i} size={size} />
          ))}
        </span>
        <span className="stars__row stars__row--full" style={{ width: `${pct}%` }}>
          {[0, 1, 2, 3, 4].map((i) => (
            <Star key={i} size={size} />
          ))}
        </span>
      </span>
      {showValue && (
        <span className="stars__meta">
          {rating.toFixed(1)}
          {typeof count === 'number' && <span className="stars__count">({count})</span>}
        </span>
      )}
    </span>
  );
}

function Star({ size }: { size: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
      <path d="M10 1.6l2.47 5.2 5.53.74-4.07 3.83 1.03 5.53L10 14.13l-4.96 2.77 1.03-5.53L2 7.54l5.53-.74L10 1.6z" />
    </svg>
  );
}
