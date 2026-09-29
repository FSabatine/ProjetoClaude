import dayjs from 'dayjs';
import { normalizeDocument } from './validators';

const numberFormat = new Intl.NumberFormat('pt-BR');
const currencyFormat = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

export const formatNumber = (value: number | null | undefined) => (value === null || value === undefined ? '—' : numberFormat.format(value));

export const formatCurrency = (value: number | null | undefined) =>
  value === null || value === undefined ? '—' : currencyFormat.format(value);

/** API dates are ISO "YYYY-MM-DD" (DateOnly) or ISO date-times (UTC). */
export const formatDate = (value: string | null | undefined) => (value ? dayjs(value).format('DD/MM/YYYY') : '—');

export const formatDateTime = (value: string | null | undefined) => (value ? dayjs(value).format('DD/MM/YYYY HH:mm') : '—');

export const formatCpf = (value: string) => normalizeDocument(value).replace(/^(\d{3})(\d{3})(\d{3})(\d{2})$/, '$1.$2.$3-$4');

export const formatCnpj = (value: string) =>
  normalizeDocument(value).replace(/^(\w{2})(\w{3})(\w{3})(\w{4})(\d{2})$/, '$1.$2.$3/$4-$5');

/** Legacy "ABC-1234"; Mercosul plates are displayed without hyphen as on the plate itself ("ABC1D23"). */
export const formatPlate = (value: string) => {
  const plate = normalizeDocument(value);
  return /^[A-Z]{3}\d{4}$/.test(plate) ? `${plate.slice(0, 3)}-${plate.slice(3)}` : plate;
};

export const formatPhone = (value: string | null | undefined) => {
  if (!value) return '—';
  const digits = normalizeDocument(value);
  return digits.length === 11
    ? digits.replace(/^(\d{2})(\d{5})(\d{4})$/, '($1) $2-$3')
    : digits.replace(/^(\d{2})(\d{4})(\d{4})$/, '($1) $2-$3');
};

export const formatZipCode = (value: string | null | undefined) =>
  value ? normalizeDocument(value).replace(/^(\d{5})(\d{3})$/, '$1-$2') : '—';

/** Date object from Mantine DateInput → API DateOnly string. */
export const toApiDate = (value: Date | null | undefined) => (value ? dayjs(value).format('YYYY-MM-DD') : null);

/** API DateOnly string → Date for Mantine DateInput. dayjs parses "YYYY-MM-DD" as local midnight (native Date would use UTC and shift a day). */
export const fromApiDate = (value: string | null | undefined) => (value ? dayjs(value).toDate() : null);
