/**
 * Instant feedback while typing an odometer reading. Mirrors Fleet.Domain.Mileage.OdometerPolicy —
 * the API is the authority; this only warns before submitting.
 */
export const MAX_PLAUSIBLE_KM_PER_DAY = 1500;

export type OdometerHint =
  | { kind: 'none' }
  | { kind: 'decrease'; previousKm: number }
  | { kind: 'increase'; deltaKm: number }
  | { kind: 'suspicious'; deltaKm: number; kmPerDay: number };

export function odometerHint(newKm: number | null, previousKm: number, previousAt: Date | null, readAt: Date): OdometerHint {
  if (newKm === null || Number.isNaN(newKm)) return { kind: 'none' };
  if (newKm < previousKm) return { kind: 'decrease', previousKm };
  const deltaKm = newKm - previousKm;
  const days = previousAt ? Math.max(1, (readAt.getTime() - previousAt.getTime()) / 86_400_000) : 1;
  const kmPerDay = deltaKm / days;
  return previousAt && kmPerDay > MAX_PLAUSIBLE_KM_PER_DAY ? { kind: 'suspicious', deltaKm, kmPerDay } : { kind: 'increase', deltaKm };
}

/** "vence em 5 dias", "venceu há 3 dias", "vence hoje" — for document badges and alerts. */
export function dueLabel(days: number | null): string | null {
  if (days === null) return null;
  if (days === 0) return 'vence hoje';
  if (days === 1) return 'vence amanhã';
  if (days === -1) return 'venceu ontem';
  return days > 0 ? `vence em ${days} dias` : `venceu há ${-days} dias`;
}
