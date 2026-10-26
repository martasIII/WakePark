import { HttpErrorResponse } from '@angular/common/http';

/**
 * Hláška pro člověka z chyby API. Backend vrací RFC 9457 (`detail`, u validace `errors`),
 * doménová pravidla jsou česky a srozumitelně — ukážou se tak, jak jsou.
 */
export function problemMessage(error: unknown, fallback = 'Akce se nepodařila. Zkuste to prosím znovu.'): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  const body = error.error as { detail?: string; errors?: Record<string, string[]> } | null;

  if (body?.errors) {
    return Object.values(body.errors).flat().join(' ');
  }

  if (body?.detail) {
    return body.detail;
  }

  return error.status === 0 ? 'Server neodpovídá. Zkuste to prosím za chvíli.' : fallback;
}
