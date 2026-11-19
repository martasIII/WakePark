import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { CatalogAdminDto, PromotionRequest } from '../../core/api/api';

/** Akční slevy v e-shopu (ADR 0014). Každá změna vrací celý katalog po změně. */
@Injectable({ providedIn: 'root' })
export class PromotionsApi {
  private readonly http = inject(HttpClient);

  create(request: PromotionRequest): Promise<CatalogAdminDto> {
    return firstValueFrom(this.http.post<CatalogAdminDto>('/api/v1/catalog/admin/promotions', request));
  }

  update(id: string, request: PromotionRequest): Promise<CatalogAdminDto> {
    return firstValueFrom(this.http.put<CatalogAdminDto>(`/api/v1/catalog/admin/promotions/${id}`, request));
  }

  remove(id: string): Promise<CatalogAdminDto> {
    return firstValueFrom(this.http.delete<CatalogAdminDto>(`/api/v1/catalog/admin/promotions/${id}`));
  }
}
