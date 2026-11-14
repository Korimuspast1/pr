import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useStore } from '../context/StoreContext';

const COLUMNS = [
  {
    title: 'Shop',
    links: [
      { label: 'All products', to: '/shop' },
      { label: 'Collections', to: '/collections' },
      { label: 'Metal cards', to: '/shop?material=metal' },
      { label: 'Leather goods', to: '/shop?material=leather' },
      { label: 'Gift sets', to: '/shop?collection=atelier' },
    ],
  },
  {
    title: 'Company',
    links: [
      { label: 'Our story', to: '/about' },
      { label: 'The workshop', to: '/about#workshop' },
      { label: 'Sustainability', to: '/about#sustainability' },
      { label: 'Press', to: '/about#press' },
    ],
  },
  {
    title: 'Support',
    links: [
      { label: 'Shipping & returns', to: '/support' },
      { label: 'Card activation', to: '/support#activate' },
      { label: 'Warranty', to: '/support#warranty' },
      { label: 'Contact', to: '/support#contact' },
    ],
  },
];

export function Footer() {
  const { notify } = useStore();
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);

  const subscribe = (event: React.FormEvent) => {
    event.preventDefault();
    if (!email.includes('@')) return;
    setSent(true);
    notify('Welcome to the studio list.');
    setEmail('');
  };

  return (
    <footer className="footer">
      <div className="footer__top shell shell--wide">
        <div className="footer__brand">
          <Link to="/" className="wordmark wordmark--footer">
            <span className="wordmark__visa">VISA</span>
            <span className="wordmark__rule" aria-hidden="true" />
            <span className="wordmark__sub">Debit Studio</span>
          </Link>
          <p className="footer__blurb">
            Debit cards and small leather goods, made slowly in Zürich and Florence. Designed to be used
            every day and kept for years.
          </p>
          <form className="footer__form" onSubmit={subscribe}>
            <label htmlFor="footer-email" className="footer__form-label">
              Letters from the studio — four a year
            </label>
            <div className="footer__form-row">
              <input
                id="footer-email"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                placeholder="you@example.com"
                required
                autoComplete="email"
              />
              <button type="submit" aria-label="Subscribe">
                {sent ? (
                  <svg width="16" height="16" viewBox="0 0 16 16" aria-hidden="true">
                    <path d="M2 8.4l3.6 3.6L14 3.4" stroke="currentColor" strokeWidth="1.5" fill="none" strokeLinecap="round" />
                  </svg>
                ) : (
                  <svg width="16" height="16" viewBox="0 0 16 16" aria-hidden="true">
                    <path d="M2 8h11M9 4l4 4-4 4" stroke="currentColor" strokeWidth="1.2" fill="none" strokeLinecap="round" />
                  </svg>
                )}
              </button>
            </div>
          </form>
        </div>

        <div className="footer__cols">
          {COLUMNS.map((column) => (
            <div key={column.title} className="footer__col">
              <p className="footer__col-title">{column.title}</p>
              <ul>
                {column.links.map((link) => (
                  <li key={link.label}>
                    <Link to={link.to}>{link.label}</Link>
                  </li>
                ))}
              </ul>
            </div>
          ))}
          <div className="footer__col">
            <p className="footer__col-title">Studio</p>
            <address className="footer__address">
              Löwenstrasse 12
              <br />
              8001 Zürich
              <br />
              Switzerland
            </address>
            <a href="mailto:studio@visadebit.example" className="footer__mail">
              studio@visadebit.example
            </a>
          </div>
        </div>
      </div>

      <div className="footer__marquee" aria-hidden="true">
        <div className="footer__marquee-track">
          {Array.from({ length: 2 }).map((_, index) => (
            <span key={index} className="footer__marquee-run">
              <span className="serif">Made to be carried</span>
              <span className="footer__dot" />
              <span className="serif italic">Since 2019</span>
              <span className="footer__dot" />
              <span className="serif">Zürich · Florence</span>
              <span className="footer__dot" />
              <span className="serif italic">Shipped worldwide</span>
              <span className="footer__dot" />
            </span>
          ))}
        </div>
      </div>

      <div className="footer__bottom shell shell--wide">
        <p>© 2026 Visa Debit Studio. A demo storefront — no real payments are processed.</p>
        <div className="footer__legal">
          <Link to="/support">Privacy</Link>
          <Link to="/support">Terms</Link>
          <Link to="/support">Accessibility</Link>
          <span className="footer__payment" aria-label="Demo payment methods">
            <span>VISA</span>
            <span>Apple Pay</span>
            <span>Klarna</span>
          </span>
        </div>
      </div>
    </footer>
  );
}
