import { describe, expect, it } from 'vitest';
import { dotManufacture, isoWeekMonday, previewPositions, formatCostPerKm, formatPressure, formatTread, groupPositions, pressureCheck, rotationProblems, treadLevel } from './tires';

describe('treadLevel (mirror of TireAlertPolicy)', () => {
  it('classifies by the company thresholds', () => {
    expect(treadLevel(8, 3, 4)).toBe('ok');
    expect(treadLevel(4, 3, 4)).toBe('warning');
    expect(treadLevel(3, 3, 4)).toBe('critical');
    expect(treadLevel(null, 3, 4)).toBe('unknown');
  });
});

describe('pressureCheck (mirror of TirePressure.Check)', () => {
  it('compares in any unit with the tolerance', () => {
    expect(pressureCheck(110, 'Psi', 110, 10)).toBe('WithinRange');
    expect(pressureCheck(95, 'Psi', 110, 10)).toBe('Low');
    expect(pressureCheck(7.6, 'Bar', 110, 10)).toBe('WithinRange');
    expect(pressureCheck(620, 'Kpa', 110, 10)).toBe('Low');
  });

  it('never guesses without a reference', () => {
    expect(pressureCheck(80, 'Psi', null, 10)).toBe('NotEvaluated');
    expect(pressureCheck(null, 'Psi', 110, 10)).toBeNull();
  });
});

describe('dotManufacture (mirror of TireDot)', () => {
  it('reads week and year from the last four digits', () => {
    expect(dotManufacture('DOT 4B7X 3524')).toEqual({ week: 35, year: 2024 });
    expect(dotManufacture('0123')).toEqual({ week: 1, year: 2023 });
  });

  it('rejects codes without a valid date', () => {
    expect(dotManufacture('DOT 5424')).toBeNull();
    expect(dotManufacture('DOT 123')).toBeNull();
    expect(dotManufacture('')).toBeNull();
  });

  it('gives the Monday of the ISO week, like the server', () => {
    const d = isoWeekMonday(2024, 35);
    expect([d.getFullYear(), d.getMonth() + 1, d.getDate()]).toEqual([2024, 8, 26]);
    const first = isoWeekMonday(2023, 1);
    expect([first.getFullYear(), first.getMonth() + 1, first.getDate()]).toEqual([2023, 1, 2]);
  });

  it('knows 53-week years', () => {
    expect(dotManufacture('5320')).toEqual({ week: 53, year: 2020 });
    expect(dotManufacture('5321')).toBeNull();
  });
});

describe('groupPositions', () => {
  const p = (code: string, axleNumber: number, side: 'Left' | 'Right' | 'None', placement: 'Single' | 'Outer' | 'Inner' | 'Spare') =>
    ({ code, axleNumber, side, placement });

  it('draws outer tires at the edges and keeps spares apart', () => {
    const { axles, spares } = groupPositions([
      p('1E', 1, 'Left', 'Single'), p('1D', 1, 'Right', 'Single'),
      p('2EE', 2, 'Left', 'Outer'), p('2EI', 2, 'Left', 'Inner'), p('2DI', 2, 'Right', 'Inner'), p('2DE', 2, 'Right', 'Outer'),
      p('EST1', 0, 'None', 'Spare'),
    ]);
    expect(axles.map((a) => a.number)).toEqual([1, 2]);
    expect(axles[1].left.map((x) => x.code)).toEqual(['2EE', '2EI']);
    expect(axles[1].right.map((x) => x.code)).toEqual(['2DI', '2DE']);
    expect(spares.map((x) => x.code)).toEqual(['EST1']);
  });
});

describe('previewPositions (mirror of TirePositions.For)', () => {
  it('generates the same codes and labels as the server', () => {
    const positions = previewPositions([
      { type: 'Steer', isDual: false, isRequired: true, allowedSize: null, recommendedPressurePsi: null },
      { type: 'Drive', isDual: true, isRequired: true, allowedSize: null, recommendedPressurePsi: null },
    ], 2);
    expect(positions.map((p) => p.code)).toEqual(['1E', '1D', '2EE', '2EI', '2DI', '2DE', 'EST1', 'EST2']);
    expect(positions[2].label).toBe('Eixo 2 — Esquerdo externo');
    expect(positions[7].label).toBe('Estepe 2');
  });
});

describe('rotationProblems (mirror of the rotation rule)', () => {
  const current = { '1E': 'A', '1D': 'B', '2EE': 'C' };

  it('accepts a swap and a move into a free position', () => {
    expect(rotationProblems(current, [{ tireId: 'A', toPositionCode: '1D' }, { tireId: 'B', toPositionCode: '1E' }])).toEqual([]);
    expect(rotationProblems(current, [{ tireId: 'C', toPositionCode: '2EI' }])).toEqual([]);
  });

  it('refuses an occupied target and duplicated targets', () => {
    expect(rotationProblems(current, [{ tireId: 'A', toPositionCode: '2EE' }])).toHaveLength(1);
    expect(rotationProblems(current, [{ tireId: 'A', toPositionCode: '2EI' }, { tireId: 'B', toPositionCode: '2EI' }])[0]).toContain('Dois pneus');
  });
});

describe('formatting', () => {
  it('uses pt-BR numbers', () => {
    expect(formatTread(8.5)).toBe('8,5 mm');
    expect(formatPressure(7.25, 'Bar')).toBe('7,25 bar');
    expect(formatCostPerKm(0.028)).toBe('R$ 0,0280/km');
    expect(formatCostPerKm(null)).toBe('—');
  });
});
