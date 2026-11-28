import type { Review } from './types';

/**
 * A shared pool of verified-purchase reviews. Each product surfaces a
 * hand-picked subset, while `reviewCount` reflects the wider (seeded) history
 * shown on the storefront — the way a real catalogue reads.
 */
export const reviewPool: Review[] = [
  {
    id: 'r-01',
    author: 'Elise Marchetti',
    location: 'Copenhagen, DK',
    rating: 5,
    title: 'Feels like an heirloom',
    body:
      'I have carried a plastic debit card for fifteen years without a second thought. Three days with this and every other card in my wallet feels like a hotel key. The weight is the thing — you notice it at the counter, and somehow that makes the whole transaction feel considered.',
    date: '2026-09-18',
    verified: true,
    helpful: 84,
  },
  {
    id: 'r-02',
    author: 'Tobias Renner',
    location: 'Berlin, DE',
    rating: 5,
    title: 'The packaging alone',
    body:
      'Arrived in a linen sleeve inside a rigid card box, with a hand-numbered certificate. My partner thought I had bought jewellery. Setup took four minutes in the app and the digital card was live before the metal one left the box.',
    date: '2026-10-02',
    verified: true,
    helpful: 61,
  },
  {
    id: 'r-03',
    author: 'Naomi Okafor',
    location: 'London, UK',
    rating: 4,
    title: 'Beautiful, slightly heavier than expected',
    body:
      'Genuinely lovely object. I docked a star only because the weight is noticeable in a slim wallet — it dominates the stack. Contactless works flawlessly through leather, which was my main worry with a metal card.',
    date: '2026-08-27',
    verified: true,
    helpful: 47,
  },
  {
    id: 'r-04',
    author: 'Marcus Feld',
    location: 'Zürich, CH',
    rating: 5,
    title: 'Worth it for the travel perks',
    body:
      'Used it across four countries in six weeks. No FX markup, no surprise fees, and the app converted currency at a rate that matched what I saw on the screen. The lounge access at Zurich paid for the card in one trip.',
    date: '2026-09-30',
    verified: true,
    helpful: 96,
  },
  {
    id: 'r-05',
    author: 'Priya Raman',
    location: 'Singapore, SG',
    rating: 5,
    title: 'The finish is unreal in daylight',
    body:
      'Photos do not do the surface justice. It has a fine brushed texture that catches light without looking flashy. I have had three separate people ask about it at coffee counters, which is more social interaction than I signed up for.',
    date: '2026-10-11',
    verified: true,
    helpful: 73,
  },
  {
    id: 'r-06',
    author: 'Antoine Lefèvre',
    location: 'Lyon, FR',
    rating: 4,
    title: 'Excellent card, slow engraving',
    body:
      'I ordered the monogrammed version and it took eleven days rather than the five quoted. The wait was worth it — the engraving is deep and precise rather than lasered on top. Support was responsive and credited the shipping without me asking.',
    date: '2026-07-22',
    verified: true,
    helpful: 38,
  },
  {
    id: 'r-07',
    author: 'Sofia Lindqvist',
    location: 'Stockholm, SE',
    rating: 5,
    title: 'Bought two, no regrets',
    body:
      'One for me, one for my brother as a graduation gift. The gift wrapping is genuinely beautiful — heavy paper, wax seal, no branding shouting at you. He texted a photo of it before he had even opened it.',
    date: '2026-09-05',
    verified: true,
    helpful: 52,
  },
  {
    id: 'r-08',
    author: 'Daniel Whitmore',
    location: 'Melbourne, AU',
    rating: 3,
    title: 'Gorgeous but small scratches show',
    body:
      'The finish is stunning and the weight is perfect. That said, after two months of daily pocket carry there are visible micro-scratches on the face. Not a defect exactly — patina, if you are generous. I would have liked a sleeve included at this price.',
    date: '2026-06-14',
    verified: true,
    helpful: 109,
  },
  {
    id: 'r-09',
    author: 'Hana Kobayashi',
    location: 'Kyoto, JP',
    rating: 5,
    title: 'The quietest luxury purchase I have made',
    body:
      'No logo, no shine, nothing to prove. It simply feels correct in the hand. The matte surface does not smudge the way gloss cards do, so it still looks new after weeks of use.',
    date: '2026-10-19',
    verified: true,
    helpful: 67,
  },
  {
    id: 'r-10',
    author: 'Ruben de Vries',
    location: 'Amsterdam, NL',
    rating: 5,
    title: 'Replaced my everyday card entirely',
    body:
      'I intended to keep my old bank card as a backup. Four months later it is in a drawer. Apple Pay setup was instant, and the app freezes the physical card in two taps when I travel.',
    date: '2026-08-08',
    verified: true,
    helpful: 71,
  },
  {
    id: 'r-11',
    author: 'Clara Bennington',
    location: 'New York, US',
    rating: 4,
    title: 'Lovely, though I wanted a longer cable',
    body:
      'Everything about the presentation is considered. My only note is that the travel folio is a touch too deep for two cards — it fits, but the flap bends slightly. With one card it is perfect.',
    date: '2026-09-12',
    verified: true,
    helpful: 29,
  },
  {
    id: 'r-12',
    author: 'Idris Achterberg',
    location: 'Rotterdam, NL',
    rating: 5,
    title: 'Leather smells like a good bookshop',
    body:
      'Vegetable-tanned, thick but pliable, and the stitching is even all the way around. It has darkened beautifully in six weeks. This is the sort of thing I will still be using in a decade.',
    date: '2026-07-30',
    verified: true,
    helpful: 88,
  },
  {
    id: 'r-13',
    author: 'Camille Rousseau',
    location: 'Geneva, CH',
    rating: 5,
    title: 'Gifted to my father',
    body:
      'He is impossible to shop for. He carried it out of the box, put his old card away, and has mentioned the weight twice since. High praise from a man who communicates mostly in nods.',
    date: '2026-10-06',
    verified: true,
    helpful: 44,
  },
  {
    id: 'r-14',
    author: 'Owen Halvorsen',
    location: 'Oslo, NO',
    rating: 4,
    title: 'Great card — the app is the real star',
    body:
      'The metal is what you buy it for, but the spend insights are what I use daily. Categories are accurate for once, and the monthly summary is clear enough that I cancelled a budgeting subscription.',
    date: '2026-08-19',
    verified: true,
    helpful: 58,
  },
  {
    id: 'r-15',
    author: 'Meera Kapoor',
    location: 'Dubai, AE',
    rating: 5,
    title: 'Concierge actually answers',
    body:
      'I expected a chatbot. Got a person, in under ninety seconds, at 11pm, who rebooked a flight for me during a delay. The card could be plastic and I would still keep the account.',
    date: '2026-09-24',
    verified: true,
    helpful: 132,
  },
  {
    id: 'r-16',
    author: 'Lucas Pinheiro',
    location: 'Lisbon, PT',
    rating: 5,
    title: 'Perfect weight, perfect pocket',
    body:
      'I was worried a metal card would feel like a novelty. It does not — it feels like a tool that was designed by someone who carries one. The slightly softened corners are a thoughtful touch.',
    date: '2026-10-14',
    verified: true,
    helpful: 36,
  },
  {
    id: 'r-17',
    author: 'Freya Sandberg',
    location: 'Aarhus, DK',
    rating: 4,
    title: 'Sizing note for the sleeve',
    body:
      'Beautiful object, and the interior is remarkably soft. Note that a metal card plus a transit pass is the comfortable maximum — three cards makes it bulge. Still, the leather quality is exceptional for the price.',
    date: '2026-06-29',
    verified: true,
    helpful: 41,
  },
  {
    id: 'r-18',
    author: 'Julian Osei',
    location: 'Toronto, CA',
    rating: 5,
    title: 'The card that made me care about cards',
    body:
      'I did not know a debit card could be an object of design. The brushed edge detail, the absence of any printed noise, the way it lands on a counter. Absurd, honestly. I love it.',
    date: '2026-10-01',
    verified: true,
    helpful: 78,
  },
  {
    id: 'r-19',
    author: 'Beatrice Conti',
    location: 'Milan, IT',
    rating: 5,
    title: 'Bought it for the look, stayed for the support',
    body:
      'A questionable purchase that turned out to be a good one. When a restaurant double-charged me, the dispute was resolved in two days without a single phone call. My old bank took eleven weeks.',
    date: '2026-08-03',
    verified: true,
    helpful: 65,
  },
  {
    id: 'r-20',
    author: 'Theo Nakamura',
    location: 'Vancouver, CA',
    rating: 4,
    title: 'Ships fast, runs warm in the app',
    body:
      'Ordered Monday, arrived Thursday. Everything as described. The app occasionally takes a moment to load the card image, which is cosmetic — payments have never failed.',
    date: '2026-09-08',
    verified: true,
    helpful: 22,
  },
  {
    id: 'r-21',
    author: 'Amara Diallo',
    location: 'Paris, FR',
    rating: 5,
    title: 'Monogram is beautifully done',
    body:
      'Ordered with initials on the back edge. Small, discreet, exactly where my thumb rests. It makes an already personal object feel genuinely mine. Would order again as a gift.',
    date: '2026-07-16',
    verified: true,
    helpful: 50,
  },
  {
    id: 'r-22',
    author: 'Peter Lindgren',
    location: 'Gothenburg, SE',
    rating: 4,
    title: 'Solid, with one small caveat',
    body:
      'The card is excellent and the packaging is museum-grade. One caution: some older parking meters and train ticket machines struggle to read a metal card in a wallet. Tapping the bare card works fine.',
    date: '2026-05-27',
    verified: true,
    helpful: 74,
  },
  {
    id: 'r-23',
    author: 'Isabel Ferreira',
    location: 'Porto, PT',
    rating: 5,
    title: 'The set is the way to buy',
    body:
      'Went for the bundle with the leather sleeve and I would not separate them. The card lives in it, the sleeve has aged to a deep chestnut, and the whole thing feels like one object rather than two.',
    date: '2026-09-27',
    verified: true,
    helpful: 89,
  },
  {
    id: 'r-24',
    author: 'Johan Berg',
    location: 'Bergen, NO',
    rating: 5,
    title: 'Two years in, still like new',
    body:
      'Updating a review I wrote after purchase. Two years of daily use, a few small marks on the face, and it still looks intentional rather than worn. The chip and contactless have never faltered.',
    date: '2026-04-11',
    verified: true,
    helpful: 143,
  },
  {
    id: 'r-25',
    author: 'Renata Alves',
    location: 'São Paulo, BR',
    rating: 4,
    title: 'Wanted a stand for my desk',
    body:
      'I keep it on my desk as much as in my wallet. A small display stand would be a welcome addition to the range — it deserves better than lying flat next to my keys.',
    date: '2026-10-09',
    verified: true,
    helpful: 18,
  },
  {
    id: 'r-26',
    author: 'Grace Whitfield',
    location: 'Edinburgh, UK',
    rating: 5,
    title: 'Feels considered at every step',
    body:
      'The card, the sleeve, the certificate, the app. None of it is loud and all of it is deliberate. It is the only card I have ever owned that I would describe as tasteful.',
    date: '2026-08-31',
    verified: true,
    helpful: 56,
  },
  {
    id: 'r-27',
    author: 'Samir Haddad',
    location: 'Beirut, LB',
    rating: 5,
    title: 'International use is effortless',
    body:
      'Six currencies in one month, no conversion surprises, and a clean monthly statement I could actually read. The exchange rate held up well against what the local machines offered.',
    date: '2026-07-05',
    verified: true,
    helpful: 62,
  },
  {
    id: 'r-28',
    author: 'Anna Kowalska',
    location: 'Kraków, PL',
    rating: 4,
    title: 'Understated in the best way',
    body:
      'I bought this hoping it would not look like a status symbol. It does not. It looks like a very well-made object, which is more than I can say for most things in my wallet.',
    date: '2026-10-16',
    verified: true,
    helpful: 33,
  },
  {
    id: 'r-29',
    author: 'Victor Eklund',
    location: 'Helsinki, FI',
    rating: 5,
    title: 'The weight sells it',
    body:
      'You hand it over and the cashier pauses. That is the entire review. Everything else — fees, app, support — has been as promised.',
    date: '2026-09-14',
    verified: true,
    helpful: 91,
  },
  {
    id: 'r-30',
    author: 'Leila Nasser',
    location: 'Montréal, CA',
    rating: 5,
    title: 'Bought as a gift, kept the second one',
    body:
      'Ordered two of the essential card — one to give, one to keep. The polymer is genuinely tactile, not plasticky, and the colour is a soft chalk rather than a stark white. No regrets about either.',
    date: '2026-06-21',
    verified: true,
    helpful: 39,
  },
];

export const pickReviews = (...ids: string[]): Review[] =>
  ids
    .map((id) => reviewPool.find((review) => review.id === id))
    .filter((review): review is Review => Boolean(review));
