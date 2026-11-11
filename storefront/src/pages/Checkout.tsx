import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useStore } from '../context/StoreContext';
import { money, classNames } from '../lib/format';
import { CheckIcon } from '../components/CartDrawer';

const STEPS = ['Information', 'Shipping', 'Payment'] as const;

const SHIPPING_METHODS = [
  {
    id: 'express',
    label: 'DHL Express',
    eta: '2–3 business days',
    price: 8,
    freeOverThreshold: true,
    note: 'Complimentary over €150',
  },
  {
    id: 'priority',
    label: 'Priority overnight',
    eta: 'Next business day',
    price: 19,
    freeOverThreshold: false,
    note: 'Order before 14:00 CET',
  },
  {
    id: 'pickup',
    label: 'Boutique pickup',
    eta: 'Ready in 2 hours',
    price: 0,
    freeOverThreshold: true,
    note: 'Löwenstrasse 12, Zürich',
  },
];

const COUNTRIES = [
  { code: 'DE', name: 'Germany', prefix: '+49' },
  { code: 'CH', name: 'Switzerland', prefix: '+41' },
  { code: 'FR', name: 'France', prefix: '+33' },
  { code: 'IT', name: 'Italy', prefix: '+39' },
  { code: 'NL', name: 'Netherlands', prefix: '+31' },
  { code: 'SE', name: 'Sweden', prefix: '+46' },
  { code: 'DK', name: 'Denmark', prefix: '+45' },
  { code: 'ES', name: 'Spain', prefix: '+34' },
  { code: 'GB', name: 'United Kingdom', prefix: '+44' },
  { code: 'US', name: 'United States', prefix: '+1' },
];

interface FormState {
  email: string;
  firstName: string;
  lastName: string;
  address: string;
  apartment: string;
  city: string;
  postal: string;
  country: string;
  phone: string;
  shipping: string;
  gift: boolean;
  giftNote: string;
  card: string;
  expiry: string;
  cvc: string;
  cardName: string;
  billingSame: boolean;
}

const initialForm: FormState = {
  email: '',
  firstName: '',
  lastName: '',
  address: '',
  apartment: '',
  city: '',
  postal: '',
  country: 'DE',
  phone: '',
  shipping: 'express',
  gift: false,
  giftNote: '',
  card: '',
  expiry: '',
  cvc: '',
  cardName: '',
  billingSame: true,
};

