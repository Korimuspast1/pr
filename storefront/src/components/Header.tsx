import { useEffect, useState } from 'react';
import { Link, NavLink, useLocation } from 'react-router-dom';
import { useStore } from '../context/StoreContext';
import { classNames } from '../lib/format';
import { collections } from '../data/collections';

const ANNOUNCEMENTS = [
  'Complimentary shipping over €150',
  'Use code STUDIO15 for 15% off your first card',
  'Hand-finished in Zürich — shipped worldwide',
];

export function Header() {
  const { cart, openCart, setSearchOpen } = useStore();
  const [scrolled, setScrolled] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [ticker, setTicker] = useState(0);
  const location = useLocation();

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 24);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  useEffect(() => {
    setMenuOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    const id = window.setInterval(() => setTicker((value) => (value + 1) % ANNOUNCEMENTS.length), 4800);
    return () => window.clearInterval(id);
  }, []);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && menuOpen) setMenuOpen(false);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [menuOpen]);

  const isHome = location.pathname === '/';
  const transparent = isHome && !scrolled && !menuOpen;

  return (
    <>
      <div className="announce">
        <div className="announce__inner">
          <span className="announce__item announce__item--side">Ships worldwide · DHL Express</span>
          <span className="announce__item announce__item--main" key={ticker}>
            {ANNOUNCEMENTS[ticker]}
          </span>
          <span className="announce__item announce__item--side">30-day returns</span>
        </div>
      </div>

      <header className={classNames('header', scrolled && 'is-scrolled', transparent && 'is-transparent')}>
        <div className="header__inner shell shell--wide">
          <nav className="header__nav header__nav--left" aria-label="Main">
            <button
              type="button"
              className="header__menu-btn"
              onClick={() => setMenuOpen((open) => !open)}
              aria-expanded={menuOpen}
              aria-label="Toggle menu"
            >
              <span className={classNames('burger', menuOpen && 'is-open')}>
                <span />
                <span />
              </span>
            </button>
            <NavLink to="/shop" className="header__link">
              Shop all
            </NavLink>
            <NavLink to="/collections" className="header__link">
              Collections
            </NavLink>
            <NavLink to="/shop?sort=newest" className="header__link">
              New in
            </NavLink>
          </nav>

          <Link to="/" className="wordmark" aria-label="Visa Debit — home">
            <span className="wordmark__visa">VISA</span>
            <span className="wordmark__rule" aria-hidden="true" />
            <span className="wordmark__sub">Debit Studio</span>
          </Link>

          <div className="header__nav header__nav--right">
            <button
              type="button"
              className="header__icon"
              onClick={() => setSearchOpen(true)}
              aria-label="Search products"
            >
              <svg width="17" height="17" viewBox="0 0 18 18" aria-hidden="true">
                <circle cx="8" cy="8" r="5.6" stroke="currentColor" strokeWidth="1.2" fill="none" />
                <path d="M12.2 12.2L16 16" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
              </svg>
            </button>
            <Link to="/shop?collection=atelier" className="header__link header__link--hide-sm">
              Gift sets
            </Link>
            <button type="button" className="header__cart" onClick={openCart} aria-label={`Bag, ${cart.totals.itemCount} items`}>
              <span className="header__cart-label">Bag</span>
              <span className={classNames('header__cart-count', cart.totals.itemCount > 0 && 'is-active')}>
                {cart.totals.itemCount}
              </span>
            </button>
          </div>
        </div>

        <div className={classNames('mobile-menu', menuOpen && 'is-open')}>
          <div className="mobile-menu__inner">
            <p className="eyebrow">Menu</p>
            <ul className="mobile-menu__list">
              <li>
                <NavLink to="/shop" onClick={() => setMenuOpen(false)}>
                  Shop all
                </NavLink>
              </li>
              <li>
                <NavLink to="/collections" onClick={() => setMenuOpen(false)}>
                  Collections
                </NavLink>
              </li>
              <li>
                <NavLink to="/about" onClick={() => setMenuOpen(false)}>
                  Our story
                </NavLink>
              </li>
            </ul>
            <div className="mobile-menu__collections">
              {collections.map((collection) => (
                <Link
                  key={collection.id}
                  to={`/shop?collection=${collection.id}`}
                  className="mobile-collection"
                  onClick={() => setMenuOpen(false)}
                >
                  <img src={collection.image} alt="" loading="lazy" />
                  <span>
                    <span className="mobile-collection__eyebrow">{collection.eyebrow}</span>
                    <span className="mobile-collection__name serif">{collection.name}</span>
                  </span>
                </Link>
              ))}
            </div>
            <div className="mobile-menu__foot">
              <span>Mon–Fri, 9–18 CET</span>
              <a href="tel:+41445550120">+41 44 555 01 20</a>
            </div>
          </div>
        </div>
      </header>
      {menuOpen && <button type="button" className="mobile-menu__scrim" aria-label="Close menu" onClick={() => setMenuOpen(false)} />}
    </>
  );
}
