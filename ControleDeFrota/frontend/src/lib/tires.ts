/**
 * Instant feedback for the tire screens. Mirrors Fleet.Domain.Tires (TireAlertPolicy thresholds, TirePressure, TireDot,
 * the rotation map rule) — the API is the authority; these only show the user what will happen before saving.
 */

export type TreadLevel = 'unknown' | 'ok' | 'warning' | 'critical';
export type PressureUnit = 'Psi' | 'Bar' | 'Kpa';

/** Same comparison as TireAlertPolicy: ≤ minimum = critical, ≤ warning depth = warning (company policy, not law). */
export function treadLevel(treadMm: number | null | undefined, minMm: number, warningMm: number): TreadLevel {
  if (treadMm === null || treadMm === undefined) return 'unknown';
  if (treadMm <= minMm) return 'critical';
  if (treadMm <= warningMm) return 'warning';
  return 'ok';
}

export const TREAD_LEVEL_TEXT: Record<TreadLevel, string> = {
  unknown: 'Sulco não medido',
  ok: 'Sulco normal',
  warning: 'Perto do mínimo',
  critical: 'No mínimo da empresa',
};

export function formatTread(mm: number | null | undefined): string {
  if (mm === null || mm === undefined) return '—';
  return `${mm.toLocaleString('pt-BR', { maximumFractionDigits: 1 })} mm`;
}

const UNIT_TEXT: Record<PressureUnit, string> = { Psi: 'psi', Bar: 'bar', Kpa: 'kPa' };

export function formatPressure(value: number | null | undefined, unit: PressureUnit | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return `${value.toLocaleString('pt-BR', { maximumFractionDigits: 2 })} ${UNIT_TEXT[unit ?? 'Psi']}`;
}

/** TirePressure.ToPsi. */
export function toPsi(value: number, unit: PressureUnit): number {
  return unit === 'Bar' ? value * 14.5038 : unit === 'Kpa' ? value * 0.145038 : value;
}

/** Display of a psi reference in the company unit. */
export function fromPsi(psi: number, unit: PressureUnit): number {
  return unit === 'Bar' ? Math.round((psi / 14.5038) * 100) / 100 : unit === 'Kpa' ? Math.round(psi / 0.145038) : Math.round(psi);
}

export type PressureCheck = 'WithinRange' | 'Low' | 'High' | 'NotEvaluated';

/** TirePressure.Check: no reference means "not evaluated", never a guess. */
export function pressureCheck(value: number | null, unit: PressureUnit, recommendedPsi: number | null, tolerancePercent: number): PressureCheck | null {
  if (value === null || !(value > 0)) return null;
  if (!recommendedPsi || recommendedPsi <= 0) return 'NotEvaluated';
  const psi = toPsi(value, unit);
  const margin = (recommendedPsi * tolerancePercent) / 100;
  return psi < recommendedPsi - margin ? 'Low' : psi > recommendedPsi + margin ? 'High' : 'WithinRange';
}

/** TireDot.ManufacturedOn: last four digits of the DOT = week + year ("3524" → week 35 of 2024). */
export function dotManufacture(dot: string | null | undefined): { week: number; year: number } | null {
  if (!dot) return null;
  const digits = dot.toUpperCase().replace(/^\s*DOT\s*/, '').replace(/[^0-9A-Z]/g, '');
  if (digits.length < 4) return null;
  const code = digits.slice(-4);
  if (!/^\d{4}$/.test(code)) return null;
  const week = Number(code.slice(0, 2));
  const year = 2000 + Number(code.slice(2));
  if (week < 1 || week > weeksInIsoYear(year)) return null;
  return { week, year };
}

/** Monday of an ISO week (local date) — the manufacturing date the server derives from the DOT. */
export function isoWeekMonday(year: number, week: number): Date {
  const jan4 = new Date(year, 0, 4);
  const monday = new Date(year, 0, 4 - ((jan4.getDay() + 6) % 7));
  monday.setDate(monday.getDate() + (week - 1) * 7);
  return monday;
}

