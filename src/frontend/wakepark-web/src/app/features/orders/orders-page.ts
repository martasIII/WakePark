import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';

import { num, type OrderListDto, type UnmatchedPaymentDto } from '../../core/api/api';
import { ORDER_STATUSES, date, dateTime, money, orderStatusLabel } from './order-format';

const PAGE_SIZE = 25;

/**
 * Přehled objednávek e-shopu (ADR 0012) — jen čtení. Nahoře platby k ručnímu vyřízení
 * (jen když nějaké jsou), pod nimi objednávky od nejnovější s filtrem stavu a hledáním.
 */
@Component({
  selector: 'wp-orders-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatInputModule, MatProgressBarModule, RouterLink],
  templateUrl: './orders-page.html',
  styleUrl: './orders-page.scss',
})
export class OrdersPage {
  protected readonly statuses = ORDER_STATUSES;
  protected readonly money = money;
  protected readonly dateTime = dateTime;
  protected readonly date = date;
  protected readonly statusLabel = orderStatusLabel;

  protected readonly status = signal('');
  protected readonly search = signal('');
  protected readonly page = signal(1);

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected readonly orders = httpResource<OrderListDto>(() => {
    const params = new URLSearchParams({ page: String(this.page()), pageSize: String(PAGE_SIZE) });
    if (this.status()) {
      params.set('status', this.status());
    }
    if (this.search().trim()) {
      params.set('q', this.search().trim());
    }
    return `/api/v1/billing/admin/orders?${params}`;
  });

  protected readonly unmatched = httpResource<UnmatchedPaymentDto[]>(() => '/api/v1/billing/admin/payments/unmatched', {
    defaultValue: [],
  });

  protected readonly total = computed(() => num(this.orders.value()?.total ?? 0));

  protected readonly pageCount = computed(() => {
    const list = this.orders.value();
    return list ? Math.max(1, Math.ceil(num(list.total) / num(list.pageSize))) : 1;
  });

  /** „1 objednávka“, „3 objednávky“, „12 objednávek“. */
  protected readonly totalLabel = computed(() => {
    const total = this.total();
    return `${total} ${total === 1 ? 'objednávka' : total >= 2 && total <= 4 ? 'objednávky' : 'objednávek'}`;
  });

  protected setStatus(value: string): void {
    this.status.set(value);
    this.page.set(1);
  }

  /** Hledá se až chvíli po dopsání, ne po každém písmenu. */
  protected setSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.search.set(value);
      this.page.set(1);
    }, 300);
  }
}
