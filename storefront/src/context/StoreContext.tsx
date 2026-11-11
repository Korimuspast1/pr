import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { useCart } from '../lib/cart-core';

type Cart = ReturnType<typeof useCart>;

interface StoreValue {
  cart: Cart;
  cartOpen: boolean;
  openCart: () => void;
  closeCart: () => void;
  toast: string | null;
  notify: (message: string) => void;
  searchOpen: boolean;
  setSearchOpen: (open: boolean) => void;
}

const StoreContext = createContext<StoreValue | null>(null);

export function StoreProvider({ children }: { children: ReactNode }) {
  const cart = useCart();
  const [cartOpen, setCartOpen] = useState(false);
  const [toast, setToast] = useState<string | null>(null);
  const [searchOpen, setSearchOpen] = useState(false);

  const openCart = useCallback(() => setCartOpen(true), []);
  const closeCart = useCallback(() => setCartOpen(false), []);

  const notify = useCallback((message: string) => setToast(message), []);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timer);
  }, [toast]);

  useEffect(() => {
    const shouldLock = cartOpen || searchOpen;
    document.body.classList.toggle('no-scroll', shouldLock);
    return () => document.body.classList.remove('no-scroll');
  }, [cartOpen, searchOpen]);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setCartOpen(false);
        setSearchOpen(false);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  return (
    <StoreContext.Provider
      value={{ cart, cartOpen, openCart, closeCart, toast, notify, searchOpen, setSearchOpen }}
    >
      {children}
    </StoreContext.Provider>
  );
}

export function useStore() {
  const value = useContext(StoreContext);
  if (!value) throw new Error('useStore must be used inside StoreProvider');
  return value;
}
