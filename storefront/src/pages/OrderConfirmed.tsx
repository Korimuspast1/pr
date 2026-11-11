import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { money, formatReviewDate } from '../lib/format';
import { Reveal } from '../components/Reveal';

interface OrderItem {
  key: string;
  name: string;
  variantLabel: string;
  image: string;
  qty: number;
  unitPrice: number;
  monogram?: string;
}

interface Order {
  number: string;
  email: string;
  name: string;
  address: string;
  shipping: string;
  total: number;
  items: OrderItem[];
}

export function OrderConfirmed() {
  const [order, setOrder] = useState<Order | null>(null);

  useEffect(() => {
    document.title = 'Order confirmed · Visa Debit Studio';
    const raw = sessionStorage.getItem('visa-last-order');
    if (raw) {
      try {
        setOrder(JSON.parse(raw));
      } catch {
        setOrder(null);
      }
    }
    window.scrollTo({ top: 0 });
  }, []);

  const eta = new Date();
  eta.setDate(eta.getDate() + (order?.shipping === 'Priority overnight' ? 1 : 3));

  return (
    <main id="main" className="confirmed">
      <div className="confirmed__hero shell shell--narrow">
        <Reveal>
          <span className="confirmed__mark" aria-hidden="true">
            <svg width="26" height="26" viewBox="0 0 28 28">
              <path d="M5 14.5l5.6 5.6L23 7" stroke="currentColor" strokeWidth="1.3" fill="none" strokeLinecap="round" />
            </svg>
          </span>
        </Reveal>
        <Reveal delay={1}>
          <span className="eyebrow eyebrow--plain">Order received</span>
        </Reveal>
        <Reveal delay={2}>
          <h1 className="display confirmed__title">
            Thank you — it's being
            <br />
            <em>wrapped by hand</em>
          </h1>
        </Reveal>
        <Reveal delay={3}>
          <p className="lede">
            A confirmation is on its way to {order?.email || 'your inbox'}. We'll send tracking as soon as DHL
            collects from Löwenstrasse.
          </p>
        </Reveal>
      </div>

      <div className="confirmed__card shell shell--narrow">
        <div className="confirmed__card-head">
          <div>
            <p className="eyebrow eyebrow--plain">Order</p>
            <p className="confirmed__number serif">{order?.number ?? 'VB-000000'}</p>
          </div>
          <div className="confirmed__eta">
            <p className="eyebrow eyebrow--plain">Estimated arrival</p>
            <p className="serif">{formatReviewDate(eta.toISOString())}</p>
          </div>
        </div>

        <div className="confirmed__track">
          {['Received', 'In the studio', 'Dispatched', 'Delivered'].map((label, index) => (
            <div key={label} className={`confirmed__track-step ${index === 0 ? 'is-done' : ''}`}>
              <span className="confirmed__dot" />
              <span className="confirmed__track-label">{label}</span>
            </div>
          ))}
        </div>

        {order && (
          <>
            <ul className="confirmed__items">
              {order.items.map((item) => (
                <li key={item.key}>
                  <img src={item.image} alt="" />
                  <div>
                    <p className="confirmed__item-name">{item.name}</p>
                    <p className="body-muted">
                      {item.variantLabel}
                      {item.monogram ? ` · “${item.monogram}”` : ''} · Qty {item.qty}
                    </p>
                  </div>
                  <span>{money(item.unitPrice * item.qty)}</span>
                </li>
              ))}
            </ul>
            <dl className="confirmed__meta">
              <div>
                <dt>Delivering to</dt>
                <dd>{order.address}</dd>
              </div>
              <div>
                <dt>Method</dt>
                <dd>{order.shipping}</dd>
              </div>
              <div>
                <dt>Total paid</dt>
                <dd className="confirmed__total">{money(order.total)}</dd>
              </div>
            </dl>
          </>
        )}

        {!order && (
          <p className="body-muted confirmed__none">
            We couldn't find a recent order in this browser session. Your bag is still saved if you'd like to
            try again.
          </p>
        )}
      </div>

      <div className="confirmed__actions shell shell--narrow">
        <Link to="/shop" className="btn">
          Continue shopping
        </Link>
        <Link to="/support" className="btn btn--ghost">
          Delivery questions
        </Link>
      </div>

      <section className="confirmed__next shell shell--narrow">
        <p className="eyebrow">While you wait</p>
        <div className="confirmed__next-grid">
          <Link to="/about" className="next-card">
            <img src="/img/collection-business.jpg" alt="" loading="lazy" />
            <span className="next-card__title serif">Inside the workshop</span>
            <span className="next-card__sub">Eleven people, one bench, since 2019</span>
          </Link>
          <Link to="/product/atelier-card-sleeve" className="next-card">
            <img src="/img/product-sleeve.jpg" alt="A cognac leather card sleeve" loading="lazy" />
            <span className="next-card__title serif">The sleeve it belongs in</span>
            <span className="next-card__sub">Vegetable-tanned, stitched by hand in Florence</span>
          </Link>
        </div>
      </section>
    </main>
  );
}
