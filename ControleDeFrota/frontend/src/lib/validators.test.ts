import { describe, expect, it } from 'vitest';
import {
  combine,
  isValidChassis,
  isValidCnpj,
  isValidCpf,
  isValidDriverLicense,
  isValidEmail,
  isValidPhone,
  isValidPlate,
  isValidRenavam,
  isValidZipCode,
  optional,
  required,
  rules,
} from './validators';

// Same cases as tests/Fleet.Domain.Tests/Validation/BrazilianDocumentTests.cs — keep in sync.
describe('document validators (mirror of Fleet.Domain.Validation)', () => {
  it.each(['529.982.247-25', '52998224725'])('accepts valid CPF %s', (v) => expect(isValidCpf(v)).toBe(true));
  it.each(['529.982.247-24', '111.111.111-11', '5299822472', '52998224A25', ''])('rejects invalid CPF %s', (v) =>
    expect(isValidCpf(v)).toBe(false),
  );

  it.each(['11.222.333/0001-81', '12.ABC.345/01DE-35', '12abc34501de35'])('accepts valid CNPJ %s', (v) =>
    expect(isValidCnpj(v)).toBe(true),
  );
  it.each(['11.222.333/0001-82', '12.ABC.345/01DE-36', '12.ABC.345/01DE-3A', '00.000.000/0000-00'])('rejects invalid CNPJ %s', (v) =>
    expect(isValidCnpj(v)).toBe(false),
  );

  it.each(['ABC1234', 'ABC-1234', 'abc1d23'])('accepts plate %s', (v) => expect(isValidPlate(v)).toBe(true));
  it.each(['AB12345', 'ABCD123', 'ABC12D3'])('rejects plate %s', (v) => expect(isValidPlate(v)).toBe(false));

  it.each(['12345678900', '123456789', '00123456789'])('accepts RENAVAM %s', (v) => expect(isValidRenavam(v)).toBe(true));
  it.each(['12345678901', '11111111111', '1234567'])('rejects RENAVAM %s', (v) => expect(isValidRenavam(v)).toBe(false));

  it('validates chassis (VIN)', () => {
    expect(isValidChassis('9BWZZZ377VT004251')).toBe(true);
    expect(isValidChassis('9BWZZZ377VT00425I')).toBe(false);
    expect(isValidChassis('9BWZZZ377VT00425')).toBe(false);
  });

  it('validates contact formats', () => {
    expect(isValidZipCode('80010-000')).toBe(true);
    expect(isValidZipCode('00000-000')).toBe(false);
    expect(isValidPhone('(41) 99999-8888')).toBe(true);
    expect(isValidPhone('(41) 3333-4444')).toBe(true);
    expect(isValidPhone('41 89999-8888')).toBe(false);
    expect(isValidEmail('frota@rodoxisto.com.br')).toBe(true);
    expect(isValidEmail('frota@empresa')).toBe(false);
    expect(isValidDriverLicense('04512345678')).toBe(true);
    expect(isValidDriverLicense('11111111111')).toBe(false);
  });
});

describe('form rule helpers', () => {
  it('required returns a message for blank values', () => {
    expect(required('Placa')('  ')).toBe('Placa: campo obrigatório.');
    expect(required('Placa')('ABC1234')).toBeNull();
  });

  it('optional skips empty values', () => {
    expect(optional(rules.phone)('')).toBeNull();
    expect(optional(rules.phone)('123')).not.toBeNull();
  });

  it('combine returns the first error', () => {
    expect(combine(required('CPF'), rules.cpf)('')).toBe('CPF: campo obrigatório.');
    expect(combine(required('CPF'), rules.cpf)('123')).toContain('CPF inválido');
  });
});
