/**
 * Report exports (CSV, Excel, PDF) built from the SAME API data the screen shows — so filters, period, tenant and
 * permissions are exactly the screen's (a value hidden by permission comes back null and is exported empty).
 * Heavy libraries load only when the user exports.
 */
export type ExportValue = string | number | null | undefined;

export interface ExportColumn<T> {
  header: string;
  value: (row: T) => ExportValue;
}

export type ExportFormat = 'csv' | 'xlsx' | 'pdf';

/** Hard cap so a huge report cannot freeze the browser; the file says when it was cut. */
export const MAX_EXPORT_ROWS = 5000;

const CSV_SEPARATOR = ';'; // Excel pt-BR opens ";" CSV in columns.

function csvCell(value: ExportValue): string {
  if (value === null || value === undefined) return '';
  const text = typeof value === 'number' ? value.toLocaleString('pt-BR', { useGrouping: false, maximumFractionDigits: 4 }) : value;
  // Formula injection guard (CSV opened in a spreadsheet): never start a cell with = + - @ tab or CR.
  const safe = typeof value === 'string' && /^[=+\-@\t\r]/.test(text) ? `'${text}` : text;
  return /[";\n\r]/.test(safe) ? `"${safe.replace(/"/g, '""')}"` : safe;
}

export function toCsv<T>(columns: ExportColumn<T>[], rows: T[]): string {
  const lines = [columns.map((c) => csvCell(c.header)).join(CSV_SEPARATOR)];
  for (const row of rows) lines.push(columns.map((c) => csvCell(c.value(row))).join(CSV_SEPARATOR));
  return '﻿' + lines.join('\r\n'); // BOM: accents survive in Excel.
}

export function exportFileName(base: string, format: ExportFormat, now = new Date()): string {
  const slug = base.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
  const stamp = `${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}${String(now.getDate()).padStart(2, '0')}`;
  return `${slug || 'relatorio'}-${stamp}.${format}`;
}

function download(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export async function exportRows<T>(options: {
  format: ExportFormat; title: string; subtitle?: string; columns: ExportColumn<T>[]; rows: T[]; truncated?: boolean;
}) {
  const { format, title, columns, rows } = options;
  const subtitle = [options.subtitle, options.truncated ? `Exportação limitada às primeiras ${MAX_EXPORT_ROWS} linhas.` : null]
    .filter(Boolean).join(' · ');
  const fileName = exportFileName(title, format);

  if (format === 'csv') {
    download(new Blob([toCsv(columns, rows)], { type: 'text/csv;charset=utf-8' }), fileName);
    return;
  }

  if (format === 'xlsx') {
    const { default: writeXlsxFile } = await import('write-excel-file');
    const header = columns.map((c) => ({ value: c.header, fontWeight: 'bold' as const }));
    const body = rows.map((row) => columns.map((c) => {
      const v = c.value(row);
      if (v === null || v === undefined || v === '') return null;
      return typeof v === 'number' ? { type: Number, value: v } : { type: String, value: v };
    }));
    const sheet = [header, ...body];
    if (subtitle) sheet.push([], [{ value: subtitle, type: String }]);
    const blob = await writeXlsxFile(sheet as never, { sheet: title.slice(0, 31) });
    download(blob as Blob, fileName);
    return;
  }

  const [{ jsPDF }, { default: autoTable }] = await Promise.all([import('jspdf'), import('jspdf-autotable')]);
  const doc = new jsPDF({ orientation: columns.length > 6 ? 'landscape' : 'portrait', unit: 'pt', format: 'a4' });
  doc.setFontSize(14);
  doc.text(title, 40, 40);
  doc.setFontSize(9);
  doc.text(`${subtitle ? subtitle + ' · ' : ''}Gerado em ${new Date().toLocaleString('pt-BR')}`, 40, 56);
  autoTable(doc, {
    startY: 68,
    head: [columns.map((c) => c.header)],
    body: rows.map((row) => columns.map((c) => {
      const v = c.value(row);
      return v === null || v === undefined ? '' : typeof v === 'number' ? v.toLocaleString('pt-BR', { maximumFractionDigits: 2 }) : v;
    })),
    styles: { fontSize: 8, cellPadding: 3 },
    headStyles: { fillColor: [40, 40, 40] },
  });
  doc.save(fileName);
}

/** Reads every page of a paged endpoint (pageSize 100) up to MAX_EXPORT_ROWS. */
export async function fetchAllPages<T>(
  fetchPage: (page: number, pageSize: number) => Promise<{ items: T[]; totalPages: number }>,
): Promise<{ rows: T[]; truncated: boolean }> {
  const rows: T[] = [];
  for (let page = 1; ; page++) {
    const result = await fetchPage(page, 100);
    rows.push(...result.items);
    if (rows.length >= MAX_EXPORT_ROWS) return { rows: rows.slice(0, MAX_EXPORT_ROWS), truncated: true };
    if (page >= result.totalPages || result.items.length === 0) return { rows, truncated: false };
  }
}
