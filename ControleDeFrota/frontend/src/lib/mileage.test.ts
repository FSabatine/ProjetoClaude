import { describe, expect, it } from 'vitest';
import { dueLabel, odometerHint } from './mileage';

const day = 86_400_000;
const base = new Date('2026-09-01T12:00:00Z');

describe('odometerHint (mirror of OdometerPolicy)', () => {
  it('flags a decrease', () => {
    expect(odometerHint(119_000, 120_000, base, new Date(base.getTime() + day))).toEqual({ kind: 'decrease', previousKm: 120_000 });
  });

  it('accepts a plausible increase', () => {
    expect(odometerHint(120_480, 120_000, base, new Date(base.getTime() + 4 * day))).toEqual({ kind: 'increase', deltaKm: 480 });
  });

  it('flags an extra digit as suspicious, counting at least one day', () => {
    const hint = odometerHint(121_501, 120_000, base, new Date(base.getTime() + 60_000));
    expect(hint.kind).toBe('suspicious');
  });

  it('ignores an empty value', () => {
    expect(odometerHint(null, 120_000, base, base)).toEqual({ kind: 'none' });
  });
});

describe('dueLabel', () => {
  it('describes how far the due date is', () => {
    expect(dueLabel(0)).toBe('vence hoje');
    expect(dueLabel(1)).toBe('vence amanhã');
    expect(dueLabel(12)).toBe('vence em 12 dias');
    expect(dueLabel(-1)).toBe('venceu ontem');
    expect(dueLabel(-5)).toBe('venceu há 5 dias');
    expect(dueLabel(null)).toBeNull();
  });
});
