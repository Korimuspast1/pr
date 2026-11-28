import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { productBySlug, products } from '../data/products';
import { collectionMap } from '../data/collections';
import { money, classNames, formatReviewDate } from '../lib/format';
import { Stars } from '../components/Stars';
import { Reveal } from '../components/Reveal';
import { QuantityStepper } from '../components/QuantityStepper';
import { ProductCard } from '../components/ProductCard';
import { useStore } from '../context/StoreContext';
import type { Product as ProductType } from '../data/types';

export function Product() {
  const { slug = '' } = useParams();
  const product = productBySlug(slug);
  const navigate = useNavigate();

  useEffect(() => {
    if (!product) return;
    document.title = `${product.name} — ${product.subtitle} · Visa Debit Studio`;
    window.scrollTo({ top: 0, behavior: 'instant' as ScrollBehavior });
  }, [product]);

  if (!product) {
    return (
      <main id="main" className="notfound">
        <p className="eyebrow">404</p>
        <h1 className="display">We can't find that card</h1>
        <Link to="/shop" className="btn">
          Back to the shop
        </Link>
      </main>
    );
  }

  return <ProductView key={product.id} product={product} navigate={navigate} />;
}

function ProductView({ product, navigate }: { product: ProductType; navigate: ReturnType<typeof useNavigate> }) {
  const { cart, openCart, notify } = useStore();
  const [variantId, setVariantId] = useState(product.variants[0].id);
  const [qty, setQty] = useState(1);
  const [monogram, setMonogram] = useState('');
  const [activeImage, setActiveImage] = useState(0);
  const [lightbox, setLightbox] = useState(false);
  const [justAdded, setJustAdded] = useState(false);

  const variant = product.variants.find((v) => v.id === variantId) ?? product.variants[0];
  const price = product.price + variant.priceDelta;
  const collection = collectionMap[product.collection];

  const related = useMemo(
    () =>
      products
        .filter((p) => p.id !== product.id)
        .sort((a, b) => {
          const score = (x: typeof a) =>
            (x.collection === product.collection ? 2 : 0) + (x.materialGroup === product.materialGroup ? 1 : 0);
          return score(b) - score(a);
        })
        .slice(0, 4),
    [product],
  );

  const ratingBreakdown = useMemo(() => {
    const buckets = [5, 4, 3, 2, 1].map((stars) => ({
      stars,
      count: product.reviews.filter((r) => r.rating === stars).length,
    }));
    const total = product.reviews.length || 1;
    return buckets.map((bucket) => ({ ...bucket, pct: (bucket.count / total) * 100 }));
  }, [product]);

  const handleAdd = (goToCart = true) => {
    cart.add({
      productId: product.id,
      slug: product.slug,
      variantId: variant.id,
      name: product.name,
      variantLabel: variant.label,
      finish: variant.finish,
      image: product.images[0],
      unitPrice: price,
      qty,
      monogram: monogram.trim().toUpperCase() || undefined,
    });
    setJustAdded(true);
    notify(`${product.name} added to your bag`);
    if (goToCart) openCart();
  };

  const buyNow = () => {
    handleAdd(false);
    navigate('/checkout');
  };

  return (
    <main id="main" className="pdp">
      <div className="pdp__crumb shell shell--wide">
        <nav className="breadcrumb" aria-label="Breadcrumb">
          <Link to="/">Home</Link>
          <span aria-hidden="true">/</span>
          <Link to="/shop">Shop</Link>
          <span aria-hidden="true">/</span>
          <Link to={`/shop?collection=${product.collection}`}>{collection?.name}</Link>
          <span aria-hidden="true">/</span>
          <span className="breadcrumb__current">{product.name}</span>
        </nav>
      </div>

      <div className="pdp__layout shell shell--wide">
        {/* ------------- Gallery ------------- */}
        <section className="gallery" aria-label="Product images">
          <div className="gallery__main">
            <button
              type="button"
              className="gallery__zoom"
              onClick={() => setLightbox(true)}
              aria-label="Expand image"
            >
              <svg width="15" height="15" viewBox="0 0 16 16" aria-hidden="true">
                <path d="M6 2H2v4M10 2h4v4M6 14H2v-4M10 14h4v-4" stroke="currentColor" strokeWidth="1.1" fill="none" strokeLinecap="round" />
              </svg>
            </button>
            {product.badge && <span className="pdp__badge">{product.badge}</span>}
            <img
              key={product.images[activeImage]}
              src={product.images[activeImage]}
              alt={`${product.name} in ${variant.label} — view ${activeImage + 1}`}
              className="gallery__img"
              decoding="async"
            />
            <div className="gallery__nav">
              <button
                type="button"
                onClick={() => setActiveImage((i) => (i - 1 + product.images.length) % product.images.length)}
                aria-label="Previous image"
              >
                <svg width="13" height="13" viewBox="0 0 16 16" aria-hidden="true">
                  <path d="M10 3L5 8l5 5" stroke="currentColor" strokeWidth="1.2" fill="none" strokeLinecap="round" />
                </svg>
              </button>
              <span className="gallery__counter">
                {activeImage + 1} / {product.images.length}
              </span>
              <button
                type="button"
                onClick={() => setActiveImage((i) => (i + 1) % product.images.length)}
                aria-label="Next image"
              >
                <svg width="13" height="13" viewBox="0 0 16 16" aria-hidden="true">
                  <path d="M6 3l5 5-5 5" stroke="currentColor" strokeWidth="1.2" fill="none" strokeLinecap="round" />
                </svg>
              </button>
            </div>
          </div>

          <div className="gallery__thumbs" role="tablist" aria-label="Product image thumbnails">
            {product.images.map((image, index) => (
              <button
                key={image + index}
                type="button"
                role="tab"
                aria-selected={index === activeImage}
                className={classNames('gallery__thumb', index === activeImage && 'is-active')}
                onClick={() => setActiveImage(index)}
              >
                <img src={image} alt="" loading="lazy" />
              </button>
            ))}
          </div>

          <div className="gallery__strip">
            {product.images.slice(0, 3).map((image, index) => (
              <div key={image + index} className="gallery__strip-item">
                <img src={image} alt="" loading="lazy" />
              </div>
            ))}
          </div>
        </section>

        {/* ------------- Buy panel ------------- */}
        <section className="buy">
          <div className="buy__intro">
            <div className="buy__eyebrow-row">
              <Link to={`/shop?collection=${product.collection}`} className="eyebrow eyebrow--plain buy__coll">
                {collection?.name}
              </Link>
              <span className="buy__lead">{product.leadTime}</span>
            </div>
            <h1 className="display buy__title">{product.name}</h1>
            <p className="buy__subtitle">{product.subtitle}</p>
            <button
              type="button"
              className="buy__rating"
              onClick={() => document.getElementById('reviews')?.scrollIntoView({ behavior: 'smooth' })}
            >
              <Stars rating={product.rating} size={13} />
              <span>
                {product.rating.toFixed(1)} · {product.reviewCount.toLocaleString('en-GB')} reviews
              </span>
            </button>
          </div>

          <div className="buy__price-row">
            <span className="buy__price">{money(price)}</span>
            {product.compareAt && <s className="buy__compare">{money(product.compareAt)}</s>}
            <span className="buy__tax">incl. VAT</span>
          </div>

          <p className="buy__summary">{product.summary}</p>

          <div className="buy__option">
            <div className="buy__option-head">
              <span className="buy__option-label">
                Finish — <strong>{variant.label}</strong>
              </span>
              <span className="buy__option-finish">{variant.finish}</span>
            </div>
            <div className="swatches">
              {product.variants.map((option) => (
                <button
                  key={option.id}
                  type="button"
                  className={classNames(
                    'swatch',
                    `swatch--${option.id}`,
                    option.id === variantId && 'is-active',
                    option.stock === 0 && 'is-soldout',
                  )}
                  onClick={() => setVariantId(option.id)}
                  aria-pressed={option.id === variantId}
                  title={option.label}
                >
                  <span className="swatch__dot" />
                  <span className="swatch__label">{option.label}</span>
                  {option.priceDelta > 0 && <span className="swatch__delta">+{money(option.priceDelta)}</span>}
                </button>
              ))}
            </div>
          </div>

          <div className="buy__stock">
            {variant.stock <= 10 ? (
              <>
                <span className="buy__stock-dot buy__stock-dot--low" />
                Only {variant.stock} left in {variant.label}
              </>
            ) : (
              <>
                <span className="buy__stock-dot" />
                In stock · {variant.stock} ready to ship
              </>
            )}
          </div>

          <div className="buy__monogram">
            <label htmlFor="monogram">
              Add a monogram <span className="body-muted">— optional, engraved on the reverse edge</span>
            </label>
            <div className="buy__monogram-row">
              <input
                id="monogram"
                value={monogram}
                maxLength={3}
                placeholder="AB"
                onChange={(event) => setMonogram(event.target.value.replace(/[^a-zA-Z]/g, '').toUpperCase())}
                aria-describedby="monogram-hint"
              />
              <span id="monogram-hint" className="buy__monogram-hint">
                Up to 3 initials · free · adds 2 days
              </span>
            </div>
          </div>

          <div className="buy__actions">
            <QuantityStepper value={qty} onChange={setQty} />
            <button type="button" className="btn buy__add" onClick={() => handleAdd(true)}>
              {justAdded ? 'Added' : 'Add to bag'} · {money(price * qty)}
            </button>
          </div>
          <button type="button" className="btn btn--ghost btn--block buy__now" onClick={buyNow}>
            Buy it now
          </button>

          <ul className="buy__assurance">
            <li>
              <svg width="15" height="15" viewBox="0 0 16 16" aria-hidden="true">
                <path d="M1 4.5h9v7H1zM10 7h3l2 2.5v2h-5" stroke="currentColor" strokeWidth="1" fill="none" />
                <circle cx="4" cy="12" r="1.4" stroke="currentColor" strokeWidth="1" fill="none" />
                <circle cx="12.5" cy="12" r="1.4" stroke="currentColor" strokeWidth="1" fill="none" />
              </svg>
              Complimentary DHL Express over {money(150)}
            </li>
            <li>
              <svg width="15" height="15" viewBox="0 0 16 16" aria-hidden="true">
                <path d="M8 1.8l5.5 2.4v4c0 3-2.3 5.3-5.5 6-3.2-.7-5.5-3-5.5-6v-4z" stroke="currentColor" strokeWidth="1" fill="none" />
                <path d="M5.6 7.9l1.7 1.7 3.3-3.4" stroke="currentColor" strokeWidth="1.1" fill="none" strokeLinecap="round" />
              </svg>
              30-day returns, no questions
            </li>
            <li>
              <svg width="15" height="15" viewBox="0 0 16 16" aria-hidden="true">
                <circle cx="8" cy="8" r="6.2" stroke="currentColor" strokeWidth="1" fill="none" />
                <path d="M8 4.4V8l2.4 1.6" stroke="currentColor" strokeWidth="1.1" fill="none" strokeLinecap="round" />
              </svg>
              {product.materialGroup === 'leather' ? 'Lifetime repair, free of charge' : '5-year warranty, free replacement'}
            </li>
            <li>
              <svg width="15" height="15" viewBox="0 0 16 16" aria-hidden="true">
                <path d="M2 8.5l3 3 7-7" stroke="currentColor" strokeWidth="1.1" fill="none" strokeLinecap="round" />
              </svg>
              Wrapped by hand in the studio
            </li>
          </ul>

          <div className="buy__accordions">
            <Accordion title="What's in the box" defaultOpen>
              <ul className="bullet-list">
                {product.includes.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </Accordion>
            <Accordion title="Specifications">
              <dl className="spec-list">
                {product.specs.map((spec) => (
                  <div key={spec.label}>
                    <dt>{spec.label}</dt>
                    <dd>{spec.value}</dd>
                  </div>
                ))}
              </dl>
            </Accordion>
            <Accordion title="Shipping & returns">
              <p className="body-muted">
                {product.leadTime}. Orders placed before 14:00 CET ship the same day from Zürich. DHL Express is
                complimentary over {money(150)}; otherwise {money(8)}. Returns are free within 30 days, and we
                cover postage both ways.
              </p>
            </Accordion>
            <Accordion title="Care">
              <p className="body-muted">
                Wipe metal with a dry microfibre cloth. Leather benefits from a thin layer of our Atelier balm
                twice a year. Avoid solvents, and keep engraved edges away from abrasive pockets.
              </p>
            </Accordion>
          </div>
        </section>
      </div>

      {/* ------------- Story ------------- */}
      <section className="pdp__story shell shell--wide">
        <Reveal className="pdp__story-copy">
          <span className="eyebrow">The object</span>
          <h2 className="display pdp__story-title">
            Considered from
            <br />
            <em>every angle</em>
          </h2>
          {product.story.map((paragraph) => (
            <p key={paragraph.slice(0, 24)} className="pdp__story-p">
              {paragraph}
            </p>
          ))}
        </Reveal>
        <div className="pdp__features">
          {product.features.map((feature, index) => (
            <Reveal key={feature.title} delay={(index % 4) as 0 | 1 | 2 | 3} className="pdp__feature">
              <span className="pdp__feature-num">{String(index + 1).padStart(2, '0')}</span>
              <h3 className="pdp__feature-title">{feature.title}</h3>
              <p className="body-muted">{feature.body}</p>
            </Reveal>
          ))}
        </div>
      </section>

      <section className="pdp__band">
        <div className="shell shell--wide pdp__band-inner">
          <div>
            <span className="pdp__band-num serif">{product.weightGrams}</span>
            <span className="pdp__band-label">grams</span>
          </div>
          <div>
            <span className="pdp__band-num serif">{product.thicknessMm}</span>
            <span className="pdp__band-label">mm thick</span>
          </div>
          <div>
            <span className="pdp__band-num serif">{product.materialGroup === 'leather' ? '∞' : '5'}</span>
            <span className="pdp__band-label">{product.materialGroup === 'leather' ? 'year repair' : 'year warranty'}</span>
          </div>
          <div>
            <span className="pdp__band-num serif">{product.reviewCount.toLocaleString('en-GB')}</span>
            <span className="pdp__band-label">verified reviews</span>
          </div>
          <div>
            <span className="pdp__band-num serif">{product.rating.toFixed(1)}</span>
            <span className="pdp__band-label">average rating</span>
          </div>
        </div>
      </section>

      {/* ------------- Reviews ------------- */}
      <section className="reviews shell shell--wide" id="reviews">
        <div className="reviews__head">
          <div>
            <Reveal>
              <span className="eyebrow">Verified reviews</span>
            </Reveal>
            <Reveal delay={1}>
              <h2 className="display reviews__title">
                What owners <em>say</em>
              </h2>
            </Reveal>
          </div>
          <Reveal delay={2} className="reviews__score">
            <span className="reviews__score-num serif">{product.rating.toFixed(1)}</span>
            <div>
              <Stars rating={product.rating} size={14} />
              <p className="body-muted">{product.reviewCount.toLocaleString('en-GB')} verified purchases</p>
            </div>
          </Reveal>
        </div>

        <div className="reviews__grid">
          <div className="reviews__breakdown">
            {ratingBreakdown.map((bucket) => (
              <div key={bucket.stars} className="reviews__bar-row">
                <span className="reviews__bar-label">{bucket.stars}</span>
                <div className="reviews__bar">
                  <span style={{ width: `${bucket.pct}%` }} />
                </div>
                <span className="reviews__bar-count">{bucket.count}</span>
              </div>
            ))}
            <div className="reviews__summary-note">
              <p className="serif">96% would recommend this</p>
              <p className="body-muted">Based on post-purchase surveys, last 12 months.</p>
            </div>
          </div>

          <div className="reviews__list">
            {product.reviews.map((review, index) => (
              <Reveal key={review.id} as="article" delay={(index % 3) as 0 | 1 | 2} className="review">
                <header className="review__head">
                  <div>
                    <div className="review__stars">
                      <Stars rating={review.rating} size={11} />
                      {review.verified && <span className="review__verified">Verified purchase</span>}
                    </div>
                    <h3 className="review__title">{review.title}</h3>
                  </div>
                  <time className="review__date" dateTime={review.date}>
                    {formatReviewDate(review.date)}
                  </time>
                </header>
                <p className="review__body">{review.body}</p>
                <footer className="review__foot">
                  <span className="review__author">
                    {review.author} · <span className="body-muted">{review.location}</span>
                  </span>
                  <span className="review__helpful">
                    <svg width="11" height="11" viewBox="0 0 14 14" aria-hidden="true">
                      <path d="M4.5 6.5L7 1.5l1.2.4c.6.2.9.9.7 1.5L8.4 5h3a1 1 0 011 1.2l-.8 4.6a1 1 0 01-1 .8H4.5M4.5 6.5v5.6M1.5 6.5h3v5.6h-3z" stroke="currentColor" strokeWidth="0.9" fill="none" strokeLinejoin="round" />
                    </svg>
                    {review.helpful} found this helpful
                  </span>
                </footer>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ------------- Related ------------- */}
      <section className="section shell shell--wide">
        <div className="section-head">
          <div>
            <Reveal>
              <span className="eyebrow">Goes well with</span>
            </Reveal>
            <Reveal delay={1}>
              <h2 className="display section-head__title">
                Complete the <em>set</em>
              </h2>
            </Reveal>
          </div>
          <Reveal delay={2}>
            <Link to="/shop" className="link-underline">
              Shop all
            </Link>
          </Reveal>
        </div>
        <div className="product-grid product-grid--home">
          {related.map((item, index) => (
            <Reveal key={item.id} delay={(index % 4) as 0 | 1 | 2 | 3}>
              <ProductCard product={item} />
            </Reveal>
          ))}
        </div>
      </section>

      {/* ------------- Lightbox ------------- */}
      {lightbox && (
        <div className="lightbox" role="dialog" aria-modal="true" aria-label="Image viewer" onClick={() => setLightbox(false)}>
          <button type="button" className="lightbox__close" onClick={() => setLightbox(false)} aria-label="Close viewer">
            <svg width="18" height="18" viewBox="0 0 16 16" aria-hidden="true">
              <path d="M2 2l12 12M14 2L2 14" stroke="currentColor" strokeWidth="1" />
            </svg>
          </button>
          <img src={product.images[activeImage]} alt={product.name} onClick={(e) => e.stopPropagation()} />
          <div className="lightbox__thumbs" onClick={(e) => e.stopPropagation()}>
            {product.images.map((image, index) => (
              <button
                key={image + index}
                type="button"
                className={classNames(index === activeImage && 'is-active')}
                onClick={() => setActiveImage(index)}
              >
                <img src={image} alt="" />
              </button>
            ))}
          </div>
        </div>
      )}
    </main>
  );
}

function Accordion({
  title,
  children,
  defaultOpen = false,
}: {
  title: string;
  children: React.ReactNode;
  defaultOpen?: boolean;
}) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className={classNames('acc', open && 'is-open')}>
      <button type="button" className="acc__head" onClick={() => setOpen((v) => !v)} aria-expanded={open}>
        <span>{title}</span>
        <span className="acc__icon" aria-hidden="true">
          <span />
          <span />
        </span>
      </button>
      <div className="acc__body">
        <div className="acc__inner">{children}</div>
      </div>
    </div>
  );
}
