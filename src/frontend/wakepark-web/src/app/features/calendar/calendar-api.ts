import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { OperatingCalendarDto, OperatingDaysImportDto } from '../../core/api/api';

/** Změny v provozním kalendáři (ADR 0009). Čtení si stránka dělá sama přes `httpResource`. */
@Injectable({ providedIn: 'root' })
export class CalendarApi {
  private readonly http = inject(HttpClient);

  /** Otevírací doba jednoho dne. Oba časy null = zavřeno. Časy jako `HH:mm`. */
  setDay(parkId: string, date: string, opensAt: string | null, closesAt: string | null): Promise<OperatingCalendarDto> {
    return firstValueFrom(
      this.http.put<OperatingCalendarDto>(`/api/v1/operations/parks/${parkId}/days/${date}`, {
        opensAt: opensAt && `${opensAt}:00`,
        closesAt: closesAt && `${closesAt}:00`,
      }),
    );
  }

  /** Co by import CSV změnil. Nic neuloží. */
  previewImport(parkId: string, csv: string): Promise<OperatingDaysImportDto> {
    return firstValueFrom(
      this.http.post<OperatingDaysImportDto>(`/api/v1/operations/parks/${parkId}/days/import/preview`, { csv }),
    );
  }

  /** Import CSV. S chybou v souboru neuloží nic (422). */
  applyImport(parkId: string, csv: string): Promise<OperatingDaysImportDto> {
    return firstValueFrom(this.http.post<OperatingDaysImportDto>(`/api/v1/operations/parks/${parkId}/days/import`, { csv }));
  }
}
