import { Link } from 'react-router-dom';

export function NotFound() {
  return (
    <main id="main" className="notfound">
      <span className="eyebrow">Error 404</span>
      <h1 className="display notfound__title">
        This page has
        <br />
        <em>slipped out</em>
      </h1>
      <p className="lede">The link may be old, or the card may have sold out. Both happen.</p>
      <div className="row">
        <Link to="/" className="btn">
          Back home
        </Link>
        <Link to="/shop" className="btn btn--ghost">
          Shop all
        </Link>
      </div>
    </main>
  );
}
