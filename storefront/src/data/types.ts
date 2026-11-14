export type CollectionId = 'essential' | 'travel' | 'infinite' | 'atelier';

export type Badge = 'New' | 'Best seller' | 'Limited' | "Editor's pick" | 'Set';

export interface Variant {
  id: string;
  label: string;
  finish: string;
  priceDelta: number;
  stock: number;
}

export interface Review {
  id: string;
  author: string;
  location: string;
  rating: number;
  title: string;
  body: string;
  date: string;
  verified: boolean;
  helpful: number;
}

export interface Product {
  id: string;
  slug: string;
  name: string;
  subtitle: string;
  collection: CollectionId;
  price: number;
  compareAt?: number;
  badge?: Badge;
  images: string[];
  variants: Variant[];
  material: string;
  materialGroup: MaterialGroup;
  weightGrams: number;
  thicknessMm: number;
  rating: number;
  reviewCount: number;
  reviews: Review[];
  summary: string;
  story: string[];
  features: { title: string; body: string }[];
  specs: { label: string; value: string }[];
  includes: string[];
  leadTime: string;
  featuredRank: number;
  releasedAt: string;
  tags: string[];
}

export type MaterialGroup =
  | 'polymer'
  | 'metal'
  | 'leather'
  | 'composite';

export interface Collection {
  id: CollectionId;
  name: string;
  eyebrow: string;
  description: string;
  image: string;
  accent: string;
}
