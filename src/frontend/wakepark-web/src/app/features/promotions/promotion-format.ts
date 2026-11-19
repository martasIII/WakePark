import { num, type AdminCategoryDto, type AdminProductDto, type CatalogAdminDto } from '../../core/api/api';

/** `2026-12-24` → „24. 12. 2026“ */
export function czechDate(isoDate: string): string {
  const [y, m, d] = isoDate.split('-').map(Number);
  return `${d}. ${m}. ${y}`;
}

/** „1. 12. – 24. 12. 2026“, u jednodenní akce jen den. */
export function dateRange(from: string, to: string): string {
  if (from === to) {
    return czechDate(from);
  }

  return from.slice(0, 4) === to.slice(0, 4)
    ? `${czechDate(from).replace(/ \d{4}$/, '')} – ${czechDate(to)}`
    : `${czechDate(from)} – ${czechDate(to)}`;
}

/** „1 490 Kč“ */
export function czk(amount: number): string {
  return `${new Intl.NumberFormat('cs-CZ', { maximumFractionDigits: 2 }).format(amount)} Kč`;
}

/** Cena po slevě stejně jako na serveru: celé koruny nahoru. */
export function discounted(price: number, percent: number): number {
  return Math.ceil((price * (100 - percent)) / 100);
}

/** Název kategorie s cestou: „Vouchery › Dárkové“. */
export function categoryLabel(category: AdminCategoryDto, all: readonly AdminCategoryDto[]): string {
  const parent = category.parentId ? all.find((c) => c.id === category.parentId) : undefined;
  return parent ? `${parent.name['cs']} › ${category.name['cs']}` : category.name['cs'];
}

/** Aktivní produkty e-shopu v kategorii a jejích podkategoriích, s dnešní cenou. */
export function eshopProductsIn(catalog: CatalogAdminDto, categoryId: string): AdminProductDto[] {
  const ids = new Set([categoryId, ...catalog.categories.filter((c) => c.parentId === categoryId).map((c) => c.id)]);
  return catalog.products.filter((p) => ids.has(p.categoryId) && p.isActive && p.visibility.eshop && p.currentPrice && num(p.currentPrice.unitPrice) > 0);
}
