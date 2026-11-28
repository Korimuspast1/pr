import type { Collection } from './types';

export const collections: Collection[] = [
  {
    id: 'essential',
    name: 'The Essential',
    eyebrow: 'Everyday',
    description:
      'Recycled-polymer cards for the rituals you repeat — coffee, groceries, the 7:42 train. Quietly made, endlessly dependable.',
    image: '/img/collection-essentials.jpg',
    accent: '#8C8577',
  },
  {
    id: 'travel',
    name: 'The Voyager',
    eyebrow: 'Travel',
    description:
      'Metal travel debit with zero foreign transaction fees, lounge access and instant multi-currency holding in 42 currencies.',
    image: '/img/collection-travel.jpg',
    accent: '#B08A4A',
  },
  {
    id: 'infinite',
    name: 'The Infinite',
    eyebrow: 'Signature',
    description:
      'Our heaviest, most considered debit card. Hand-finished metal, private concierge, and a limit of 250 numbered pieces per release.',
    image: '/img/collection-infinite.jpg',
    accent: '#CBB07C',
  },
  {
    id: 'atelier',
    name: 'The Atelier',
    eyebrow: 'Objects',
    description:
      'Leather and brass objects made in small batches with our partner workshop in Tuscany — designed to carry the card you use most.',
    image: '/img/editorial-1.jpg',
    accent: '#9B6B43',
  },
];

export const collectionMap: Record<string, Collection> = Object.fromEntries(
  collections.map((c) => [c.id, c]),
);
