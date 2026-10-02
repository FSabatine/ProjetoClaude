import { useState } from 'react';
import { Box, Button, Group, Paper, Stack, Table, Text, Tooltip } from '@mantine/core';
import { IconChartBar, IconTable } from '@tabler/icons-react';

export interface ColumnPoint {
  key: string;
  label: string;
  /** null = no data for this bucket (drawn as a gap, never as zero). */
  value: number | null;
}

/**
 * Single-series column chart (dataviz skill): one validated hue (indigo 6 light / 5 dark), columns ≤ 24px with 4px
 * rounded tops on a single baseline, hairline gridlines, no legend (the title names the series), one direct label on
 * the highest column, a hover/focus tooltip per column and a table view with every number — the chart never gates data.
 */
export function ColumnChart({ title, description, points, format, emptyText = 'Sem dados no período.', height = 180 }: {
  title: string;
  description?: string;
  points: ColumnPoint[];
  format: (value: number) => string;
  emptyText?: string;
  height?: number;
}) {
  const [asTable, setAsTable] = useState(false);
  const values = points.map((p) => p.value).filter((v): v is number => v !== null);
  const max = values.length ? Math.max(...values) : 0;
  const top = niceCeiling(max);
  const highest = points.find((p) => p.value === max && max > 0)?.key;

  return (
    <Paper p={{ base: 'md', sm: 'lg' }}>
      <Stack gap="sm">
        <Group justify="space-between" align="flex-start" gap="xs">
          <div>
            <Text fw={650}>{title}</Text>
            {description && <Text size="sm" c="dimmed">{description}</Text>}
          </div>
          <Button variant="subtle" color="gray" size="compact-sm" leftSection={asTable ? <IconChartBar size={16} /> : <IconTable size={16} />}
            onClick={() => setAsTable((v) => !v)} aria-pressed={asTable}>
            {asTable ? 'Ver gráfico' : 'Ver tabela'}
          </Button>
        </Group>

        {values.length === 0 ? (
          <Text size="sm" c="dimmed" py="lg" ta="center">{emptyText}</Text>
        ) : asTable ? (
          <Table.ScrollContainer minWidth={280}>
            <Table striped withRowBorders={false} fz="sm">
              <Table.Tbody>
                {points.map((p) => (
                  <Table.Tr key={p.key}>
                    <Table.Td>{p.label}</Table.Td>
                    <Table.Td ta="right" fw={500}>{p.value === null ? '—' : format(p.value)}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        ) : (
          <Box style={{ overflowX: 'auto' }}>
            <Box role="img" aria-label={`${title}. Use "Ver tabela" para os valores.`} style={{ minWidth: Math.max(280, points.length * 28) }}>
              <Box pos="relative" h={height} mt="lg" style={{ borderBottom: '1px solid var(--mantine-color-default-border)' }}>
                {[0.5, 1].map((f) => (
                  <Box key={f} pos="absolute" left={0} right={0} bottom={`${f * 100}%`}
                    style={{ borderTop: '1px solid var(--mantine-color-default-border)', opacity: 0.6 }}>
                    <Text size="xs" c="dimmed" pos="absolute" left={0} top={-18}>{format(top * f)}</Text>
                  </Box>
                ))}
                <Group h="100%" gap={2} wrap="nowrap" align="flex-end" justify="space-around" pos="relative" px={4}>
                  {points.map((p) => (
                    <Tooltip key={p.key} label={`${p.label}: ${p.value === null ? 'sem dados' : format(p.value)}`} withinPortal>
                      <Box tabIndex={0} aria-label={`${p.label}: ${p.value === null ? 'sem dados' : format(p.value)}`}
                        h="100%" style={{ flex: 1, display: 'flex', alignItems: 'flex-end', justifyContent: 'center', position: 'relative', outlineOffset: 2 }}>
                        {p.key === highest && p.value !== null && (
                          <Text size="xs" fw={600} pos="absolute" style={{ bottom: `calc(${(p.value / top) * 100}% + 4px)`, whiteSpace: 'nowrap' }}>
                            {format(p.value)}
                          </Text>
                        )}
                        {p.value !== null && (
                          <Box w="100%" maw={24} h={`${Math.max(1, (p.value / top) * 100)}%`}
                            style={{ background: 'light-dark(var(--mantine-color-indigo-6), var(--mantine-color-indigo-5))', borderRadius: '4px 4px 0 0' }} />
                        )}
                      </Box>
                    </Tooltip>
                  ))}
                </Group>
              </Box>
              <Group gap={2} wrap="nowrap" justify="space-around" px={4} mt={4}>
                {points.map((p, i) => (
                  <Text key={p.key} size="xs" c="dimmed" ta="center" style={{ flex: 1, minWidth: 0 }} truncate>
                    {/* Every other label on long series, so labels never collide. */}
                    {points.length <= 12 || i % 2 === 0 ? p.label : ''}
                  </Text>
                ))}
              </Group>
            </Box>
          </Box>
        )}
      </Stack>
    </Paper>
  );
}

/** Clean axis top (1, 2, 2.5, 5 × 10ⁿ) so gridline labels are round numbers. */
function niceCeiling(value: number): number {
  if (value <= 0) return 1;
  const magnitude = 10 ** Math.floor(Math.log10(value));
  const step = [1, 2, 2.5, 5, 10].find((s) => s * magnitude >= value) ?? 10;
  return step * magnitude;
}
