import { describe, expect, it } from 'vitest';
import { formatCnpj, formatCpf, formatDate, formatPhone, formatPlate, fromApiDate, toApiDate, formatZipCode } from './format';

describe('formatters', () => {
  it('formats Brazilian documents', () => {
    expect(formatCpf('52998224725')).toBe('529.982.247-25');
    expect(formatCnpj('11222333000181')).toBe('11.222.333/0001-81');
    expect(formatCnpj('12ABC34501DE35')).toBe('12.ABC.345/01DE-35');
    expect(formatZipCode('80010000')).toBe('80010-000');
  });

  it('formats plates in the style printed on each plate type', () => {
    expect(formatPlate('ABC1234')).toBe('ABC-1234');
    expect(formatPlate('ABC1D23')).toBe('ABC1D23');
  });

  it('formats phones', () => {
    expect(formatPhone('41999998888')).toBe('(41) 99999-8888');
    expect(formatPhone('4133334444')).toBe('(41) 3333-4444');
    expect(formatPhone(null)).toBe('—');
  });

  it('round-trips API dates without timezone shift', () => {
    expect(formatDate('2026-09-29')).toBe('29/09/2026');
    expect(toApiDate(fromApiDate('2026-01-01'))).toBe('2026-01-01');
    expect(toApiDate(null)).toBeNull();
  });
});
