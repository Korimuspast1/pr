import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Reveal } from '../components/Reveal';
import { useStore } from '../context/StoreContext';
import { money } from '../lib/format';

const FAQS = [
  {
    q: 'How long does delivery take?',
    a: 'Express orders ship the same working day if placed before 14:00 CET and arrive in 2–3 business days across Europe, 3–5 to the rest of the world. Priority overnight is available at checkout. Every order is tracked.',
  },
  {
    q: 'Can I return a card?',
    a: 'Yes — 30 days, no questions, and we cover postage both ways. Engraved cards can be returned but the monogram fee is not refundable. Email studio@visadebit.example and we will send a prepaid label.',
  },
  {
    q: 'What does the warranty cover?',
    a: 'Five years on metal and composite cards, three years on polymer, and lifetime repair on all leather goods. Manufacturing faults and finish failures are replaced free. Deliberate damage and normal patina are not covered.',
  },
  {
    q: 'Is this a real bank card?',
    a: 'No. This is a demonstration storefront built to show the shopping experience. No payment is taken, no card is issued, and no personal data leaves your browser. Try 4242 4242 4242 4242 at checkout.',
  },
  {
    q: 'Do you ship outside the EU?',
    a: 'Yes, to 38 countries via DHL Express. Duties and import taxes are calculated at checkout for most destinations so nothing is charged on delivery.',
  },
  {
    q: 'Can I change an order after placing it?',
    a: 'Until it is packed — usually about four hours. Reply to your confirmation email or call the studio and we will adjust the address, monogram or shipping method.',
  },
];

export function Support() {
  const [open, setOpen] = useState<number | null>(0);
  const [ref, setRef] = useState('');
  const [sent, setSent] = useState(false);
  const { notify } = useStore();

  const submit = (event: React.FormEvent) => {
    event.preventDefault();
    setSent(true);
    notify('Message received — we reply within one working day.');
  };

  return (
    <main id="main" className="support">
      <header className="support__hero shell shell--wide">
        <Reveal>
          <span className="eyebrow">Support</span>
        </Reveal>
        <Reveal delay={1}>
          <h1 className="display support__title">
            A person, usually
            <br />
            <em>within the hour</em>
          </h1>
        </Reveal>
        <Reveal delay={2}>
          <p className="lede">
            Mon–Fri, 9–18 CET. We are eleven people, so the person answering your email has probably touched
            your card.
          </p>
        </Reveal>
      </header>

      <div className="support__quick shell shell--wide">
        {[
          { t: 'Track an order', d: 'Enter your order number', to: '#contact' },
          { t: 'Start a return', d: 'Free, 30 days', to: '#contact' },
          { t: 'Activate a card', d: 'Two minutes in the app', to: '#activate' },
          { t: 'Book a repair', d: 'Leather, free for life', to: '#warranty' },
        ].map((card, index) => (
          <Reveal key={card.t} delay={(index % 4) as 0 | 1 | 2 | 3}>
            <a href={card.to} className="support-quick">
              <span className="support-quick__title">{card.t}</span>
              <span className="support-quick__desc">{card.d}</span>
              <svg width="14" height="14" viewBox="0 0 16 16" aria-hidden="true">
                <path d="M3 8h9M8.5 4.5L12 8l-3.5 3.5" stroke="currentColor" strokeWidth="1.2" fill="none" strokeLinecap="round" />
              </svg>
            </a>
          </Reveal>
        ))}
      </div>

      <div className="support__layout shell shell--wide">
        <section className="support__faq">
          <Reveal>
            <h2 className="display support__h2">Common questions</h2>
          </Reveal>
          <div className="faq">
            {FAQS.map((faq, index) => (
              <div key={faq.q} className={`faq__item ${open === index ? 'is-open' : ''}`}>
                <button
                  type="button"
                  className="faq__q"
                  onClick={() => setOpen(open === index ? null : index)}
                  aria-expanded={open === index}
                >
                  <span>{faq.q}</span>
                  <span className="faq__icon" aria-hidden="true">
                    <span />
                    <span />
                  </span>
                </button>
                <div className="faq__a">
                  <p>{faq.a}</p>
                </div>
              </div>
            ))}
          </div>

          <div className="support__blocks">
            <div className="support-block" id="activate">
              <h3 className="serif">Activating your card</h3>
              <p className="body-muted">
                Open the app, scan the card's edge code, and set a PIN. The physical card activates
                immediately; the virtual twin is available before the metal one leaves the box.
              </p>
            </div>
            <div className="support-block" id="warranty">
              <h3 className="serif">Warranty & repair</h3>
              <p className="body-muted">
                Metal and composite cards carry a five-year warranty, polymer three years, and leather is
                repaired free for life. Send a photo to studio@visadebit.example and we will confirm within a
                day.
              </p>
            </div>
          </div>
        </section>

        <aside className="support__contact" id="contact">
          <div className="contact-card">
            <h2 className="serif contact-card__title">Write to the studio</h2>
            <p className="body-muted contact-card__sub">
              Replies within one working day. For order queries, include your reference.
            </p>
            <form onSubmit={submit} className="contact-form">
              <div className="field">
                <label htmlFor="c-name">Name</label>
                <input id="c-name" required placeholder="Elise Marchetti" />
              </div>
              <div className="field">
                <label htmlFor="c-email">Email</label>
                <input id="c-email" type="email" required placeholder="you@example.com" />
              </div>
              <div className="field">
                <label htmlFor="c-ref">
                  Order number <span className="field__optional">optional</span>
                </label>
                <input
                  id="c-ref"
                  value={ref}
                  onChange={(event) => setRef(event.target.value)}
                  placeholder="VB-123456"
                />
              </div>
              <div className="field">
                <label htmlFor="c-message">Message</label>
                <textarea id="c-message" rows={4} required placeholder="How can we help?" />
              </div>
              <button type="submit" className="btn btn--block">
                {sent ? 'Message sent' : 'Send message'}
              </button>
            </form>
            <div className="contact-card__details">
              <p>
                <span className="body-muted">Email</span>
                <a href="mailto:studio@visadebit.example">studio@visadebit.example</a>
              </p>
              <p>
                <span className="body-muted">Telephone</span>
                <a href="tel:+41445550120">+41 44 555 01 20</a>
              </p>
              <p>
                <span className="body-muted">Studio</span>
                Löwenstrasse 12, 8001 Zürich
              </p>
            </div>
          </div>
        </aside>
      </div>

      <section className="support__returns shell shell--narrow">
        <Reveal>
          <h2 className="display support__h2">Shipping & returns at a glance</h2>
        </Reveal>
        <div className="support__table">
          <div>
            <span>Express (Europe)</span>
            <span>2–3 business days</span>
            <span>{money(0)} over {money(150)}</span>
          </div>
          <div>
            <span>Rest of world</span>
            <span>3–5 business days</span>
            <span>{money(18)}</span>
          </div>
          <div>
            <span>Priority overnight</span>
            <span>Next business day</span>
            <span>{money(19)}</span>
          </div>
          <div>
            <span>Returns</span>
            <span>30 days, free</span>
            <span>Prepaid label</span>
          </div>
        </div>
        <Link to="/shop" className="btn">
          Back to the shop
        </Link>
      </section>
    </main>
  );
}
