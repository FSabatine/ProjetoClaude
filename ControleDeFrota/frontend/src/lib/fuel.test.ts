import { describe, expect, it } from 'vitest';
import { exceedsTank, formatConsumption, formatDeviation, formatUnitPrice, fuelingTotal, litersPer100Km } from './fuel';

describe('fuelingTotal (mirror of FuelingAmounts.Total)', () => {
  it('multiplies quantity by price', () => expect(fuelingTotal(250, 6.2)).toBe(1550));
  it('rounds to cents half away from zero', () => expect(fuelingTotal(37.512, 6.199)).toBe(232.54));
  it('is null until both values are positive', () => {
    expect(fuelingTotal(null, 6)).toBeNull();
    expect(fuelingTotal(0, 6)).toBeNull();
    expect(fuelingTotal(10, -1)).toBeNull();
  });
});

describe('exceedsTank (mirror of the tank rule)', () => {
  it('allows the configured tolerance', () => {
    expect(exceedsTank(420, 400, 5)).toBe(false);
    expect(exceedsTank(421, 400, 5)).toBe(true);
  });
  it('never flags without a known capacity', () => expect(exceedsTank(5000, null, 5)).toBe(false));
});

describe('formatting', () => {
  it('formats consumption in km per unit', () => {
    expect(formatConsumption(5)).toBe('5,0 km/L');
    expect(formatConsumption(12.345, 'CubicMeter')).toBe('12,35 km/m³');
    expect(formatConsumption(null)).toBe('—');
  });
  it('converts km/L to L/100 km', () => {
    expect(litersPer100Km(5)).toBe(20);
    expect(litersPer100Km(0)).toBeNull();
  });
  it('signs deviations so they do not rely on color', () => {
    expect(formatDeviation(-26)).toBe('-26%');
    expect(formatDeviation(12.5)).toBe('+12,5%');
  });
  it('shows pump prices with three decimals', () => expect(formatUnitPrice(6.199)).toBe('R$ 6,199/L'));
});
