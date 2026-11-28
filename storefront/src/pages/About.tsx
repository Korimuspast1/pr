import { Link } from 'react-router-dom';
import { Reveal } from '../components/Reveal';

export function About() {
  return (
    <main id="main" className="about">
      <header className="about__hero shell shell--wide">
        <div className="about__hero-copy">
          <Reveal>
            <span className="eyebrow">Our story</span>
          </Reveal>
          <Reveal delay={1}>
            <h1 className="display about__title">
              A workshop,
              <br />
              not a <em>bank</em>
            </h1>
          </Reveal>
          <Reveal delay={2}>
            <p className="lede">
              Visa Debit Studio began in 2019 with a question nobody had asked: why is the object you touch
              forty times a day the cheapest thing in your wallet?
            </p>
          </Reveal>
        </div>
        <Reveal delay={2} className="about__hero-media">
          <img src="/img/collection-business.jpg" alt="The studio building on Löwenstrasse at dusk" />
          <span className="about__caption">Löwenstrasse 12, Zürich — second floor, above the bookshop</span>
        </Reveal>
      </header>

      <section className="about__intro shell shell--narrow">
        <Reveal>
          <p className="about__lead serif">
            We make four collections and nothing else. Every card is finished by hand, every sleeve is stitched
            by two people in Tuscany, and every order is wrapped at the same bench where the prototypes were
            made.
          </p>
        </Reveal>
      </section>

      <section className="about__split shell shell--wide" id="workshop">
        <Reveal className="about__split-media">
          <img
            src="/img/collection-essentials.jpg"
            alt="The workbench at the studio: a stack of notebooks, a brass pen and reading glasses"
            loading="lazy"
          />
        </Reveal>
        <div className="about__split-copy">
          <Reveal>
            <span className="eyebrow">The workshop</span>
          </Reveal>
          <Reveal delay={1}>
            <h2 className="display about__h2">
              Eleven people,
              <br />
              one <em>bench</em>
            </h2>
          </Reveal>
          <Reveal delay={2}>
            <p>
              We started with a borrowed laser and a stack of steel blanks. Today the workshop holds eleven
              people, four tumblers, two polishing wheels and a kettle that is always on.
            </p>
          </Reveal>
          <Reveal delay={3}>
            <p>
              Nothing is outsourced except leather, which is made by a two-person atelier outside Florence that
              has been tanning the same way since 1974. We visit twice a year. They think our steel is excessive.
            </p>
          </Reveal>
          <Reveal delay={4}>
            <dl className="about__stats">
              <div>
                <dt className="serif">2019</dt>
                <dd>Founded in Zürich</dd>
              </div>
              <div>
                <dt className="serif">11</dt>
                <dd>People in the workshop</dd>
              </div>
              <div>
                <dt className="serif">4</dt>
                <dd>Releases a year</dd>
              </div>
              <div>
                <dt className="serif">86%</dt>
                <dd>Recycled polymer</dd>
              </div>
            </dl>
          </Reveal>
        </div>
      </section>

      <section className="about__values" id="sustainability">
        <div className="shell shell--wide">
          <Reveal>
            <span className="eyebrow">What we hold to</span>
          </Reveal>
          <Reveal delay={1}>
            <h2 className="display about__h2 about__h2--wide">
              Four rules we have
              <br />
              <em>not broken yet</em>
            </h2>
          </Reveal>
          <div className="about__values-grid">
            {[
              {
                n: '01',
                t: 'Small runs, no restocks',
                b: 'Every release is finite. When it sells out we change the finish rather than remake the same card — which means what you carry stays slightly rare.',
              },
              {
                n: '02',
                t: 'Repair before replace',
                b: 'Sleeves are re-stitched free for life. Cards are replaced free for five years. We would rather mend an object than sell you a second one.',
              },
              {
                n: '03',
                t: 'Traceable materials',
                b: 'Each steel batch and leather hide is logged by supplier, date and mill. Ask us about your card and we can tell you which coil it came from.',
              },
              {
                n: '04',
                t: 'No paid placements',
                b: 'We do not buy reviews, rankings or shelf space. Everything on this site comes from a verified purchase or a person we have met.',
              },
            ].map((value, index) => (
              <Reveal key={value.n} delay={(index % 4) as 0 | 1 | 2 | 3} className="value-card">
                <span className="value-card__n">{value.n}</span>
                <h3 className="value-card__t serif">{value.t}</h3>
                <p className="body-muted">{value.b}</p>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      <section className="about__press" id="press">
        <div className="shell shell--wide">
          <Reveal>
            <span className="eyebrow">Said elsewhere</span>
          </Reveal>
          <div className="about__press-grid">
            {[
              { q: 'The rare fintech object that justifies its price on feel alone.', s: 'Monocle' },
              { q: 'A debit card treated with the seriousness of a watch dial.', s: 'Wallpaper*' },
              { q: 'Absurdly good packaging. We kept the box.', s: 'Kinfolk' },
              { q: 'Proof that everyday objects deserve design attention.', s: 'Dezeen' },
            ].map((item, index) => (
              <Reveal key={item.s} delay={(index % 4) as 0 | 1 | 2 | 3} className="press-card">
                <p className="press-card__q serif">“{item.q}”</p>
                <p className="press-card__s">{item.s}</p>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      <section className="about__cta shell shell--narrow">
        <Reveal>
          <h2 className="display about__cta-title">
            Come and see
            <br />
            <em>the bench</em>
          </h2>
        </Reveal>
        <Reveal delay={1}>
          <p className="lede">
            The studio is open to visitors on Thursday afternoons. Ring the bell for the second floor — if the
            kettle is on, stay for a coffee.
          </p>
        </Reveal>
        <Reveal delay={2}>
          <div className="about__cta-actions">
            <Link to="/shop" className="btn">
              Shop the collections
            </Link>
            <Link to="/support" className="btn btn--ghost">
              Get in touch
            </Link>
          </div>
        </Reveal>
      </section>
    </main>
  );
}
