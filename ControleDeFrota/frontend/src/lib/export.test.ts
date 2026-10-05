import { describe, expect, it } from 'vitest';
import { exportFileName, fetchAllPages, MAX_EXPORT_ROWS, toCsv } from './export';

describe('toCsv', () => {
  const columns = [
    { header: 'Placa', value: (r: { plate: string; cost: number | null }) => r.plate },
    { header: 'Custo', value: (r: { plate: string; cost: number | null }) => r.cost },
  ];

  it('uses ";" separator, BOM and pt-BR decimals', () => {
    const csv = toCsv(columns, [{ plate: 'ABC1D23', cost: 1234.5 }]);
    expect(csv.startsWith('﻿')).toBe(true);
    expect(csv).toContain('Placa;Custo');
    expect(csv).toContain('ABC1D23;1234,5');
  });

  it('exports hidden values (null) as empty cells', () => {
    expect(toCsv(columns, [{ plate: 'ABC1D23', cost: null }])).toContain('ABC1D23;');
  });

  it('quotes cells with separators or quotes', () => {
    const csv = toCsv([{ header: 'Texto', value: (r: { t: string }) => r.t }], [{ t: 'a;b "c"' }]);
    expect(csv).toContain('"a;b ""c"""');
  });

  it('neutralizes spreadsheet formulas', () => {
    const csv = toCsv([{ header: 'Texto', value: (r: { t: string }) => r.t }], [{ t: '=HYPERLINK("x")' }]);
    expect(csv).toContain(`"'=HYPERLINK(""x"")"`);
  });
});

describe('exportFileName', () => {
  it('slugifies the title and stamps the date', () => {
    expect(exportFileName('Desempenho da frota', 'xlsx', new Date(2026, 9, 5))).toBe('desempenho-da-frota-20261005.xlsx');
  });
});

describe('fetchAllPages', () => {
  it('reads every page until the last one', async () => {
    const pages = [[1, 2], [3]];
    const result = await fetchAllPages(async (page) => ({ items: pages[page - 1], totalPages: 2 }));
    expect(result.rows).toEqual([1, 2, 3]);
    expect(result.truncated).toBe(false);
  });

  it('stops at the export cap and says so', async () => {
    const result = await fetchAllPages(async () => ({ items: Array.from({ length: 100 }, (_, i) => i), totalPages: 999 }));
    expect(result.rows).toHaveLength(MAX_EXPORT_ROWS);
    expect(result.truncated).toBe(true);
  });
});
