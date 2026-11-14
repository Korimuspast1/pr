# Visa Debit Studio — storefront

A production-ready, fully responsive e-commerce storefront for a fictional
**Visa Debit Studio**: hand-finished debit cards and small leather goods.

Built with React 18, TypeScript, Vite and React Router. No UI framework — the
design system is hand-written CSS with custom properties.

---

## Run it

```bash
cd storefront
npm install
npm run dev        # http://localhost:5173
```

```bash
npm run build      # typecheck + production bundle into dist/
npm run preview    # serve the production build
npm run typecheck  # tsc --noEmit
```

Requires Node 18+. The only runtime dependencies are React, React DOM and React
Router; fonts (Instrument Serif + Inter) are self-hosted via `@fontsource`, so the
site renders identically offline with no third-party requests.

---

## What's here

| Route | Page |
| --- | --- |
| `/` | Home — hero, four collections, featured grid, studio editorial, dark spotlight, best sellers, reviews, closing CTA |
| `/shop` | 12-product grid with faceted filters + sorting, all state in the URL |
| `/product/:slug` | Product detail — gallery, variants, monogram, specs, story, rating breakdown, reviews, related |
| `/collections` | Editorial overview of the four collections |
| `/checkout` | Three-step checkout with per-step validation and sticky order summary |
| `/order-confirmed` | Order receipt with tracking timeline |
| `/about`, `/support` | Brand story, FAQ, contact form |
| `*` | 404 |

### Commerce behaviour

- **Persistent cart** — lines are stored in `localStorage` (`visa-storefront-cart-v1`)
  and rehydrated on load, so the bag survives reloads and tab restarts.
- **Slide-out cart** — free-shipping progress meter, quantity steppers, remove,
  promo codes (`STUDIO15` = 15%, `WELCOME10` = 10%), cross-sell suggestions.
- **Variants & monogram** — each variant carries its own price delta and stock;
  a 3-character monogram becomes part of the cart line's identity.
- **Faceted filtering** — collection, material, price band and availability,
  combinable, URL-synced (`?collection=infinite&material=metal&sort=price-asc`)
  so any filtered view is linkable and survives a reload.
- **Checkout** — information → shipping → payment, with real inline validation,
  a chargeable overnight upgrade that is never silently waived, and an order
  number written to `sessionStorage` for the confirmation page.

### Design & engineering notes

- **Typography** — Instrument Serif for display, Inter for UI, on a fluid
  `clamp()` type scale.
- **Motion** — IntersectionObserver reveals, hover cross-fades on product cards,
  a marquee ticker and drawer/lightbox transitions. Everything collapses under
  `prefers-reduced-motion: reduce`.
- **Accessibility** — 0 WCAG 2.1 A/AA violations across all pages (axe-core),
  skip link, focus-visible rings, Escape to close overlays, `aria-live` on
  cart/validation feedback, labelled dialogs.
- **Responsive** — verified from 390 px to 1440 px with no horizontal overflow;
  filter rail becomes a bottom sheet, gallery becomes a swipeable thumbnail row.
- **Images** — art-directed, pre-optimised JPEGs in `public/img`; second image
  per product used for the hover state.

> **Demo notice.** No payment is processed and no data leaves the browser. Try
> card `4242 4242 4242 4242`, any future expiry, any CVC.

---

## Structure

```
storefront/
├── index.html
├── public/img/            # art-directed product + editorial imagery
└── src/
    ├── main.tsx           # fonts, styles, router
    ├── App.tsx            # shell: header, routes, drawer, overlays
    ├── context/           # StoreProvider (cart + drawer + toast + search)
    ├── lib/               # cart state machine, formatters
    ├── data/              # 12 products, 4 collections, 30 seeded reviews
    ├── components/        # Header, Footer, CartDrawer, ProductCard, …
    ├── pages/             # Home, Shop, Product, Collections, Checkout, …
    └── styles/            # base tokens, layout, page styles
```
