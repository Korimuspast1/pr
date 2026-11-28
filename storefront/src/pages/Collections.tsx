import { Link } from 'react-router-dom';
import { collections } from '../data/collections';
import { products } from '../data/products';
import { Reveal } from '../components/Reveal';
import { money } from '../lib/format';

export function Collections() {
  return (
    <main id="main" className="collections-page">
      <header className="collections-page__head shell shell--wide">
        <Reveal>
          <span className="eyebrow">Four collections</span>
        </Reveal>
        <Reveal delay={1}>
          <h1 className="display collections-page__title">
            Objects for a life,
            <br />
            <em>not a season</em>
          </h1>
        </Reveal>
        <Reveal delay={2}>
          <p className="lede">
            We keep the range deliberately small. Four collections, twelve products, released slowly and rarely
            discounted. Here is how to tell them apart.
          </p>
        </Reveal>
      </header>

      <div className="collections-page__list">
        {collections.map((collection, index) => {
          const items = products.filter((p) => p.collection === collection.id);
          const cheapest = Math.min(...items.map((p) => p.price));
          return (
            <section key={collection.id} className="collection-row">
              <div className="shell shell--wide collection-row__inner">
                <Reveal className="collection-row__media">
                  <Link to={`/shop?collection=${collection.id}`}>
                    <img src={collection.image} alt={collection.name} loading={index === 0 ? 'eager' : 'lazy'} />
                  </Link>
                  <span className="collection-row__index serif">0{index + 1}</span>
                </Reveal>
                <div className="collection-row__copy">
                  <Reveal>
                    <span className="eyebrow">{collection.eyebrow}</span>
                  </Reveal>
                  <Reveal delay={1}>
                    <h2 className="display collection-row__title">{collection.name}</h2>
                  </Reveal>
                  <Reveal delay={2}>
                    <p className="lede collection-row__desc">{collection.description}</p>
                  </Reveal>
                  <Reveal delay={2}>
                    <dl className="collection-row__meta">
                      <div>
                        <dt>Pieces</dt>
                        <dd>{items.length}</dd>
                      </div>
                      <div>
                        <dt>From</dt>
                        <dd>{money(cheapest)}</dd>
                      </div>
                      <div>
                        <dt>Made in</dt>
                        <dd>{collection.id === 'atelier' ? 'Florence' : 'Zürich'}</dd>
                      </div>
                    </dl>
                  </Reveal>
                  <Reveal delay={3}>
                    <ul className="collection-row__items">
                      {items.map((item) => (
                        <li key={item.id}>
                          <Link to={`/product/${item.slug}`}>
                            <img src={item.images[0]} alt="" loading="lazy" />
                            <span>
                              <span className="collection-row__item-name">{item.name}</span>
                              <span className="collection-row__item-price">{money(item.price)}</span>
                            </span>
                          </Link>
                        </li>
                      ))}
                    </ul>
                  </Reveal>
                  <Reveal delay={4}>
                    <Link to={`/shop?collection=${collection.id}`} className="link-underline">
                      Explore {collection.name}
                    </Link>
                  </Reveal>
                </div>
              </div>
            </section>
          );
        })}
      </div>
    </main>
  );
}
