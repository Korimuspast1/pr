import { Link } from 'react-router-dom';
import type { Product } from '../data/types';
import { money, classNames } from '../lib/format';
import { Stars } from './Stars';
import { useStore } from '../context/StoreContext';

interface Props {
  product: Product;
  priority?: boolean;
  compact?: boolean;
}

export function ProductCard({ product, priority = false, compact = false }: Props) {
  const { cart, openCart, notify } = useStore();
  const hoverImage = product.images[1] ?? product.images[0];
  const defaultVariant = product.variants[0];
  const price = product.price + defaultVariant.priceDelta;

  const quickAdd = (event: React.MouseEvent) => {
    event.preventDefault();
    event.stopPropagation();
    cart.add({
      productId: product.id,
      slug: product.slug,
      variantId: defaultVariant.id,
      name: product.name,
      variantLabel: defaultVariant.label,
      finish: defaultVariant.finish,
      image: product.images[0],
      unitPrice: price,
    });
    notify(`${product.name} added to your bag`);
    openCart();
  };

  return (
    <article className={classNames('card', compact && 'card--compact')}>
      <Link to={`/product/${product.slug}`} className="card__media">
        {product.badge && <span className="card__badge">{product.badge}</span>}
        {product.compareAt && (
          <span className="card__save">
            Save {money(product.compareAt - product.price)}
          </span>
        )}
        <img
          src={product.images[0]}
          alt={`${product.name} — ${product.subtitle}`}
          className="card__img card__img--main"
          loading={priority ? 'eager' : 'lazy'}
          decoding="async"
        />
        <img
          src={hoverImage}
          alt=""
          aria-hidden="true"
          className="card__img card__img--alt"
          loading="lazy"
          decoding="async"
        />
        <button
          type="button"
          className="card__quick"
          onClick={quickAdd}
          aria-label={`Add ${product.name} to bag`}
        >
          <span>Quick add</span>
          <svg width="13" height="13" viewBox="0 0 14 14" aria-hidden="true">
            <path d="M2 7h10M7 2v10" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" />
          </svg>
        </button>
      </Link>

      <div className="card__body">
        <div className="card__meta">
          <span className="card__subtitle">{product.subtitle}</span>
          <span className="card__price">
            {product.compareAt && <s>{money(product.compareAt)}</s>}
            {money(price)}
          </span>
        </div>
        <h3 className="card__name serif">
          <Link to={`/product/${product.slug}`}>{product.name}</Link>
        </h3>
        <div className="card__rating">
          <Stars rating={product.rating} size={11} />
          <span>{product.reviewCount.toLocaleString('en-GB')} reviews</span>
        </div>
        <div className="card__swatch-row">
          {product.variants.map((variant) => (
            <span
              key={variant.id}
              className={classNames('card__swatch', `swatch--${variant.id}`)}
              title={variant.label}
            />
          ))}
          <span className="card__swatch-count">
            {product.variants.length} finish{product.variants.length > 1 ? 'es' : ''}
          </span>
        </div>
      </div>
    </article>
  );
}