function weeksInIsoYear(year: number): number {
  // A year has 53 ISO weeks when Jan 1st is a Thursday, or a Wednesday in a leap year.
  const jan1 = new Date(Date.UTC(year, 0, 1)).getUTCDay();
  const leap = (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
  return jan1 === 4 || (leap && jan1 === 3) ? 53 : 52;
}

export interface DiagramPosition {
  code: string;
  axleNumber: number;
  side: 'Left' | 'Right' | 'None';
  placement: 'Single' | 'Outer' | 'Inner' | 'Spare';
}

export interface DiagramAxle<T extends DiagramPosition> {
  number: number;
  /** Left side as seen from above, outer first. */
  left: T[];
  /** Right side, inner first (so the outer tire is drawn at the edge). */
  right: T[];
}

/** Groups generated positions into axles for the top-view diagram, plus the spares. Never hard-coded per vehicle type. */
export function groupPositions<T extends DiagramPosition>(positions: T[]): { axles: DiagramAxle<T>[]; spares: T[] } {
  const order = { Outer: 0, Single: 1, Inner: 2, Spare: 3 } as const;
  const numbers = [...new Set(positions.filter((p) => p.placement !== 'Spare').map((p) => p.axleNumber))].sort((a, b) => a - b);
  return {
    axles: numbers.map((number) => {
      const onAxle = positions.filter((p) => p.axleNumber === number && p.placement !== 'Spare');
      return {
        number,
        left: onAxle.filter((p) => p.side === 'Left').sort((a, b) => order[a.placement] - order[b.placement]),
        right: onAxle.filter((p) => p.side === 'Right').sort((a, b) => order[b.placement] - order[a.placement]),
      };
    }),
    spares: positions.filter((p) => p.placement === 'Spare'),
  };
}

export interface AxleDraft {
  type: string;
  isDual: boolean;
  isRequired: boolean;
  allowedSize: string | null;
  recommendedPressurePsi: number | null;
}

/**
 * Same generation as TirePositions.For on the server (codes and labels), so the configuration editor can preview the
 * diagram before saving: single axle → 1E/1D; dual → 2EE/2EI/2DI/2DE; spares → EST1…
 */
export function previewPositions(axles: AxleDraft[], spareCount: number) {
  const positions: {
    code: string; label: string; axleNumber: number; axleType: string | null; side: 'Left' | 'Right' | 'None';
    placement: 'Single' | 'Outer' | 'Inner' | 'Spare'; isRequired: boolean; allowedSize: string | null; recommendedPressurePsi: number | null;
  }[] = [];
  axles.forEach((a, i) => {
    const n = i + 1;
    const make = (suffix: string, side: string, s: 'Left' | 'Right', placement: 'Single' | 'Outer' | 'Inner') =>
      positions.push({ code: `${n}${suffix}`, label: `Eixo ${n} — ${side}`, axleNumber: n, axleType: a.type, side: s, placement,
        isRequired: a.isRequired, allowedSize: a.allowedSize, recommendedPressurePsi: a.recommendedPressurePsi });
    if (a.isDual) {
      make('EE', 'Esquerdo externo', 'Left', 'Outer');
      make('EI', 'Esquerdo interno', 'Left', 'Inner');
      make('DI', 'Direito interno', 'Right', 'Inner');
      make('DE', 'Direito externo', 'Right', 'Outer');
    } else {
      make('E', 'Esquerdo', 'Left', 'Single');
      make('D', 'Direito', 'Right', 'Single');
    }
  });
  for (let s = 1; s <= spareCount; s++)
    positions.push({ code: `EST${s}`, label: spareCount === 1 ? 'Estepe' : `Estepe ${s}`, axleNumber: 0, axleType: null, side: 'None', placement: 'Spare',
      isRequired: false, allowedSize: null, recommendedPressurePsi: null });
  return positions;
}

export interface RotationMove {
  tireId: string;
  toPositionCode: string;
}

/**
 * The rotation rule of TireOperationsService, checked while the user builds it: one target per tire, no two tires to the
 * same position, and a target must be free or vacated by another tire of the same rotation.
 * `current` maps position code → tire id (the tires installed now).
 */
export function rotationProblems(current: Record<string, string>, moves: RotationMove[]): string[] {
  const problems: string[] = [];
  const moving = new Set(moves.map((m) => m.tireId));
  const fromOf = Object.fromEntries(Object.entries(current).map(([code, tire]) => [tire, code]));
  const targets = new Map<string, number>();
  for (const m of moves) targets.set(m.toPositionCode, (targets.get(m.toPositionCode) ?? 0) + 1);
  for (const [code, count] of targets) if (count > 1) problems.push(`Dois pneus foram enviados para ${code}.`);
  for (const m of moves) {
    if (fromOf[m.tireId] === m.toPositionCode) problems.push(`O pneu já está em ${m.toPositionCode}.`);
    const occupant = current[m.toPositionCode];
    if (occupant && occupant !== m.tireId && !moving.has(occupant))
      problems.push(`${m.toPositionCode} está ocupada por um pneu que não faz parte do rodízio.`);
  }
  return [...new Set(problems)];
}

/** "R$ 0,0280/km" — four decimals, the scale cost/km lives in. */
export function formatCostPerKm(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return `R$ ${value.toLocaleString('pt-BR', { minimumFractionDigits: 4, maximumFractionDigits: 4 })}/km`;
}
