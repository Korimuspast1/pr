import { classNames } from '../lib/format';

interface Props {
  value: number;
  onChange: (next: number) => void;
  min?: number;
  max?: number;
  size?: 'sm' | 'md';
  label?: string;
}

export function QuantityStepper({ value, onChange, min = 1, max = 9, size = 'md', label = 'Quantity' }: Props) {
  return (
    <div className={classNames('stepper', size === 'sm' && 'stepper--sm')} role="group" aria-label={label}>
      <button
        type="button"
        className="stepper__btn"
        onClick={() => onChange(value - 1)}
        disabled={value <= min}
        aria-label="Decrease quantity"
      >
        <svg width="11" height="11" viewBox="0 0 12 12" aria-hidden="true">
          <path d="M1 6h10" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
        </svg>
      </button>
      <span className="stepper__value" aria-live="polite">
        {value}
      </span>
      <button
        type="button"
        className="stepper__btn"
        onClick={() => onChange(value + 1)}
        disabled={value >= max}
        aria-label="Increase quantity"
      >
        <svg width="11" height="11" viewBox="0 0 12 12" aria-hidden="true">
          <path d="M1 6h10M6 1v10" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
        </svg>
      </button>
    </div>
  );
}
