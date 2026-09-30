import type { ReactNode } from 'react';
import { Group, Paper, ScrollArea, SimpleGrid, Stack, Tabs, Text } from '@mantine/core';
import { useSearchParams } from 'react-router-dom';

export interface DetailTab {
  value: string;
  label: string;
  icon?: ReactNode;
  /** Count badge (e.g. open occurrences). */
  count?: number;
  content: ReactNode;
}

/**
 * Tabs of a record hub (vehicle, driver). The active tab lives in the URL (?aba=), so links from the dashboard
 * land on the right section and the back button works.
 */
export function DetailTabs({ tabs }: { tabs: DetailTab[] }) {
  const [params, setParams] = useSearchParams();
  const active = tabs.some((t) => t.value === params.get('aba')) ? params.get('aba')! : tabs[0].value;

  return (
    <Tabs
      value={active}
      onChange={(v) => setParams((p) => { const next = new URLSearchParams(p); next.set('aba', v ?? tabs[0].value); return next; }, { replace: true })}
      keepMounted={false}
    >
      <ScrollArea type="never" mb="md">
        <Tabs.List style={{ flexWrap: 'nowrap' }}>
          {tabs.map((t) => (
            <Tabs.Tab key={t.value} value={t.value} leftSection={t.icon} rightSection={t.count ? <Text size="xs" fw={700} c="red">{t.count}</Text> : undefined}>
              {t.label}
            </Tabs.Tab>
          ))}
        </Tabs.List>
      </ScrollArea>
      {tabs.map((t) => (
        <Tabs.Panel key={t.value} value={t.value}>
          {t.content}
        </Tabs.Panel>
      ))}
    </Tabs>
  );
}

/** Label/value pairs of an overview card. */
export function InfoGrid({ items, cols = 3 }: { items: { label: string; value: ReactNode }[]; cols?: number }) {
  return (
    <SimpleGrid cols={{ base: 1, xs: 2, md: cols }} spacing="md" verticalSpacing="sm">
      {items.map((item) => (
        <div key={item.label}>
          <Text size="xs" c="dimmed" fw={500}>{item.label}</Text>
          <Text size="sm" component="div">{item.value ?? '—'}</Text>
        </div>
      ))}
    </SimpleGrid>
  );
}

/** Titled card inside a tab. `action` sits on the right (a secondary button). */
export function Section({ title, description, action, children }: { title: string; description?: string; action?: ReactNode; children: ReactNode }) {
  return (
    <Paper p={{ base: 'md', sm: 'lg' }}>
      <Stack gap="md">
        <Group justify="space-between" align="flex-start" gap="sm">
          <div>
            <Text fw={650}>{title}</Text>
            {description && <Text size="sm" c="dimmed">{description}</Text>}
          </div>
          {action}
        </Group>
        {children}
      </Stack>
    </Paper>
  );
}

/** Header fact of a hub page (big number / name with a caption). */
export function HeaderFact({ label, value, hint }: { label: string; value: ReactNode; hint?: ReactNode }) {
  return (
    <div style={{ minWidth: 0 }}>
      <Text size="xs" c="dimmed" fw={500} tt="uppercase">{label}</Text>
      <Text fw={650} size="lg" truncate component="div">{value}</Text>
      {hint && <Text size="xs" c="dimmed" component="div">{hint}</Text>}
    </div>
  );
}