export function Checkout() {
  const { cart, notify } = useStore();
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [form, setForm] = useState<FormState>(initialForm);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [placing, setPlacing] = useState(false);
  const topRef = useRef<HTMLDivElement | null>(null);

  const shippingMethod = SHIPPING_METHODS.find((m) => m.id === form.shipping) ?? SHIPPING_METHODS[0];
  const afterDiscount = cart.totals.subtotal - cart.totals.discount;
  const qualifiesForFreeShipping = afterDiscount >= cart.freeShippingThreshold;
  // Only the standard express service is waived over the threshold — an
  // overnight upgrade is always chargeable.
  const finalShipping =
    shippingMethod.freeOverThreshold && qualifiesForFreeShipping ? 0 : shippingMethod.price;
  const total = Math.round((afterDiscount + finalShipping + cart.totals.tax) * 100) / 100;

  useEffect(() => {
    window.scrollTo({ top: 0, behavior: 'instant' as ScrollBehavior });
  }, [step]);

  useEffect(() => {
    document.title = 'Checkout · Visa Debit Studio';
  }, []);

  const country = useMemo(() => COUNTRIES.find((c) => c.code === form.country) ?? COUNTRIES[0], [form.country]);

  const update = <K extends keyof FormState>(key: K, value: FormState[K]) => {
    setForm((current) => ({ ...current, [key]: value }));
    setErrors((current) => {
      if (!current[key as string]) return current;
      const next = { ...current };
      delete next[key as string];
      return next;
    });
  };

  const validateInformation = () => {
    const next: Record<string, string> = {};
    if (!/^[^\s@]+@[^\s@]+\.[a-z]{2,}$/i.test(form.email)) next.email = 'Enter a valid email address';
    if (form.firstName.trim().length < 2) next.firstName = 'Required';
    if (form.lastName.trim().length < 2) next.lastName = 'Required';
    if (form.address.trim().length < 4) next.address = 'Enter your street address';
    if (form.city.trim().length < 2) next.city = 'Required';
    if (!/^[A-Za-z0-9][A-Za-z0-9 -]{2,9}$/.test(form.postal)) next.postal = 'Enter a valid postal code';
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const validatePayment = () => {
    const next: Record<string, string> = {};
    const digits = form.card.replace(/\s/g, '');
    if (digits.length < 15 || !/^\d+$/.test(digits)) next.card = 'Enter a 16-digit card number';
    if (!/^(0[1-9]|1[0-2])\/\d{2}$/.test(form.expiry)) next.expiry = 'MM/YY';
    if (!/^\d{3,4}$/.test(form.cvc)) next.cvc = '3–4 digits';
    if (form.cardName.trim().length < 3) next.cardName = 'Name on card required';
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const goNext = () => {
    if (step === 0 && !validateInformation()) return;
    if (step === 2) {
      if (!validatePayment()) return;
      placeOrder();
      return;
    }
    setStep((s) => Math.min(2, s + 1));
  };

  const placeOrder = () => {
    setPlacing(true);
    window.setTimeout(() => {
      const order = {
        number: `VB-${Math.floor(100000 + Math.random() * 899999)}`,
        email: form.email,
        name: `${form.firstName} ${form.lastName}`,
        address: `${form.address}${form.apartment ? `, ${form.apartment}` : ''}, ${form.postal} ${form.city}, ${country.name}`,
        shipping: shippingMethod.label,
        total,
        items: cart.lines,
      };
      sessionStorage.setItem('visa-last-order', JSON.stringify(order));
      cart.clear();
      setPlacing(false);
      navigate('/order-confirmed');
    }, 1400);
  };

  if (cart.lines.length === 0 && !placing) {
    return (
      <main id="main" className="checkout-empty">
        <div className="shell shell--narrow">
          <span className="eyebrow">Checkout</span>
          <h1 className="display">Your bag is empty</h1>
          <p className="lede">Add a card or a sleeve and we'll get it wrapped and on its way.</p>
          <Link to="/shop" className="btn">
            Shop all products
          </Link>
        </div>
      </main>
    );
  }

  return (
    <main id="main" className="checkout">
      <div ref={topRef} />
      <div className="checkout__bar shell shell--wide">
        <Link to="/" className="wordmark wordmark--sm" aria-label="Visa Debit home">
          <span className="wordmark__visa">VISA</span>
          <span className="wordmark__rule" aria-hidden="true" />
          <span className="wordmark__sub">Debit Studio</span>
        </Link>
        <ol className="steps">
          {STEPS.map((label, index) => (
            <li
              key={label}
              className={classNames('steps__item', index === step && 'is-current', index < step && 'is-done')}
            >
              <button
                type="button"
                onClick={() => index < step && setStep(index)}
                disabled={index > step}
                aria-current={index === step ? 'step' : undefined}
              >
                <span className="steps__num">{index < step ? <CheckIcon /> : index + 1}</span>
                <span className="steps__label">{label}</span>
              </button>
            </li>
          ))}
        </ol>
        <span className="checkout__secure">
          <svg width="12" height="12" viewBox="0 0 14 14" aria-hidden="true">
            <rect x="2.5" y="6" width="9" height="6.5" rx="1" stroke="currentColor" strokeWidth="1" fill="none" />
            <path d="M4.6 6V4.4a2.4 2.4 0 014.8 0V6" stroke="currentColor" strokeWidth="1" fill="none" />
          </svg>
          Secure demo checkout
        </span>
      </div>

      <div className="checkout__layout shell shell--wide">
        <div className="checkout__main">
          {step === 0 && (
            <section className="co-step">
              <header className="co-step__head">
                <h1 className="display co-step__title">Where should it go?</h1>
                <p className="body-muted">
                  We ship worldwide from Zürich. You'll get a tracking link the moment it leaves the studio.
                </p>
              </header>

              <div className="field">
                <label htmlFor="email">Email</label>
                <input
                  id="email"
                  type="email"
                  value={form.email}
                  onChange={(e) => update('email', e.target.value)}
                  placeholder="you@example.com"
                  autoComplete="email"
                  className={classNames(errors.email && 'has-error')}
                  aria-invalid={Boolean(errors.email)}
                />
                {errors.email && <p className="field__error">{errors.email}</p>}
              </div>

              <div className="field-row">
                <div className="field">
                  <label htmlFor="firstName">First name</label>
                  <input
                    id="firstName"
                    value={form.firstName}
                    onChange={(e) => update('firstName', e.target.value)}
                    autoComplete="given-name"
                    className={classNames(errors.firstName && 'has-error')}
                  />
                  {errors.firstName && <p className="field__error">{errors.firstName}</p>}
                </div>
                <div className="field">
                  <label htmlFor="lastName">Last name</label>
                  <input
                    id="lastName"
                    value={form.lastName}
                    onChange={(e) => update('lastName', e.target.value)}
                    autoComplete="family-name"
                    className={classNames(errors.lastName && 'has-error')}
                  />
                  {errors.lastName && <p className="field__error">{errors.lastName}</p>}
                </div>
              </div>

              <div className="field">
                <label htmlFor="address">Street address</label>
                <input
                  id="address"
                  value={form.address}
                  onChange={(e) => update('address', e.target.value)}
                  placeholder="Löwenstrasse 12"
                  autoComplete="address-line1"
                  className={classNames(errors.address && 'has-error')}
                />
                {errors.address && <p className="field__error">{errors.address}</p>}
              </div>

              <div className="field">
                <label htmlFor="apartment">
                  Apartment, suite <span className="field__optional">optional</span>
                </label>
                <input
                  id="apartment"
                  value={form.apartment}
                  onChange={(e) => update('apartment', e.target.value)}
                  autoComplete="address-line2"
                />
              </div>

              <div className="field-row field-row--three">
                <div className="field">
                  <label htmlFor="postal">Postal code</label>
                  <input
                    id="postal"
                    value={form.postal}
                    onChange={(e) => update('postal', e.target.value)}
                    autoComplete="postal-code"
                    className={classNames(errors.postal && 'has-error')}
                  />
                  {errors.postal && <p className="field__error">{errors.postal}</p>}
                </div>
                <div className="field">
                  <label htmlFor="city">City</label>
                  <input
                    id="city"
                    value={form.city}
                    onChange={(e) => update('city', e.target.value)}
                    autoComplete="address-level2"
                    className={classNames(errors.city && 'has-error')}
                  />
                  {errors.city && <p className="field__error">{errors.city}</p>}
                </div>
                <div className="field">
                  <label htmlFor="country">Country</label>
                  <div className="select-wrap">
                    <select
                      id="country"
                      value={form.country}
                      onChange={(e) => update('country', e.target.value)}
                      autoComplete="country"
                    >
                      {COUNTRIES.map((option) => (
                        <option key={option.code} value={option.code}>
                          {option.name}
                        </option>
                      ))}
                    </select>
                    <Chevron />
                  </div>
                </div>
              </div>

              <div className="field">
                <label htmlFor="phone">
                  Phone <span className="field__optional">for delivery updates</span>
                </label>
                <div className="input-prefix">
                  <span>{country.prefix}</span>
                  <input
                    id="phone"
                    type="tel"
                    value={form.phone}
                    onChange={(e) => update('phone', e.target.value.replace(/[^\d -]/g, ''))}
                    autoComplete="tel-national"
                    placeholder="30 1234 5678"
                  />
                </div>
              </div>

              <div className="co-step__actions">
                <Link to="/shop" className="link-underline">
                  Continue shopping
                </Link>
                <button type="button" className="btn" onClick={goNext}>
                  Continue to shipping
                </button>
              </div>
            </section>
          )}

          {step === 1 && (
            <section className="co-step">
              <header className="co-step__head">
                <h1 className="display co-step__title">How soon?</h1>
                <p className="body-muted">Everything is packed in the studio and handed to DHL the same working day.</p>
              </header>

              <div className="shipping-methods">
                {SHIPPING_METHODS.map((method) => {
                  const active = form.shipping === method.id;
                  const isFree =
                    method.price === 0 || (method.freeOverThreshold && qualifiesForFreeShipping);
                  const cost = isFree ? 'Free' : money(method.price);
                  return (
                    <label key={method.id} className={classNames('ship-method', active && 'is-active')}>
                      <input
                        type="radio"
                        name="shipping"
                        value={method.id}
                        checked={active}
                        onChange={() => update('shipping', method.id)}
                      />
                      <span className="ship-method__radio" aria-hidden="true" />
                      <span className="ship-method__text">
                        <span className="ship-method__label">{method.label}</span>
                        <span className="ship-method__eta">
                          {method.eta} · {method.note}
                        </span>
                      </span>
                      <span className="ship-method__price">{cost}</span>
                    </label>
                  );
                })}
              </div>

              <div className="gift-block">
                <label className="checkbox-row">
                  <input type="checkbox" checked={form.gift} onChange={(e) => update('gift', e.target.checked)} />
                  <span className="checkbox-row__box" aria-hidden="true">
                    <svg width="9" height="9" viewBox="0 0 12 12">
                      <path d="M1.5 6.4l3 3L10.5 2.5" stroke="currentColor" strokeWidth="1.6" fill="none" strokeLinecap="round" />
                    </svg>
                  </span>
                  <span>
                    This is a gift <span className="body-muted">— no invoice inside, hand-written card</span>
                  </span>
                </label>
                {form.gift && (
                  <div className="field gift-block__note">
                    <label htmlFor="giftNote">Message on the card</label>
                    <textarea
                      id="giftNote"
                      rows={3}
                      maxLength={180}
                      value={form.giftNote}
                      onChange={(e) => update('giftNote', e.target.value)}
                      placeholder="For a year of good coffee…"
                    />
                    <span className="field__hint">{form.giftNote.length}/180</span>
                  </div>
                )}
              </div>

              <div className="co-note">
                <p className="co-note__title">Delivery to</p>
                <p className="body-muted">
                  {form.firstName} {form.lastName} · {form.address}
                  {form.apartment ? `, ${form.apartment}` : ''}, {form.postal} {form.city}, {country.name}
                </p>
                <button type="button" className="co-note__edit" onClick={() => setStep(0)}>
                  Edit
                </button>
              </div>

              <div className="co-step__actions">
                <button type="button" className="link-underline" onClick={() => setStep(0)}>
                  Back
                </button>
                <button type="button" className="btn" onClick={goNext}>
                  Continue to payment
                </button>
              </div>
            </section>
          )}

          {step === 2 && (
            <section className="co-step">
              <header className="co-step__head">
                <h1 className="display co-step__title">Payment</h1>
                <p className="body-muted">
                  This is a demonstration storefront. No card is charged, and nothing is sent anywhere.
                </p>
              </header>

              <div className="pay-methods">
                <button type="button" className="pay-method is-active">
                  <span className="pay-method__brand">VISA</span>
                  Debit or credit
                </button>
                <button type="button" className="pay-method">
                  <span className="pay-method__brand pay-method__brand--apple">Apple Pay</span>
                </button>
                <button type="button" className="pay-method">
                  <span className="pay-method__brand">Klarna</span>
                </button>
              </div>

              <div className="pay-card">
                <div className="field">
                  <label htmlFor="card">Card number</label>
                  <input
                    id="card"
                    inputMode="numeric"
                    value={form.card}
                    onChange={(e) => {
                      const digits = e.target.value.replace(/\D/g, '').slice(0, 16);
                      update('card', digits.replace(/(.{4})/g, '$1 ').trim());
                    }}
                    placeholder="4242 4242 4242 4242"
                    autoComplete="cc-number"
                    className={classNames(errors.card && 'has-error')}
                  />
                  {errors.card && <p className="field__error">{errors.card}</p>}
                </div>
                <div className="field-row">
                  <div className="field">
                    <label htmlFor="expiry">Expiry</label>
                    <input
                      id="expiry"
                      inputMode="numeric"
                      value={form.expiry}
                      onChange={(e) => {
                        const digits = e.target.value.replace(/\D/g, '').slice(0, 4);
                        update('expiry', digits.length > 2 ? `${digits.slice(0, 2)}/${digits.slice(2)}` : digits);
                      }}
                      placeholder="MM/YY"
                      autoComplete="cc-exp"
                      className={classNames(errors.expiry && 'has-error')}
                    />
                    {errors.expiry && <p className="field__error">{errors.expiry}</p>}
                  </div>
                  <div className="field">
                    <label htmlFor="cvc">CVC</label>
                    <input
                      id="cvc"
                      inputMode="numeric"
                      value={form.cvc}
                      onChange={(e) => update('cvc', e.target.value.replace(/\D/g, '').slice(0, 4))}
                      placeholder="123"
                      autoComplete="cc-csc"
                      className={classNames(errors.cvc && 'has-error')}
                    />
                    {errors.cvc && <p className="field__error">{errors.cvc}</p>}
                  </div>
                </div>
                <div className="field">
                  <label htmlFor="cardName">Name on card</label>
                  <input
                    id="cardName"
                    value={form.cardName}
                    onChange={(e) => update('cardName', e.target.value)}
                    placeholder="A. Marchetti"
                    autoComplete="cc-name"
                    className={classNames(errors.cardName && 'has-error')}
                  />
                  {errors.cardName && <p className="field__error">{errors.cardName}</p>}
                </div>
                <label className="checkbox-row checkbox-row--sm">
                  <input
                    type="checkbox"
                    checked={form.billingSame}
                    onChange={(e) => update('billingSame', e.target.checked)}
                  />
                  <span className="checkbox-row__box" aria-hidden="true">
                    <svg width="9" height="9" viewBox="0 0 12 12">
                      <path d="M1.5 6.4l3 3L10.5 2.5" stroke="currentColor" strokeWidth="1.6" fill="none" strokeLinecap="round" />
                    </svg>
                  </span>
                  <span>Billing address is the same as shipping</span>
                </label>
              </div>

              <div className="co-step__actions">
                <button type="button" className="link-underline" onClick={() => setStep(1)}>
                  Back
                </button>
                <button type="button" className="btn" onClick={goNext} disabled={placing}>
                  {placing ? (
                    <>
                      <span className="spinner" aria-hidden="true" /> Placing order
                    </>
                  ) : (
                    <>Pay {money(total)}</>
                  )}
                </button>
              </div>
              <p className="co-fineprint">
                By placing this order you agree to our terms of sale. This is a demo — no payment will be taken
                and no data leaves your browser. Try card 4242 4242 4242 4242.
              </p>
            </section>
          )}
        </div>

        {/* ------------- Summary ------------- */}
        <aside className="co-summary" aria-label="Order summary">
          <div className="co-summary__head">
            <p className="eyebrow eyebrow--plain">Order summary</p>
            <span className="co-summary__count">
              {cart.totals.itemCount} item{cart.totals.itemCount === 1 ? '' : 's'}
            </span>
          </div>
          <ul className="co-summary__lines">
            {cart.lines.map((line) => (
              <li key={line.key}>
                <div className="co-summary__thumb">
                  <img src={line.image} alt="" loading="lazy" />
                  <span className="co-summary__qty">{line.qty}</span>
                </div>
                <div className="co-summary__line-text">
                  <span className="co-summary__line-name">{line.name}</span>
                  <span className="co-summary__line-variant">
                    {line.variantLabel}
                    {line.monogram ? ` · “${line.monogram}”` : ''}
                  </span>
                </div>
                <span className="co-summary__line-price">{money(line.unitPrice * line.qty)}</span>
              </li>
            ))}
          </ul>

          {cart.promo === null && (
            <form
              className="promo-form promo-form--summary"
              onSubmit={(event) => {
                event.preventDefault();
                const input = (event.currentTarget.elements.namedItem('code') as HTMLInputElement).value;
                const result = cart.applyPromo(input);
                if (result.ok) notify(`Code applied — ${Math.round(result.percent * 100)}% off`);
              }}
            >
              <input name="code" placeholder="Promo code" aria-label="Promo code" />
              <button type="submit">Apply</button>
            </form>
          )}
          {cart.promo && (
            <div className="promo-applied">
              <span>
                <CheckIcon /> {cart.promo} applied
              </span>
              <button type="button" onClick={cart.removePromo}>
                Remove
              </button>
            </div>
          )}

          <dl className="summary summary--co">
            <div>
              <dt>Subtotal</dt>
              <dd>{money(cart.totals.subtotal)}</dd>
            </div>
            {cart.totals.discount > 0 && (
              <div className="summary__discount">
                <dt>Discount</dt>
                <dd>−{money(cart.totals.discount)}</dd>
              </div>
            )}
            <div>
              <dt>Shipping · {shippingMethod.label}</dt>
              <dd>{finalShipping === 0 ? 'Free' : money(finalShipping)}</dd>
            </div>
            <div>
              <dt>Estimated tax</dt>
              <dd>{money(cart.totals.tax)}</dd>
            </div>
            <div className="summary__total">
              <dt>Total</dt>
              <dd>{money(total)}</dd>
            </div>
          </dl>

          <div className="co-summary__assurance">
            <p>
              <CheckIcon /> 30-day free returns
            </p>
            <p>
              <CheckIcon /> Five-year warranty
            </p>
            <p>
              <CheckIcon /> Packed by hand in Zürich
            </p>
          </div>
        </aside>
      </div>
    </main>
  );
}

function Chevron() {
  return (
    <svg width="10" height="10" viewBox="0 0 12 12" aria-hidden="true" className="select-wrap__chev">
      <path d="M2 4.5L6 8.5L10 4.5" stroke="currentColor" strokeWidth="1.2" fill="none" strokeLinecap="round" />
    </svg>
  );
}
