import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useStore } from '../context/StoreContext';
import { money, classNames } from '../lib/format';
import { QuantityStepper } from './QuantityStepper';
import { products } from '../data/products';

export function CartDrawer() {
  const { cart, cartOpen, closeCart, notify } = useStore();
  const navigate = useNavigate();
  const panelRef = useRef<HTMLDivElement | null>(null);
  const [promoInput, setPromoInput] = useState('');
  const [promoError, setPromoError] = useState<string | null>(null);
  const { totals, lines, freeShippingThreshold } = cart;

  useEffect(() => {
    if (!cartOpen) return;
    const focusTarget = panelRef.current?.querySelector<HTMLElement>('[data-autofocus]');
    focusTarget?.focus({ preventScroll: true });
  }, [cartOpen]);

  useEffect(() => {
    if (!cartOpen) {
      setPromoInput('');
      setPromoError(null);
    }
  }, [cartOpen]);

  const remaining = Math.max(0, freeShippingThreshold - (totals.subtotal - totals.discount));
  const progress = Math.min(100, ((totals.subtotal - totals.discount) / freeShippingThreshold) * 100);

  const handlePromo = (event: React.FormEvent) => {
    event.preventDefault();
    const result = cart.applyPromo(promoInput);
    if (result.ok) {
      setPromoError(null);
      setPromoInput('');
      notify(`Code applied — ${Math.round(result.percent * 100)}% off`);
    } else {
      setPromoError(result.message);
    }
  };

  const recommendations = products
    .filter((product) => !lines.some((line) => line.productId === product.id))
    .slice(0, 2);

  return (
    <div className={classNames('drawer', cartOpen && 'is-open')} aria-hidden={!cartOpen}>
      <div className="drawer__scrim" onClick={closeCart} aria-hidden="true" />
      <div
        className="drawer__panel"
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label="Shopping bag"
      >
        <header className="drawer__head">
          <div>
            <p className="eyebrow eyebrow--plain">Your bag</p>
            <h2 className="drawer__title serif">
              {totals.itemCount > 0 ? `${totals.itemCount} item${totals.itemCount > 1 ? 's' : ''}` : 'Empty'}
            </h2>
          </div>
          <button
            type="button"
            onClick={closeCart}
            className="icon-btn"
            aria-label="Close bag"
            data-autofocus
          >
            <svg width="15" height="15" viewBox="0 0 16 16" aria-hidden="true">
              <path d="M2 2l12 12M14 2L2 14" stroke="currentColor" strokeWidth="1.15" />
            </svg>
          </button>
        </header>

        {totals.itemCount === 0 ? (
          <div className="drawer__empty">
            <div className="drawer__empty-mark" aria-hidden="true">
              <CircleMark />
            </div>
            <h3 className="serif drawer__empty-title">Nothing here yet</h3>
            <p className="body-muted">Your bag is waiting. Start with our most-loved card.</p>
            <Link to="/shop" className="btn btn--block" onClick={closeCart}>
              Browse the shop
            </Link>
            <Link to="/product/carbon-metal-debit" className="link-underline" onClick={closeCart}>
              See the Carbon Debit
            </Link>
          </div>
        ) : (
          <>
            <div className="drawer__ship">
              <p className="drawer__ship-label">
                {remaining > 0 ? (
                  <>
                    You're <strong>{money(remaining)}</strong> from complimentary shipping
                  </>
                ) : (
                  <>
                    <CheckIcon /> Complimentary shipping unlocked
                  </>
                )}
              </p>
              <div className="drawer__ship-bar">
                <span style={{ width: `${progress}%` }} />
              </div>
            </div>

            <div className="drawer__body">
              <ul className="drawer__lines">
                {lines.map((line) => (
                  <li key={line.key} className="cart-line">
                    <Link
                      to={`/product/${line.slug}`}
                      className="cart-line__media"
                      onClick={closeCart}
                      aria-label={line.name}
                    >
                      <img src={line.image} alt={line.name} loading="lazy" />
                    </Link>
                    <div className="cart-line__info">
                      <div className="cart-line__top">
                        <Link to={`/product/${line.slug}`} onClick={closeCart} className="cart-line__name">
                          {line.name}
                        </Link>
                        <span className="cart-line__price">{money(line.unitPrice * line.qty)}</span>
                      </div>
                      <p className="cart-line__variant">
                        {line.variantLabel}
                        {line.monogram ? ` · “${line.monogram}”` : ''}
                      </p>
                      <div className="cart-line__bottom">
                        <QuantityStepper
                          size="sm"
                          value={line.qty}
                          onChange={(qty) => cart.setQty(line.key, qty)}
                        />
                        <button
                          type="button"
                          className="cart-line__remove"
                          onClick={() => cart.remove(line.key)}
                        >
                          Remove
                        </button>
                      </div>
                    </div>
                  </li>
                ))}
              </ul>

              {recommendations.length > 0 && (
                <div className="drawer__recs">
                  <p className="drawer__recs-title">Pairs well with</p>
                  {recommendations.map((product) => (
                    <Link
                      key={product.id}
                      to={`/product/${product.slug}`}
                      className="mini-rec"
                      onClick={closeCart}
                    >
                      <img src={product.images[0]} alt="" loading="lazy" />
                      <span className="mini-rec__text">
                        <span className="mini-rec__name">{product.name}</span>
                        <span className="mini-rec__sub">{product.subtitle}</span>
                      </span>
                      <span className="mini-rec__price">{money(product.price)}</span>
                    </Link>
                  ))}
                </div>
              )}
            </div>

            <footer className="drawer__foot">
              {cart.promo ? (
                <div className="promo-applied">
                  <span>
                    <CheckIcon /> {cart.promo} applied
                  </span>
                  <button type="button" onClick={cart.removePromo}>
                    Remove
                  </button>
                </div>
              ) : (
                <form className="promo-form" onSubmit={handlePromo}>
                  <input
                    value={promoInput}
                    onChange={(event) => {
                      setPromoInput(event.target.value);
                      setPromoError(null);
                    }}
                    placeholder="Promo code"
                    aria-label="Promo code"
                    autoComplete="off"
                  />
                  <button type="submit" disabled={!promoInput.trim()}>
                    Apply
                  </button>
                </form>
              )}
              {promoError && <p className="promo-error">{promoError}</p>}

              <dl className="summary">
                <div>
                  <dt>Subtotal</dt>
                  <dd>{money(totals.subtotal)}</dd>
                </div>
                {totals.discount > 0 && (
                  <div className="summary__discount">
                    <dt>Discount</dt>
                    <dd>−{money(totals.discount)}</dd>
                  </div>
                )}
                <div>
                  <dt>Shipping</dt>
                  <dd>{totals.shipping === 0 ? 'Complimentary' : money(totals.shipping)}</dd>
                </div>
                <div className="summary__total">
                  <dt>Total</dt>
                  <dd>{money(totals.total)}</dd>
                </div>
              </dl>

              <button
                type="button"
                className="btn btn--block"
                onClick={() => {
                  closeCart();
                  navigate('/checkout');
                }}
              >
                Checkout · {money(totals.total)}
              </button>
              <p className="drawer__note">Taxes calculated at checkout. Free returns within 30 days.</p>
            </footer>
          </>
        )}
      </div>
    </div>
  );
}

export function CheckIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 14 14" aria-hidden="true" className="check-icon">
      <path d="M2 7.4l3.2 3.2L12 3.4" stroke="currentColor" strokeWidth="1.5" fill="none" strokeLinecap="round" />
    </svg>
  );
}

function CircleMark() {
  return (
    <svg width="46" height="46" viewBox="0 0 48 48" fill="none" aria-hidden="true">
      <circle cx="24" cy="24" r="23" stroke="currentColor" strokeWidth="0.8" opacity="0.3" />
      <rect x="13" y="17" width="22" height="14" rx="2.5" stroke="currentColor" strokeWidth="0.9" />
      <path d="M13 22h22" stroke="currentColor" strokeWidth="0.9" />
    </svg>
  );
}
