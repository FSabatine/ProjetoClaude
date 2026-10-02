/**
 * Instant feedback on the fueling form. Mirrors Fleet.Domain.Fuel (FuelingAmounts, the tank rule) — the API is the
 * authority and recalculates everything; these only show the user what will happen before saving.
 */

export type FuelUnit = 'Liter' | 'CubicMeter' | 'KilowattHour';

export const FUEL_UNIT_SYMBOL: Record<FuelUnit, string> = { Liter: 'L', CubicMeter: 'm³', KilowattHour: 'kWh' };

/** Same rounding as FuelingAmounts.Total: quantity × price, to cents, half away from zero. */
export function fuelingTotal(quantity: number | null, unitPrice: number | null): number | null {
  if (quantity === null || unitPrice === null || !(quantity > 0) || !(unitPrice > 0)) return null;
  const cents = quantity * unitPrice * 100;
  // Number.EPSILON nudge: 232.535 × 100 is 23253.499999… in binary floating point.
  return Math.sign(cents) * Math.round(Math.abs(cents) + Number.EPSILON * 100) / 100;
}

/** Tank rule of FuelAnomalyRules: above capacity × (1 + tolerance) is flagged for review (never blocked). */
export function exceedsTank(quantity: number | null, tankCapacity: number | null, tolerancePercent: number): boolean {
  if (quantity === null || tankCapacity === null || tankCapacity <= 0) return false;
  return quantity > tankCapacity * (1 + tolerancePercent / 100);
}

/** "5,0 km/L" — km per unit is the primary unit everywhere (UX_UI.md). */
export function formatConsumption(value: number | null | undefined, unit: FuelUnit = 'Liter'): string {
  if (value === null || value === undefined) return '—';
  return `${value.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 2 })} km/${FUEL_UNIT_SYMBOL[unit]}`;
}

/** Alternative unit shown only where it helps (fueling detail): L/100 km = 100 / (km/L). Liquid fuels only. */
export function litersPer100Km(kmPerLiter: number | null | undefined): number | null {
  if (!kmPerLiter || kmPerLiter <= 0) return null;
  return Math.round((100 / kmPerLiter) * 10) / 10;
}

/** "+12,5%" / "-26%" — signed, so a negative deviation of consumption is visible without color. */
export function formatDeviation(percent: number | null | undefined): string {
  if (percent === null || percent === undefined) return '—';
  const text = Math.abs(percent).toLocaleString('pt-BR', { maximumFractionDigits: 1 });
  return `${percent > 0 ? '+' : percent < 0 ? '-' : ''}${text}%`;
}

export function formatQuantity(value: number | null | undefined, unit: FuelUnit = 'Liter'): string {
  if (value === null || value === undefined) return '—';
  return `${value.toLocaleString('pt-BR', { maximumFractionDigits: 2 })} ${FUEL_UNIT_SYMBOL[unit]}`;
}

/** Unit price with 3 decimals, as printed on Brazilian pumps (R$ 6,199). */
export function formatUnitPrice(value: number | null | undefined, unit: FuelUnit = 'Liter'): string {
  if (value === null || value === undefined) return '—';
  return `R$ ${value.toLocaleString('pt-BR', { minimumFractionDigits: 3, maximumFractionDigits: 4 })}/${FUEL_UNIT_SYMBOL[unit]}`;
}
