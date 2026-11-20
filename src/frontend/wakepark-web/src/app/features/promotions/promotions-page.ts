import { NgTemplateOutlet } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';

import { num, type AdminPromotionDto, type CatalogAdminDto } from '../../core/api/api';
import { problemMessage } from '../../shared/problem';
import { PromotionDialog, PromotionDialogData } from './promotion-dialog';
import { categoryLabel, dateRange, discountFor, eshopProductsIn } from './promotion-format';
import { PromotionsApi } from './promotions-api';

/** Kolik skončených akcí ukázat — starší jsou jen historie. */
const ENDED_SHOWN = 5;

type PromotionState = 'running' | 'planned' | 'ended';

interface PromotionRow {
  readonly promotion: AdminPromotionDto;
  readonly name: string;
  readonly nameEn: string | null;
  readonly category: string;
  /** Výchozí procento akce. */
  readonly percent: number;
  /** Nejvyšší procento mezi produkty — „až −30 %“, když se liší. */
  readonly maxPercent: number;
  readonly upTo: boolean;
  /** Produkty s jiným než výchozím procentem. */
  readonly ownCount: number;
  readonly dates: string;
  readonly state: PromotionState;
  readonly productCount: number;
  readonly lastDay: boolean;
}

/**
 * Akční slevy v e-shopu (ADR 0014): co běží, co je naplánované a co nedávno skončilo.
 * Založení a úprava v dialogu s náhledem cen, smazání s potvrzením.
 */
@Component({
  selector: 'wp-promotions-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, MatButtonModule, MatProgressBarModule],
  templateUrl: './promotions-page.html',
  styleUrl: './promotions-page.scss',
})
export class PromotionsPage {
  private readonly api = inject(PromotionsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly catalog = httpResource<CatalogAdminDto>(() => '/api/v1/catalog/admin');

  protected readonly confirming = signal<string | null>(null);
  protected readonly removing = signal(false);

  private readonly rows = computed<PromotionRow[]>(() => {
    const catalog = this.catalog.value();
    if (!catalog) {
      return [];
    }

    const today = catalog.today;
    return catalog.promotions.map((promotion) => {
      const category = catalog.categories.find((c) => c.id === promotion.categoryId);
      const percents = eshopProductsIn(catalog, promotion.categoryId)
        .map((product) => discountFor(promotion, product.id))
        .filter((percent) => percent > 0);
      const state: PromotionState =
        promotion.validTo < today ? 'ended' : promotion.validFrom > today ? 'planned' : 'running';
      return {
        promotion,
        name: promotion.name['cs'],
        nameEn: promotion.name['en'] ?? null,
        category: category ? categoryLabel(category, catalog.categories) : '—',
        percent: num(promotion.discountPercent),
        maxPercent: percents.length > 0 ? Math.max(...percents) : num(promotion.discountPercent),
        upTo: new Set(percents).size > 1,
        ownCount: promotion.productDiscounts.length,
        dates: dateRange(promotion.validFrom, promotion.validTo),
        state,
        productCount: percents.length,
        lastDay: state === 'running' && promotion.validTo === today,
      };
    });
  });

  protected readonly running = computed(() => this.rows().filter((r) => r.state === 'running'));
  protected readonly planned = computed(() =>
    this.rows()
      .filter((r) => r.state === 'planned')
      .sort((a, b) => a.promotion.validFrom.localeCompare(b.promotion.validFrom)),
  );
  protected readonly ended = computed(() =>
    this.rows()
      .filter((r) => r.state === 'ended')
      .sort((a, b) => b.promotion.validTo.localeCompare(a.promotion.validTo))
      .slice(0, ENDED_SHOWN),
  );

  protected readonly isEmpty = computed(() => this.rows().length === 0);

  protected open(promotion?: AdminPromotionDto): void {
    const catalog = this.catalog.value();
    if (!catalog) {
      return;
    }

    const data: PromotionDialogData = { catalog, promotion };
    this.dialog
      .open(PromotionDialog, { data, width: '34rem', maxWidth: '100vw', autoFocus: false })
      .afterClosed()
      .subscribe((saved?: CatalogAdminDto) => {
        if (saved) {
          this.catalog.set(saved);
          this.snackBar.open(
            promotion
              ? 'Akce upravena.'
              : 'Akce vytvořena. V e-shopu se ukáže nejpozději do minuty.',
            'OK',
            {
              duration: 5000,
            },
          );
        }
      });
  }

  protected async remove(row: PromotionRow): Promise<void> {
    this.removing.set(true);
    try {
      this.catalog.set(await this.api.remove(row.promotion.id));
      this.confirming.set(null);
      this.snackBar.open('Akce smazána.', 'OK', { duration: 4000 });
    } catch (error) {
      this.snackBar.open(problemMessage(error), 'OK', { duration: 8000 });
    } finally {
      this.removing.set(false);
    }
  }
}
