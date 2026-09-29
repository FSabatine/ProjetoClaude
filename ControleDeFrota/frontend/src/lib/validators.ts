/**
 * Mirror of Fleet.Domain.Validation — for instant feedback only. The backend is the authority
 * and runs the same rules; keep both in sync (same test cases in validators.test.ts).
 */

const SEPARATORS = /[.\-/\s()]/g;

export const normalizeDocument = (value: string | null | undefined): string =>
  (value ?? '').replace(SEPARATORS, '').toUpperCase();

const isRepeated = (value: string) => value.length > 0 && [...value].every((c) => c === value[0]);

function mod11(value: string, weights: number[], charValue: (c: string) => number): number {
  const sum = weights.reduce((acc, weight, i) => acc + charValue(value[i]) * weight, 0);
  const remainder = sum % 11;
  return remainder < 2 ? 0 : 11 - remainder;
}

const asciiValue = (c: string) => c.charCodeAt(0) - 48;

export function isValidCpf(value: string | null | undefined): boolean {
  const cpf = normalizeDocument(value);
  if (!/^\d{11}$/.test(cpf) || isRepeated(cpf)) return false;
  const first = mod11(cpf, [10, 9, 8, 7, 6, 5, 4, 3, 2], asciiValue);
  const second = mod11(cpf, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2], asciiValue);
  return asciiValue(cpf[9]) === first && asciiValue(cpf[10]) === second;
}

/** Numeric and alphanumeric CNPJ (IN RFB 2.229/2024 — ADR-013). */
export function isValidCnpj(value: string | null | undefined): boolean {
  const cnpj = normalizeDocument(value);
  if (!/^[0-9A-Z]{12}\d{2}$/.test(cnpj) || isRepeated(cnpj)) return false;
  const first = mod11(cnpj, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2], asciiValue);
  const second = mod11(cnpj, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2], asciiValue);
  return asciiValue(cnpj[12]) === first && asciiValue(cnpj[13]) === second;
}

/** Legacy ABC1234 or Mercosul ABC1D23. */
export const isValidPlate = (value: string | null | undefined) => /^[A-Z]{3}\d[A-Z0-9]\d{2}$/.test(normalizeDocument(value));

export function isValidRenavam(value: string | null | undefined): boolean {
  let renavam = normalizeDocument(value);
  if (/^\d{9,10}$/.test(renavam)) renavam = renavam.padStart(11, '0');
  if (!/^\d{11}$/.test(renavam) || isRepeated(renavam)) return false;
  const weights = [3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
  const sum = weights.reduce((acc, w, i) => acc + Number(renavam[i]) * w, 0);
  const digit = (sum * 10) % 11;
  return Number(renavam[10]) === (digit === 10 ? 0 : digit);
}

export const isValidChassis = (value: string | null | undefined) => /^[A-HJ-NPR-Z0-9]{17}$/.test(normalizeDocument(value));

export const isValidZipCode = (value: string | null | undefined) => {
  const zip = normalizeDocument(value);
  return /^\d{8}$/.test(zip) && zip !== '00000000';
};

export const isValidPhone = (value: string | null | undefined) =>
  /^[1-9]{2}(9\d{8}|[2-8]\d{7})$/.test(normalizeDocument(value));

export const isValidEmail = (value: string | null | undefined) =>
  /^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$/.test((value ?? '').trim());

export const isValidDriverLicense = (value: string | null | undefined) => {
  const number = normalizeDocument(value);
  return /^\d{11}$/.test(number) && !isRepeated(number);
};

export const BRAZILIAN_STATES = [
  'AC', 'AL', 'AP', 'AM', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MT', 'MS', 'MG', 'PA',
  'PB', 'PR', 'PE', 'PI', 'RJ', 'RN', 'RS', 'RO', 'RR', 'SC', 'SP', 'SE', 'TO',
] as const;

// ---- Mantine form helpers: return the error message or null.

type Rule = (value: string) => string | null;

export const required = (label: string): Rule => (value) => (value?.trim() ? null : `${label}: campo obrigatório.`);

export const optional =
  (rule: Rule): Rule =>
  (value) =>
    value?.trim() ? rule(value) : null;

export const combine =
  (...rules: Rule[]): Rule =>
  (value) => {
    for (const rule of rules) {
      const error = rule(value);
      if (error) return error;
    }
    return null;
  };

const check = (predicate: (v: string) => boolean, message: string): Rule => (value) => (predicate(value) ? null : message);

export const rules = {
  cpf: check(isValidCpf, 'CPF inválido. Confira os 11 dígitos.'),
  cnpj: check(isValidCnpj, 'CNPJ inválido. Confira os 14 caracteres e os dígitos verificadores.'),
  plate: check(isValidPlate, 'Placa inválida. Use o formato ABC-1234 ou Mercosul ABC-1D23.'),
  renavam: check(isValidRenavam, 'RENAVAM inválido. Confira os 11 dígitos no documento do veículo (CRLV).'),
  chassis: check(isValidChassis, 'Chassi inválido. Deve ter 17 caracteres, sem as letras I, O e Q.'),
  zipCode: check(isValidZipCode, 'CEP inválido. Informe os 8 dígitos.'),
  phone: check(isValidPhone, 'Telefone inválido. Informe DDD + número, ex.: (41) 99999-9999.'),
  email: check(isValidEmail, 'E-mail inválido. Use o formato nome@empresa.com.br.'),
  driverLicense: check(isValidDriverLicense, 'Número da CNH inválido. Informe os 11 dígitos do registro.'),
};
