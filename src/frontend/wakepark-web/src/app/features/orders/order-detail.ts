import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';

import { num, type OrderAdminDetailDto } from '../../core/api/api';
import { PUBLIC_WEB, date, dateTime, money, orderStatusLabel, voucherStatusLabel } from './order-format';

/** Detail objednávky (ADR 0012): zákazník, položky s věnováním, platba, doklad a poukazy. Jen čtení. */
@Component({
  selector: 'wp-order-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatProgressBarModule, RouterLink],
  templateUrl: './order-detail.html',
  styleUrl: './order-detail.scss',
})
export class OrderDetail {
  /** Číslo objednávky z adresy (`/objednavky/261003`). */
  readonly number = input.required<string>();

  protected readonly money = money;
  protected readonly dateTime = dateTime;
  protected readonly date = date;
  protected readonly statusLabel = orderStatusLabel;
  protected readonly voucherStatusLabel = voucherStatusLabel;
  protected readonly num = num;

  protected readonly order = httpResource<OrderAdminDetailDto>(() =>
    /^\d+$/.test(this.number()) ? `/api/v1/billing/admin/orders/${this.number()}` : undefined,
  );

  /** Stránka objednávky na webu, jak ji vidí zákazník (v jeho jazyce). */
  protected readonly publicUrl = computed(() => {
    const order = this.order.value();
    return order ? `${PUBLIC_WEB}${order.language === 'cs' ? '' : `/${order.language}`}/objednavka/${order.accessToken}` : null;
  });
}
