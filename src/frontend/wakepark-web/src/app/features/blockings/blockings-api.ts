import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { BlockingDto, CreateBlockingRequest } from '../../core/api/api';

/** Založení a zrušení blokace (ADR 0009). Seznam si stránka čte z provozního kalendáře. */
@Injectable({ providedIn: 'root' })
export class BlockingsApi {
  private readonly http = inject(HttpClient);

  /** Začátek a konec v lokálním čase areálu (`2026-10-27T14:00:00`), bez vleku = celý areál. */
  create(parkId: string, request: CreateBlockingRequest): Promise<BlockingDto> {
    return firstValueFrom(this.http.post<BlockingDto>(`/api/v1/operations/parks/${parkId}/blockings`, request));
  }

  remove(parkId: string, blockingId: string): Promise<unknown> {
    return firstValueFrom(this.http.delete(`/api/v1/operations/parks/${parkId}/blockings/${blockingId}`));
  }
}
