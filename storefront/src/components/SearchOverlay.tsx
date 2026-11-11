import { useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useStore } from '../context/StoreContext';
import { products } from '../data/products';
import { money, classNames } from '../lib/format';

const SUGGESTIONS = ['Metal card', 'Travel', 'Leather sleeve', 'Gift set', 'Under €100'];

export function SearchOverlay() {
  const { searchOpen, setSearchOpen } = useStore();
  const [query, setQuery] = useState('');
  const inputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    if (searchOpen) {
      const t = window.setTimeout(() => inputRef.current?.focus(), 120);
      return () => window.clearTimeout(t);
    }
    setQuery('');
    return undefined;
  }, [searchOpen]);

  const results = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return [];
    return products
      .filter((product) =>
        [product.name, product.subtitle, product.material, ...product.tags]
          .join(' ')
          .toLowerCase()
          .includes(q),
      )
      .slice(0, 5);
  }, [query]);

  if (!searchOpen) return null;

  return (
    <div className="search-overlay">
      <div className="search-overlay__scrim" onClick={() => setSearchOpen(false)} aria-hidden="true" />
      <div className="search-overlay__panel" role="dialog" aria-modal="true" aria-label="Search">
        <div className="search-overlay__bar shell">
          <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
            <circle cx="8" cy="8" r="5.6" stroke="currentColor" strokeWidth="1.2" fill="none" />
            <path d="M12.2 12.2L16 16" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
          </svg>
          <input
            ref={inputRef}
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search cards, leather, gifts…"
            aria-label="Search"
          />
          <button type="button" onClick={() => setSearchOpen(false)} className="search-overlay__close">
            Close
          </button>
        </div>

        <div className="search-overlay__body shell shell--narrow">
          {!query && (
            <>
              <p className="eyebrow">Popular</p>
              <div className="search-suggestions">
                {SUGGESTIONS.map((suggestion) => (
                  <button key={suggestion} type="button" onClick={() => setQuery(suggestion.replace('Under €100', 'essential'))}>
                    {suggestion}
                  </button>
                ))}
              </div>
              <p className="eyebrow search-overlay__section">Featured</p>
              <div className="search-featured">
                {products.slice(0, 3).map((product) => (
                  <Link
                    key={product.id}
                    to={`/product/${product.slug}`}
                    className="search-result"
                    onClick={() => setSearchOpen(false)}
                  >
                    <img src={product.images[0]} alt="" loading="lazy" />
                    <span>
                      <span className="search-result__name">{product.name}</span>
                      <span className="search-result__sub">{product.subtitle}</span>
                    </span>
                    <span className="search-result__price">{money(product.price)}</span>
                  </Link>
                ))}
              </div>
            </>
          )}

          {query && results.length === 0 && (
            <div className="search-empty">
              <p className="serif">No matches for “{query}”</p>
              <p className="body-muted">Try “metal”, “leather”, or browse the full shop.</p>
              <Link to="/shop" className="btn btn--sm" onClick={() => setSearchOpen(false)}>
                Shop all products
              </Link>
            </div>
          )}

          {query && results.length > 0 && (
            <>
              <p className={classNames('eyebrow', 'search-overlay__section')}>
                {results.length} result{results.length > 1 ? 's' : ''}
              </p>
              <div className="search-featured">
                {results.map((product) => (
                  <Link
                    key={product.id}
                    to={`/product/${product.slug}`}
                    className="search-result"
                    onClick={() => setSearchOpen(false)}
                  >
                    <img src={product.images[0]} alt="" loading="lazy" />
                    <span>
                      <span className="search-result__name">{product.name}</span>
                      <span className="search-result__sub">{product.subtitle}</span>
                    </span>
                    <span className="search-result__price">{money(product.price)}</span>
                  </Link>
                ))}
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
