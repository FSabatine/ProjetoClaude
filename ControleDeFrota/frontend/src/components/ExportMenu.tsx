import { useState } from 'react';
import { Button, Menu } from '@mantine/core';
import { IconDownload, IconFileSpreadsheet, IconFileText, IconFileTypePdf } from '@tabler/icons-react';
import { exportRows, type ExportColumn, type ExportFormat } from '../lib/export';
import { notifyError, notifySuccess } from './notify';

export interface ExportSource<T> {
  /** File/sheet title, e.g. "Desempenho da frota". */
  title: string;
  /** Period/filters in words, printed in the file. */
  subtitle?: string;
  columns: ExportColumn<T>[];
  /** Same endpoint and filters as the screen (permissions and tenant come with it). */
  load: () => Promise<{ rows: T[]; truncated: boolean }>;
}

/** "Exportar" button: CSV, Excel or PDF of the report currently on screen. */
export function ExportMenu<T>({ source, disabled }: { source: ExportSource<T>; disabled?: boolean }) {
  const [busy, setBusy] = useState<ExportFormat | null>(null);

  const run = async (format: ExportFormat) => {
    setBusy(format);
    try {
      const { rows, truncated } = await source.load();
      await exportRows({ format, title: source.title, subtitle: source.subtitle, columns: source.columns, rows, truncated });
      notifySuccess(truncated ? 'Arquivo gerado com as primeiras 5.000 linhas. Use filtros para exportar o restante.' : 'Arquivo gerado.');
    } catch (error) {
      notifyError(error, 'O arquivo não foi gerado');
    } finally {
      setBusy(null);
    }
  };

  return (
    <Menu position="bottom-end" shadow="md" withinPortal>
      <Menu.Target>
        <Button variant="default" size="xs" leftSection={<IconDownload size={14} />} loading={busy !== null} disabled={disabled}>Exportar</Button>
      </Menu.Target>
      <Menu.Dropdown>
        <Menu.Item leftSection={<IconFileText size={16} />} onClick={() => void run('csv')}>CSV (planilha simples)</Menu.Item>
        <Menu.Item leftSection={<IconFileSpreadsheet size={16} />} onClick={() => void run('xlsx')}>Excel (.xlsx)</Menu.Item>
        <Menu.Item leftSection={<IconFileTypePdf size={16} />} onClick={() => void run('pdf')}>PDF</Menu.Item>
      </Menu.Dropdown>
    </Menu>
  );
}
