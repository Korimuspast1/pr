import { useEffect } from 'react';
import { Route, Routes, useLocation } from 'react-router-dom';
import { StoreProvider, useStore } from './context/StoreContext';
import { Header } from './components/Header';
import { Footer } from './components/Footer';
import { CartDrawer } from './components/CartDrawer';
import { SearchOverlay } from './components/SearchOverlay';
import { BackToTop } from './components/BackToTop';
import { Home } from './pages/Home';
import { Shop } from './pages/Shop';
import { Product } from './pages/Product';
import { Collections } from './pages/Collections';
import { Checkout } from './pages/Checkout';
import { OrderConfirmed } from './pages/OrderConfirmed';
import { About } from './pages/About';
import { Support } from './pages/Support';
import { NotFound } from './pages/NotFound';

export default function App() {
  return (
    <StoreProvider>
      <Shell />
    </StoreProvider>
  );
}

function Shell() {
  const location = useLocation();
  const { toast } = useStore();

  useEffect(() => {
    if (location.pathname !== '/checkout' && location.pathname !== '/order-confirmed') {
      return;
    }
    window.scrollTo({ top: 0 });
  }, [location.pathname]);

  const hideChrome = location.pathname === '/checkout';

  return (
    <div className="app grain">
      <a href="#main" className="skip-link">
        Skip to content
      </a>
      {!hideChrome && <Header />}
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/shop" element={<Shop />} />
        <Route path="/product/:slug" element={<Product />} />
        <Route path="/collections" element={<Collections />} />
        <Route path="/checkout" element={<Checkout />} />
        <Route path="/order-confirmed" element={<OrderConfirmed />} />
        <Route path="/about" element={<About />} />
        <Route path="/support" element={<Support />} />
        <Route path="*" element={<NotFound />} />
      </Routes>
      {!hideChrome && <Footer />}
      <CartDrawer />
      <SearchOverlay />
      {!hideChrome && <BackToTop />}
      <div className={`toast ${toast ? 'is-visible' : ''}`} role="status" aria-live="polite">
        <span className="toast__dot" aria-hidden="true" />
        {toast}
      </div>
    </div>
  );
}
