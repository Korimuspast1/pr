export const money = (value: number, currency = 'EUR') =>
  new Intl.NumberFormat('en-IE', {
    style: 'currency',
    currency,
    minimumFractionDigits: Number.isInteger(value) ? 0 : 2,
    maximumFractionDigits: 2,
  }).format(value);

export const compactNumber = (value: number) =>
  new Intl.NumberFormat('en-IE', { notation: 'compact', maximumFractionDigits: 1 }).format(value);

export const formatReviewDate = (iso: string) =>
  new Date(iso).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });

export const classNames = (...values: Array<string | false | null | undefined>) =>
  values.filter(Boolean).join(' ');
