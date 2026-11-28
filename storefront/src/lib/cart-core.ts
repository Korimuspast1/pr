import { useEffect, useMemo, useState } from 'react';

export interface CartLine {
  key: string;
  productId: string;
  slug: string;
  variantId: string;
  name: string;
  variantLabel: string;
  finish: string;
  image: string;
  unitPrice: number;
  qty: number;
  monogram?: string;
}

export interface CartTotals {
  subtotal: number;
  discount: number;
  shipping: number;
  tax: number;
  total: number;
  itemCount: number;
}

const STORAGE_KEY = 'visa-storefront-cart-v1';
const FREE_SHIPPING_THRESHOLD = 150;
const TAX_RATE = 0.081;
const PROMOS: Record<string, number> = { STUDIO15: 0.15, WELCOME10: 0.1 };

function readCart(): CartLine[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(
      (line): line is CartLine =>
        Boolean(line) &&
        typeof line.key === 'string' &&
        typeof line.productId === 'string' &&
        typeof line.qty === 'number' &&
        line.qty > 0,
    );
  } catch {
    return [];
  }
}

export function useCart() {
  const [lines, setLines] = useState<CartLine[]>(readCart);
  const [promo, setPromo] = useState<string | null>(null);
  const [lastAdded, setLastAdded] = useState<string | null>(null);

  useEffect(() => {
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(lines));
    } catch {
      /* storage may be disabled; the session still works in memory */
    }
  }, [lines]);

  const add = (line: Omit<CartLine, 'key' | 'qty'> & { qty?: number }) => {
    const key = `${line.productId}::${line.variantId}::${(line.monogram ?? '').trim().toUpperCase()}`;
    const qty = Math.max(1, line.qty ?? 1);
    setLines((current) => {
      const existing = current.find((item) => item.key === key);
      if (existing) {
        return current.map((item) =>
          item.key === key ? { ...item, qty: Math.min(9, item.qty + qty) } : item,
        );
      }
      return [...current, { ...line, key, qty: Math.min(9, qty) }];
    });
    setLastAdded(key);
  };

  const setQty = (key: string, qty: number) => {
    setLines((current) =>
      qty <= 0
        ? current.filter((item) => item.key !== key)
        : current.map((item) => (item.key === key ? { ...item, qty: Math.min(9, qty) } : item)),
    );
  };

  const remove = (key: string) => setLines((current) => current.filter((item) => item.key !== key));

  const clear = () => {
    setLines([]);
    setPromo(null);
  };

  const applyPromo = (code: string) => {
    const normalised = code.trim().toUpperCase();
    if (PROMOS[normalised]) {
      setPromo(normalised);
      return { ok: true as const, percent: PROMOS[normalised] };
    }
    return { ok: false as const, message: 'That code has expired or does not exist.' };
  };

  const removePromo = () => setPromo(null);

  const totals: CartTotals = useMemo(() => {
    const subtotal = lines.reduce((sum, line) => sum + line.unitPrice * line.qty, 0);
    const itemCount = lines.reduce((sum, line) => sum + line.qty, 0);
    const discount = promo ? Math.round(subtotal * PROMOS[promo] * 100) / 100 : 0;
    const afterDiscount = subtotal - discount;
    const shipping = subtotal === 0 || afterDiscount >= FREE_SHIPPING_THRESHOLD ? 0 : 8;
    const tax = Math.round(afterDiscount * TAX_RATE * 100) / 100;
    return {
      subtotal,
      discount,
      shipping,
      tax,
      total: Math.round((afterDiscount + shipping + tax) * 100) / 100,
      itemCount,
    };
  }, [lines, promo]);

  return {
    lines,
    totals,
    promo,
    lastAdded,
    add,
    setQty,
    remove,
    clear,
    applyPromo,
    removePromo,
    freeShippingThreshold: FREE_SHIPPING_THRESHOLD,
  };
}

export type CartApi = ReturnType<typeof useCart>;
