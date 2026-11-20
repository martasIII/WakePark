import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import {
  num,
  type AdminPromotionDto,
  type CatalogAdminDto,
  type PromotionRequest,
} from '../../core/api/api';
import { addDays } from '../../shared/park-time';
import { problemMessage } from '../../shared/problem';
import { categoryLabel, czk, discounted, eshopProductsIn } from './promotion-format';
import { PromotionsApi } from './promotions-api';

export interface PromotionDialogData {
  readonly catalog: CatalogAdminDto;
  /** Upravovaná akce; bez ní nová. */
  readonly promotion?: AdminPromotionDto;
}

const NAME_MAX_LENGTH = 60;
const MIN_PERCENT = 1;
const MAX_PERCENT = 90;

/**
 * Nová nebo upravená akční sleva (ADR 0014). Dole produkty, kterých se akce týká, s cenou
 * přesně tak, jak ji e-shop spočítá (na koruny nahoru). Produktům přímo v kategorii jde
 * procento přepsat (0 = bez slevy); změna výchozí slevy nebo kategorie vrátí všechny na
 * výchozí. Zavírá se novým katalogem, když se uložilo.
 */
@Component({
  selector: 'wp-promotion-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './promotion-dialog.html',
  styleUrl: './promotion-dialog.scss',
})
export class PromotionDialog {
  private readonly api = inject(PromotionsApi);
  private readonly ref = inject<MatDialogRef<PromotionDialog, CatalogAdminDto>>(MatDialogRef);
  protected readonly data = inject<PromotionDialogData>(MAT_DIALOG_DATA);

  protected readonly nameMaxLength = NAME_MAX_LENGTH;
  protected readonly minPercent = MIN_PERCENT;
  protected readonly maxPercent = MAX_PERCENT;
  protected readonly today = this.data.catalog.today;
  protected readonly isEdit = !!this.data.promotion;

  protected readonly categories = this.data.catalog.categories.map((category) => ({
    id: category.id,
    label: categoryLabel(category, this.data.catalog.categories),
  }));

  protected readonly nameCs = signal(this.data.promotion?.name['cs'] ?? '');
  protected readonly nameEn = signal(this.data.promotion?.name['en'] ?? '');
  protected readonly categoryId = signal(
    this.data.promotion?.categoryId ?? this.data.catalog.categories[0]?.id ?? '',
  );
  protected readonly percent = signal<number | null>(
    this.data.promotion ? num(this.data.promotion.discountPercent) : null,
  );
  protected readonly validFrom = signal(this.data.promotion?.validFrom ?? this.today);
  protected readonly validTo = signal(this.data.promotion?.validTo ?? addDays(this.today, 6));
  /** Vlastní procenta produktů: id produktu → procento. Jen produkty přímo v kategorii. */
  protected readonly overrides = signal<ReadonlyMap<string, number | null>>(
    new Map(
      (this.data.promotion?.productDiscounts ?? []).map((d) => [
        d.productId,
        num(d.discountPercent),
      ]),
    ),
  );
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly percentValid = computed(() => {
    const value = this.percent();
    return (
      value !== null && Number.isInteger(value) && value >= MIN_PERCENT && value <= MAX_PERCENT
    );
  });

  protected readonly rangeValid = computed(
    () => !!this.validFrom() && !!this.validTo() && this.validTo() >= this.validFrom(),
  );

  protected readonly overridesValid = computed(() =>
    [...this.overrides().values()].every(
      (value) => value !== null && Number.isInteger(value) && value >= 0 && value <= MAX_PERCENT,
    ),
  );

  protected readonly canSave = computed(
    () =>
      !!this.nameCs().trim() &&
      !!this.categoryId() &&
      this.percentValid() &&
      this.overridesValid() &&
      this.rangeValid() &&
      !this.saving(),
  );

  /**
   * Produkty e-shopu v kategorii (i podkategoriích) s procentem a cenou v akci. Procento jde
   * měnit jen u produktů přímo v kategorii; podkategorie mají vždy výchozí.
   */
  protected readonly preview = computed(() => {
    const percent = this.percentValid() ? (this.percent() as number) : null;
    const overrides = this.overrides();
    return eshopProductsIn(this.data.catalog, this.categoryId()).map((product) => {
      const regular = num(product.currentPrice?.unitPrice);
      const editable = product.categoryId === this.categoryId();
      const own =
        editable && overrides.has(product.id) ? (overrides.get(product.id) ?? null) : undefined;
      const effective = own !== undefined ? own : percent;
      const sale = effective === null ? null : discounted(regular, effective);
      return {
        id: product.id,
        name: product.name['cs'],
        editable,
        own: own !== undefined,
        percent: effective,
        regular: czk(regular),
        sale: sale === null ? null : czk(sale),
        same: sale === regular,
      };
    });
  });

  /** Kolik produktů má vlastní procento — kvůli upozornění, že změna výchozí je smaže. */
  protected readonly ownCount = computed(() => this.preview().filter((row) => row.own).length);

  /** Změna výchozí slevy vrátí všechny produkty na výchozí procento. */
  protected setPercent(value: number | null): void {
    this.percent.set(value);
    this.overrides.set(new Map());
  }

  protected setCategory(id: string): void {
    this.categoryId.set(id);
    this.overrides.set(new Map());
  }

  /** Procento u produktu; stejné jako výchozí = žádná výjimka. */
  protected setProductPercent(productId: string, value: number | null): void {
    const next = new Map(this.overrides());
    if (value !== null && value === this.percent()) {
      next.delete(productId);
    } else {
      next.set(productId, value);
    }
    this.overrides.set(next);
  }

  protected resetProduct(productId: string): void {
    const next = new Map(this.overrides());
    next.delete(productId);
    this.overrides.set(next);
  }

  protected setValidFrom(date: string): void {
    this.validFrom.set(date);
    if (date && this.validTo() < date) {
      this.validTo.set(date);
    }
  }

  protected async save(): Promise<void> {
    if (!this.canSave()) {
      return;
    }

    const name: Record<string, string> = { cs: this.nameCs().trim() };
    if (this.nameEn().trim()) {
      name['en'] = this.nameEn().trim();
    }

    const request: PromotionRequest = {
      name,
      categoryId: this.categoryId(),
      discountPercent: this.percent() as number,
      validFrom: this.validFrom(),
      validTo: this.validTo(),
      productDiscounts: this.preview()
        .filter((row) => row.own)
        .map((row) => ({ productId: row.id, discountPercent: row.percent as number })),
    };

    this.saving.set(true);
    this.error.set(null);
    try {
      const catalog = this.data.promotion
        ? await this.api.update(this.data.promotion.id, request)
        : await this.api.create(request);
      this.ref.close(catalog);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
