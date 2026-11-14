import { Link } from 'react-router-dom';
import { Reveal } from '../components/Reveal';
import { ProductCard } from '../components/ProductCard';
import { Stars } from '../components/Stars';
import { collections } from '../data/collections';
import { products, featuredProducts } from '../data/products';
import { money } from '../lib/format';
import { useStore } from '../context/StoreContext';

export function Home() {
  const hero = products.find((p) => p.slug === 'carbon-metal-debit')!;
  const spotlight = products.find((p) => p.slug === 'champagne-gold-debit')!;
  const { openCart } = useStore();

  const bestSellers = [...products].sort((a, b) => b.reviewCount - a.reviewCount).slice(0, 4);

  return (
    <main id="main">
      {/* ---------------- Hero ---------------- */}
      <section className="hero">
        <div className="hero__inner shell shell--wide">
          <div className="hero__copy">
            <Reveal>
              <span className="eyebrow">The Winter Release · MMXXVI</span>
            </Reveal>
            <Reveal delay={1}>
              <h1 className="display hero__title">
                A debit card
                <br />
                you'll want
                <br />
                <em>to hold.</em>
              </h1>
            </Reveal>
            <Reveal delay={2}>
              <p className="lede hero__lede">
                Hand-finished metal and recycled polymer, made in Zürich for the everyday. No printed noise, no
                logos shouting — just weight, surface, and the quiet pleasure of paying for something with
                something beautiful.
              </p>
            </Reveal>
            <Reveal delay={3}>
              <div className="hero__actions">
                <Link to="/shop" className="btn">
                  Shop the collection
                </Link>
                <Link to={`/product/${hero.slug}`} className="link-underline">
                  Meet the Carbon Debit
                </Link>
              </div>
            </Reveal>
            <Reveal delay={4}>
              <div className="hero__trust">
                <div className="hero__trust-item">
                  <Stars rating={4.8} size={11} />
                  <span>4.8 · 2,400+ reviews</span>
                </div>
                <span className="hero__trust-div" aria-hidden="true" />
                <div className="hero__trust-item">
                  <span>Free shipping over €150</span>
                </div>
                <span className="hero__trust-div" aria-hidden="true" />
                <div className="hero__trust-item">
                  <span>30-day returns</span>
                </div>
              </div>
            </Reveal>
          </div>

          <div className="hero__visual">
            <Reveal delay={1} className="hero__frame">
              <img
                src="/img/lifestyle-hero.jpg"
                alt="A matte black metal debit card on linen beside an espresso cup"
                className="hero__img"
                decoding="async"
              />
              <div className="hero__tag hero__tag--tl">
                <span className="hero__tag-label">Featured</span>
                <span className="hero__tag-value serif">Carbon Debit</span>
              </div>
              <div className="hero__price-card">
                <div>
                  <p className="hero__price-name">{hero.name}</p>
                  <p className="hero__price-sub">{hero.subtitle}</p>
                </div>
                <div className="hero__price-right">
                  <span className="hero__price-value">{money(hero.price)}</span>
                  <Link to={`/product/${hero.slug}`} aria-label={`View ${hero.name}`} className="hero__price-go">
                    <svg width="14" height="14" viewBox="0 0 16 16" aria-hidden="true">
                      <path d="M3 8h9M8.5 4.5L12 8l-3.5 3.5" stroke="currentColor" strokeWidth="1.2" fill="none" strokeLinecap="round" />
                    </svg>
                  </Link>
                </div>
              </div>
            </Reveal>
            <span className="hero__vertical" aria-hidden="true">
              Est. 2019 — Zürich
            </span>
          </div>
        </div>

        <div className="hero__ticker" aria-hidden="true">
          <div className="hero__ticker-track">
            {Array.from({ length: 2 }).map((_, index) => (
              <span key={index} className="hero__ticker-run">
                <span>Hand-finished metal</span>
                <span className="hero__ticker-dot" />
                <span>Recycled polymer</span>
                <span className="hero__ticker-dot" />
                <span>Free returns</span>
                <span className="hero__ticker-dot" />
                <span>42 currencies</span>
                <span className="hero__ticker-dot" />
                <span>Made in Switzerland</span>
                <span className="hero__ticker-dot" />
              </span>
            ))}
          </div>
        </div>
      </section>

      {/* ---------------- Collections ---------------- */}
      <section className="section shell shell--wide">
        <div className="section-head">
          <div>
            <Reveal>
              <span className="eyebrow">Four collections</span>
            </Reveal>
            <Reveal delay={1}>
              <h2 className="display section-head__title">
                Choose by <em>temperament</em>
              </h2>
            </Reveal>
          </div>
          <Reveal delay={2}>
            <p className="section-head__side body-muted">
              Some people want a card that disappears into a pocket. Others want one that lands on a counter
              with a sound. We make both.
            </p>
          </Reveal>
        </div>

        <div className="collection-grid">
          {collections.map((collection, index) => (
            <Reveal key={collection.id} delay={(index % 4) as 0 | 1 | 2 | 3} className={`coll-tile coll-tile--${index}`}>
              <Link to={`/shop?collection=${collection.id}`} className="coll-tile__link">
                <div className="coll-tile__media">
                  <img src={collection.image} alt={collection.name} loading="lazy" />
                </div>
                <div className="coll-tile__overlay">
                  <span className="coll-tile__eyebrow">{collection.eyebrow}</span>
                  <h3 className="display coll-tile__name">{collection.name}</h3>
                  <p className="coll-tile__desc">{collection.description}</p>
                  <span className="coll-tile__cta">
                    Explore
                    <svg width="14" height="14" viewBox="0 0 16 16" aria-hidden="true">
                      <path d="M3 8h9M8.5 4.5L12 8l-3.5 3.5" stroke="currentColor" strokeWidth="1.2" fill="none" strokeLinecap="round" />
                    </svg>
                  </span>
                </div>
              </Link>
            </Reveal>
          ))}
        </div>
      </section>

      {/* ---------------- Featured products ---------------- */}
      <section className="section section--tight shell shell--wide featured">
        <div className="section-head">
          <div>
            <Reveal>
              <span className="eyebrow">The short list</span>
            </Reveal>
            <Reveal delay={1}>
              <h2 className="display section-head__title">
                This season's <em>essentials</em>
              </h2>
            </Reveal>
          </div>
          <Reveal delay={2}>
            <Link to="/shop" className="link-underline">
              View all {products.length} products
            </Link>
          </Reveal>
        </div>
        <div className="product-grid product-grid--home">
          {featuredProducts.slice(0, 4).map((product, index) => (
            <Reveal key={product.id} delay={(index % 4) as 0 | 1 | 2 | 3}>
              <ProductCard product={product} priority={index === 0} />
            </Reveal>
          ))}
        </div>
      </section>

      {/* ---------------- Editorial split ---------------- */}
      <section className="editorial">
        <div className="editorial__inner shell shell--wide">
          <Reveal className="editorial__media">
            <img
              src="/img/editorial-2.jpg"
              alt="A stack of hand-finished metal cards resting in a ceramic tray"
              loading="lazy"
            />
            <div className="editorial__badge">
              <span className="serif">86%</span>
              <span>recycled polymer in every Essential card</span>
            </div>
          </Reveal>
          <div className="editorial__copy">
            <Reveal>
              <span className="eyebrow">The studio</span>
            </Reveal>
            <Reveal delay={1}>
              <h2 className="display editorial__title">
                Made slowly, by people
                <br />
                you could <em>call</em>
              </h2>
            </Reveal>
            <Reveal delay={2}>
              <p className="lede">
                Eleven people in a workshop off Löwenstrasse. A two-person leather atelier outside Florence. We
                make a small number of things properly, and we answer the phone when you ring.
              </p>
            </Reveal>
            <Reveal delay={3}>
              <ul className="editorial__list">
                <li>
                  <span className="editorial__num">01</span>
                  <div>
                    <p className="editorial__item-title">Materials you can trace</p>
                    <p className="body-muted">Every steel batch and leather hide is logged by supplier and date.</p>
                  </div>
                </li>
                <li>
                  <span className="editorial__num">02</span>
                  <div>
                    <p className="editorial__item-title">Repair before replace</p>
                    <p className="body-muted">Sleeves are re-stitched free, for life. Cards replaced for five years.</p>
                  </div>
                </li>
                <li>
                  <span className="editorial__num">03</span>
                  <div>
                    <p className="editorial__item-title">No growth at any cost</p>
                    <p className="body-muted">We release four times a year and stop when a run sells out.</p>
                  </div>
                </li>
              </ul>
            </Reveal>
            <Reveal delay={4}>
              <Link to="/about" className="btn btn--ghost">
                Read our story
              </Link>
            </Reveal>
          </div>
        </div>
      </section>

      {/* ---------------- Spotlight ---------------- */}
      <section className="spotlight">
        <div className="spotlight__inner shell shell--wide">
          <div className="spotlight__copy">
            <Reveal>
              <span className="eyebrow">Numbered release</span>
            </Reveal>
            <Reveal delay={1}>
              <h2 className="display spotlight__title">
                {spotlight.name}
                <br />
                <em>250 pieces</em>
              </h2>
            </Reveal>
            <Reveal delay={2}>
              <p className="lede spotlight__lede">{spotlight.summary}</p>
            </Reveal>
            <Reveal delay={3}>
              <dl className="spotlight__stats">
                <div>
                  <dt>Material</dt>
                  <dd>PVD brass</dd>
                </div>
                <div>
                  <dt>Weight</dt>
                  <dd>17 g</dd>
                </div>
                <div>
                  <dt>Edition</dt>
                  <dd>250</dd>
                </div>
                <div>
                  <dt>Concierge</dt>
                  <dd>24 / 7</dd>
                </div>
              </dl>
            </Reveal>
            <Reveal delay={4}>
              <div className="hero__actions">
                <Link to={`/product/${spotlight.slug}`} className="btn btn--light">
                  View the Champagne
                </Link>
                <span className="spotlight__stock">17 remaining</span>
              </div>
            </Reveal>
          </div>
          <Reveal delay={2} className="spotlight__media">
            <img src="/img/editorial-3.jpg" alt="A hand holding a champagne-gold metal debit card" loading="lazy" />
            <span className="spotlight__caption">Champagne, brushed in one direction — 400 °C PVD</span>
          </Reveal>
        </div>
      </section>

      {/* ---------------- Best sellers ---------------- */}
      <section className="section shell shell--wide">
        <div className="section-head">
          <div>
            <Reveal>
              <span className="eyebrow">Most carried</span>
            </Reveal>
            <Reveal delay={1}>
              <h2 className="display section-head__title">
                What people <em>actually buy</em>
              </h2>
            </Reveal>
          </div>
          <Reveal delay={2}>
            <p className="section-head__side body-muted">
              Ranked by verified purchases this quarter. The Champagne has been outselling the Carbon since
              September.
            </p>
          </Reveal>
        </div>
        <div className="product-grid product-grid--home">
          {bestSellers.map((product, index) => (
            <Reveal key={product.id} delay={(index % 4) as 0 | 1 | 2 | 3}>
              <ProductCard product={product} />
            </Reveal>
          ))}
        </div>
      </section>

      {/* ---------------- Reviews ---------------- */}
      <section className="section reviews-band">
        <div className="shell shell--wide">
          <div className="reviews-band__head">
            <Reveal>
              <span className="eyebrow">In their words</span>
            </Reveal>
            <Reveal delay={1}>
              <h2 className="display reviews-band__title">
                2,400 people, <em>one observation</em>
              </h2>
            </Reveal>
            <Reveal delay={2}>
              <div className="reviews-band__rating">
                <Stars rating={4.8} size={16} />
                <span>4.8 average across all cards</span>
              </div>
            </Reveal>
          </div>

          <div className="reviews-band__grid">
            {[
              { name: 'Elise Marchetti', loc: 'Copenhagen', text: 'The weight is the thing. You notice it at the counter, and somehow that makes the whole transaction feel considered.', product: 'Carbon Debit' },
              { name: 'Marcus Feld', loc: 'Zürich', text: 'Four countries in six weeks, no FX markup, no surprises. The lounge access paid for the card in one trip.', product: 'Voyager Debit' },
              { name: 'Naomi Okafor', loc: 'London', text: 'Genuinely lovely object. I docked a star only because the weight is noticeable in a slim wallet.', product: 'Carbon Debit' },
              { name: 'Idris Achterberg', loc: 'Rotterdam', text: 'Vegetable-tanned, thick but pliable, stitching even all the way around. It has darkened beautifully.', product: 'Atelier Sleeve' },
            ].map((review, index) => (
              <Reveal key={review.name} delay={(index % 4) as 0 | 1 | 2 | 3} className="review-card">
                <Stars rating={5} size={11} />
                <p className="review-card__text serif">“{review.text}”</p>
                <div className="review-card__foot">
                  <span>{review.name}</span>
                  <span className="review-card__loc">{review.loc}</span>
                </div>
                <span className="review-card__product">{review.product}</span>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ---------------- Closing CTA ---------------- */}
      <section className="closing">
        <div className="closing__inner shell shell--narrow">
          <Reveal>
            <span className="eyebrow">The next step</span>
          </Reveal>
          <Reveal delay={1}>
            <h2 className="display closing__title">
              Buy one good thing
              <br />
              <em>instead of three</em>
            </h2>
          </Reveal>
          <Reveal delay={2}>
            <p className="lede closing__lede">
              Free shipping over €150, 30-day returns, and a five-year card warranty. Use{' '}
              <button
                type="button"
                className="inline-code"
                onClick={() => {
                  navigator.clipboard?.writeText('STUDIO15');
                  openCart();
                }}
              >
                STUDIO15
              </button>{' '}
              for 15% off your first order.
            </p>
          </Reveal>
          <Reveal delay={3}>
            <div className="closing__actions">
              <Link to="/shop" className="btn">
                Shop all products
              </Link>
              <Link to="/collections" className="btn btn--ghost">
                Browse collections
              </Link>
            </div>
          </Reveal>
        </div>
      </section>
    </main>
  );
}
