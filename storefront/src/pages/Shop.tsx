import { useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { products, allMaterials } from '../data/products';
import { collections, collectionMap } from '../data/collections';
import { ProductCard } from '../components/ProductCard';
import { Reveal } from '../components/Reveal';
import { money, classNames } from '../lib/format';
import type { Product } from '../data/types';

const SORTS = [
  { id: 'featured', label: 'Featured' },
  { id: 'price-asc', label: 'Price · low to high' },
  { id: 'price-desc', label: 'Price · high to low' },
  { id: 'rating', label: 'Top rated' },
  { id: 'newest', label: 'Newest' },
] as const;

type SortId = (typeof SORTS)[number]['id'];

const PRICE_BANDS = [
  { id: 'under-100', label: 'Under €100', test: (p: number) => p < 100 },
  { id: '100-250', label: '€100 – €250', test: (p: number) => p >= 100 && p <= 250 },
  { id: '250-400', label: '€250 – €400', test: (p: number) => p > 250 && p <= 400 },
  { id: 'over-400', label: 'Over €400', test: (p: number) => p > 400 },
];

const AVAILABILITY = [
  { id: 'in-stock', label: 'Ready to ship', test: (product: Product) => product.variants.some((v) => v.stock > 0) },
  { id: 'limited', label: 'Limited editions', test: (product: Product) => product.badge === 'Limited' },
  { id: 'new', label: 'New arrivals', test: (product: Product) => product.badge === 'New' },
];

export function Shop() {
  const [params, setParams] = useSearchParams();
  const [filtersOpen, setFiltersOpen] = useState(false);

  const activeCollections = params.getAll('collection');
  const activeMaterials = params.getAll('material');
  const activePrice = params.getAll('price');
  const activeAvailability = params.getAll('availability');
  const sort = (params.get('sort') as SortId) ?? 'featured';

  const toggleParam = (key: string, value: string) => {
    const next = new URLSearchParams(params);
    const existing = next.getAll(key);
    next.delete(key);
    const updated = existing.includes(value) ? existing.filter((v) => v !== value) : [...existing, value];
    updated.forEach((v) => next.append(key, v));
    setParams(next, { replace: true });
  };

  const clearAll = () => {
    const next = new URLSearchParams();
    if (params.get('sort')) next.set('sort', params.get('sort') as string);
    setParams(next, { replace: true });
  };

  const setSort = (id: string) => {
    const next = new URLSearchParams(params);
    next.set('sort', id);
    setParams(next, { replace: true });
  };

  const filtered = useMemo(() => {
    let list = products.filter((product) => {
      if (activeCollections.length && !activeCollections.includes(product.collection)) return false;
      if (activeMaterials.length && !activeMaterials.includes(product.materialGroup)) return false;
      if (activePrice.length) {
        const bands = PRICE_BANDS.filter((band) => activePrice.includes(band.id));
        if (bands.length && !bands.some((band) => band.test(product.price))) return false;
      }
      if (activeAvailability.length) {
        const tests = AVAILABILITY.filter((a) => activeAvailability.includes(a.id));
        if (tests.length && !tests.some((test) => test.test(product))) return false;
      }
      return true;
    });

    switch (sort) {
      case 'price-asc':
        list = [...list].sort((a, b) => a.price - b.price);
        break;
      case 'price-desc':
        list = [...list].sort((a, b) => b.price - a.price);
        break;
      case 'rating':
        list = [...list].sort((a, b) => b.rating - a.rating);
        break;
      case 'newest':
        list = [...list].sort((a, b) => +new Date(b.releasedAt) - +new Date(a.releasedAt));
        break;
      default:
        list = [...list].sort((a, b) => a.featuredRank - b.featuredRank);
    }
    return list;
  }, [activeCollections, activeMaterials, activePrice, activeAvailability, sort]);

  const activeCount =
    activeCollections.length + activeMaterials.length + activePrice.length + activeAvailability.length;

  useEffect(() => {
    document.title = 'Shop all · Visa Debit Studio';
  }, []);

  const collectionParam = activeCollections[0];
  const heading = collectionParam ? collectionMap[collectionParam]?.name ?? 'All products' : 'All products';
  const headingCopy = collectionParam
    ? collectionMap[collectionParam]?.description
    : 'Twelve objects, four collections. Every card is hand-finished; every sleeve is stitched by hand. Nothing here is made in a hurry.';

  return (
    <main id="main" className="shop">
      <header className="shop__head shell shell--wide">
        <div className="shop__head-inner">
          <Reveal>
            <nav className="breadcrumb" aria-label="Breadcrumb">
              <Link to="/">Home</Link>
              <span aria-hidden="true">/</span>
              <span>Shop</span>
              {collectionParam && (
                <>
                  <span aria-hidden="true">/</span>
                  <span>{collectionMap[collectionParam]?.name}</span>
                </>
              )}
            </nav>
          </Reveal>
          <Reveal delay={1}>
            <h1 className="display shop__title">{heading}</h1>
          </Reveal>
          <Reveal delay={2}>
            <p className="lede shop__lede">{headingCopy}</p>
          </Reveal>
        </div>
        <Reveal delay={2} className="shop__head-aside">
          <span className="eyebrow eyebrow--plain">Showing</span>
          <span className="shop__count">
            {filtered.length} <span>/ {products.length}</span>
          </span>
        </Reveal>
      </header>

      <div className="shop__chip-row shell shell--wide">
        <button
          type="button"
          className="filter-trigger"
          onClick={() => setFiltersOpen((open) => !open)}
          aria-expanded={filtersOpen}
        >
          <svg width="13" height="13" viewBox="0 0 16 16" aria-hidden="true">
            <path d="M2 4h12M4 8h8M6 12h4" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
          </svg>
          Filters
          {activeCount > 0 && <span className="filter-trigger__badge">{activeCount}</span>}
        </button>
        <div className="shop__chips">
          <button
            type="button"
            className={classNames('chip', activeCollections.length === 0 && 'is-active')}
            onClick={() => {
              const next = new URLSearchParams(params);
              next.delete('collection');
              setParams(next, { replace: true });
            }}
          >
            All
          </button>
          {collections.map((collection) => (
            <button
              key={collection.id}
              type="button"
              className={classNames('chip', activeCollections.includes(collection.id) && 'is-active')}
              onClick={() => toggleParam('collection', collection.id)}
            >
              {collection.name}
            </button>
          ))}
        </div>
      </div>

      <div className="shop__layout shell shell--wide">
        <aside className={classNames('filters', filtersOpen && 'is-open')} aria-label="Filters">
          <div className="filters__head">
            <p className="eyebrow">Refine</p>
            {activeCount > 0 && (
              <button type="button" className="filters__clear" onClick={clearAll}>
                Clear all
              </button>
            )}
          </div>

          <FilterGroup title="Collection">
            {collections.map((collection) => (
              <label key={collection.id} className="filter-opt">
                <input
                  type="checkbox"
                  checked={activeCollections.includes(collection.id)}
                  onChange={() => toggleParam('collection', collection.id)}
                />
                <span className="filter-opt__box" aria-hidden="true">
                  <svg width="9" height="9" viewBox="0 0 12 12">
                    <path d="M1.5 6.4l3 3L10.5 2.5" stroke="currentColor" strokeWidth="1.6" fill="none" strokeLinecap="round" />
                  </svg>
                </span>
                <span className="filter-opt__label">{collection.name}</span>
                <span className="filter-opt__count">
                  {products.filter((p) => p.collection === collection.id).length}
                </span>
              </label>
            ))}
          </FilterGroup>

          <FilterGroup title="Material">
            {allMaterials.map((material) => (
              <label key={material.id} className="filter-opt">
                <input
                  type="checkbox"
                  checked={activeMaterials.includes(material.id)}
                  onChange={() => toggleParam('material', material.id)}
                />
                <span className="filter-opt__box" aria-hidden="true">
                  <svg width="9" height="9" viewBox="0 0 12 12">
                    <path d="M1.5 6.4l3 3L10.5 2.5" stroke="currentColor" strokeWidth="1.6" fill="none" strokeLinecap="round" />
                  </svg>
                </span>
                <span className="filter-opt__label">{material.label}</span>
                <span className="filter-opt__count">
                  {products.filter((p) => p.materialGroup === material.id).length}
                </span>
              </label>
            ))}
          </FilterGroup>

          <FilterGroup title="Price">
            {PRICE_BANDS.map((band) => (
              <label key={band.id} className="filter-opt">
                <input
                  type="checkbox"
                  checked={activePrice.includes(band.id)}
                  onChange={() => toggleParam('price', band.id)}
                />
                <span className="filter-opt__box" aria-hidden="true">
                  <svg width="9" height="9" viewBox="0 0 12 12">
                    <path d="M1.5 6.4l3 3L10.5 2.5" stroke="currentColor" strokeWidth="1.6" fill="none" strokeLinecap="round" />
                  </svg>
                </span>
                <span className="filter-opt__label">{band.label}</span>
                <span className="filter-opt__count">
                  {products.filter((p) => band.test(p.price)).length}
                </span>
              </label>
            ))}
          </FilterGroup>

          <FilterGroup title="Availability">
            {AVAILABILITY.map((option) => (
              <label key={option.id} className="filter-opt">
                <input
                  type="checkbox"
                  checked={activeAvailability.includes(option.id)}
                  onChange={() => toggleParam('availability', option.id)}
                />
                <span className="filter-opt__box" aria-hidden="true">
                  <svg width="9" height="9" viewBox="0 0 12 12">
                    <path d="M1.5 6.4l3 3L10.5 2.5" stroke="currentColor" strokeWidth="1.6" fill="none" strokeLinecap="round" />
                  </svg>
                </span>
                <span className="filter-opt__label">{option.label}</span>
                <span className="filter-opt__count">
                  {products.filter((p) => option.test(p)).length}
                </span>
              </label>
            ))}
          </FilterGroup>

          <div className="filters__note">
            <p className="serif">Not sure where to start?</p>
            <p className="body-muted">The Essential Debit is the one we recommend to first-time buyers.</p>
            <Link to="/product/essential-debit" className="link-underline">
              See it
            </Link>
          </div>

          <button type="button" className="btn btn--block filters__apply" onClick={() => setFiltersOpen(false)}>
            Show {filtered.length} products
          </button>
        </aside>

        <section className="shop__results">
          <div className="shop__toolbar">
            <p className="shop__result-count" aria-live="polite">
              {filtered.length} product{filtered.length === 1 ? '' : 's'}
              {activeCount > 0 && <span className="body-muted"> · filtered</span>}
            </p>
            <div className="sort">
              <label htmlFor="sort-select" className="sort__label">
                Sort
              </label>
              <select id="sort-select" value={sort} onChange={(event) => setSort(event.target.value)}>
                {SORTS.map((option) => (
                  <option key={option.id} value={option.id}>
                    {option.label}
                  </option>
                ))}
              </select>
              <svg width="10" height="10" viewBox="0 0 12 12" aria-hidden="true" className="sort__chev">
                <path d="M2 4.5L6 8.5L10 4.5" stroke="currentColor" strokeWidth="1.2" fill="none" strokeLinecap="round" />
              </svg>
            </div>
          </div>

          {activeCount > 0 && (
            <div className="active-filters">
              {[
                ...activeCollections.map((v) => ({ key: 'collection', value: v, label: collectionMap[v]?.name ?? v })),
                ...activeMaterials.map((v) => ({ key: 'material', value: v, label: allMaterials.find((m) => m.id === v)?.label ?? v })),
                ...activePrice.map((v) => ({ key: 'price', value: v, label: PRICE_BANDS.find((b) => b.id === v)?.label ?? v })),
                ...activeAvailability.map((v) => ({ key: 'availability', value: v, label: AVAILABILITY.find((a) => a.id === v)?.label ?? v })),
              ].map((tag) => (
                <button key={`${tag.key}-${tag.value}`} className="active-filter" onClick={() => toggleParam(tag.key, tag.value)}>
                  {tag.label}
                  <svg width="9" height="9" viewBox="0 0 12 12" aria-hidden="true">
                    <path d="M2 2l8 8M10 2l-8 8" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" />
                  </svg>
                </button>
              ))}
              <button type="button" className="active-filters__clear" onClick={clearAll}>
                Clear
              </button>
            </div>
          )}

          {filtered.length === 0 ? (
            <div className="shop__empty">
              <p className="serif shop__empty-title">Nothing matches that combination</p>
              <p className="body-muted">Try removing a filter, or browse everything we make.</p>
              <button type="button" className="btn btn--sm" onClick={clearAll}>
                Clear filters
              </button>
            </div>
          ) : (
            <div className="product-grid product-grid--shop">
              {filtered.map((product, index) => (
                <Reveal key={product.id} delay={(index % 3) as 0 | 1 | 2}>
                  <ProductCard product={product} priority={index < 3} />
                </Reveal>
              ))}
            </div>
          )}

          <div className="shop__footnote">
            <p>
              Prices include VAT where applicable. Complimentary shipping over {money(150)}. Every order is
              wrapped by hand in the studio.
            </p>
          </div>
        </section>
      </div>
    </main>
  );
}

function FilterGroup({ title, children }: { title: string; children: React.ReactNode }) {
  const [open, setOpen] = useState(true);
  return (
    <div className={classNames('filter-group', open && 'is-open')}>
      <button type="button" className="filter-group__head" onClick={() => setOpen((v) => !v)} aria-expanded={open}>
        <span>{title}</span>
        <svg width="10" height="10" viewBox="0 0 12 12" aria-hidden="true">
          <path d="M2 4.5L6 8.5L10 4.5" stroke="currentColor" strokeWidth="1.2" fill="none" strokeLinecap="round" />
        </svg>
      </button>
      <div className="filter-group__body">{children}</div>
    </div>
  );
}
