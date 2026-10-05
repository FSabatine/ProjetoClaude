import { useEffect, useMemo, useState } from 'react';
import {
  Anchor, Badge, Group, Kbd, Loader, Modal, Paper, RingProgress, ScrollArea, Skeleton, Stack, Text, TextInput, ThemeIcon, Tooltip,
  UnstyledButton,
} from '@mantine/core';
import { useDebouncedValue, useHotkeys } from '@mantine/hooks';
import { IconBulb, IconSearch, IconTrendingDown, IconTrendingUp } from '@tabler/icons-react';
import { Link, useNavigate } from 'react-router-dom';
import classes from '../alerts/alerts.module.css';
import { analyticsApi, HEALTH_AREA_LABEL, HEALTH_LEVEL, HEALTH_STATUS, SEARCH_TYPE_LABEL, type SearchResult } from './analytics';

/** Health score in the vehicle hub header; click explains every factor (spec §16). */
export function VehicleHealthFact({ vehicleId }: { vehicleId: string }) {
  const query = analyticsApi.useHealth(vehicleId);
  const [opened, setOpened] = useState(false);
  const h = query.data;
  if (!h) return <Skeleton height={44} />;
  const level = HEALTH_LEVEL[h.level];
  return (
    <>
      <UnstyledButton onClick={() => setOpened(true)} aria-label={`Saúde operacional ${h.score} de 100. Ver como foi calculada`}>
        <Text size="xs" c="dimmed" fw={500} tt="uppercase">Saúde operacional</Text>
        <Group gap={6} wrap="nowrap">
          <Text fw={650} size="lg">{h.score}/100</Text>
          <Badge color={level.color} variant="light">{level.label}</Badge>
        </Group>
        <Text size="xs" c="blue">ver fatores</Text>
      </UnstyledButton>
      <Modal opened={opened} onClose={() => setOpened(false)} title="Saúde operacional do veículo" centered size="lg">
        <Stack gap="md">
          <Group gap="md">
            <RingProgress size={84} thickness={8} sections={[{ value: h.score, color: level.color }]}
              label={<Text ta="center" fw={700}>{h.score}</Text>} />
            <Text size="sm" c="dimmed" style={{ flex: 1 }}>
              Um resumo prático, não uma medida científica: começa em 100 e cada área em atenção tira 10 pontos e cada área crítica tira 25.
              Áreas que você não pode ver não entram na conta.
            </Text>
          </Group>
          <Stack gap="xs">
            {h.factors.map((f) => (
              <Paper key={f.area} withBorder p="sm">
                <Group justify="space-between" wrap="nowrap" align="flex-start">
                  <div style={{ minWidth: 0 }}>
                    <Text fw={600} size="sm">{HEALTH_AREA_LABEL[f.area]}</Text>
                    <Text size="sm" c="dimmed">{f.explanation}</Text>
                    {f.tab && f.status !== 'NotVisible' && (
                      <Anchor component={Link} to={`?aba=${f.tab}`} size="xs" onClick={() => setOpened(false)}>abrir a aba</Anchor>
                    )}
                  </div>
                  <Badge color={HEALTH_STATUS[f.status].color} variant="light">{HEALTH_STATUS[f.status].label}</Badge>
                </Group>
              </Paper>
            ))}
          </Stack>
        </Stack>
      </Modal>
    </>
  );
}

/** "Destaques" on the dashboard: a few findings computed by the system, each with its numbers and a link. */
export function InsightsPanel() {
  const query = analyticsApi.useInsights();
  const items = query.data ?? [];
  if (query.data && items.length === 0) return null;
  return (
    <Paper p={{ base: 'md', sm: 'lg' }}>
      <Group gap="xs" mb="sm">
        <ThemeIcon variant="light" size={28} radius="md"><IconBulb size={16} /></ThemeIcon>
        <Text fw={650}>Destaques</Text>
        <Text size="xs" c="dimmed">comparação dos últimos 30 dias com os 30 anteriores, calculada pelo sistema</Text>
      </Group>
      {!query.data ? <Stack gap="xs">{[1, 2].map((i) => <Skeleton key={i} height={52} />)}</Stack> : (
        <Stack gap={6}>
          {items.map((i) => {
            const negative = i.sentiment === 'Negative';
            const Icon = i.direction === 'Down' ? IconTrendingDown : i.direction === 'Up' ? IconTrendingUp : IconBulb;
            return (
              <Group key={i.key} wrap="nowrap" align="flex-start" gap="sm" className={classes.row}>
                <ThemeIcon variant="light" color={negative ? 'orange' : i.sentiment === 'Positive' ? 'teal' : 'gray'} size={32} radius="md"><Icon size={18} /></ThemeIcon>
                <div style={{ minWidth: 0 }}>
                  <Text size="sm" fw={600}>{i.link ? <Anchor component={Link} to={i.link} inherit>{i.title}</Anchor> : i.title}</Text>
                  <Text size="sm" c="dimmed">{i.explanation}</Text>
                  <Tooltip label={i.evidence} multiline w={320}><Text size="xs" c="dimmed" td="underline dotted" w="fit-content">base do cálculo</Text></Tooltip>
                </div>
              </Group>
            );
          })}
        </Stack>
      )}
    </Paper>
  );
}

/** Header search (Ctrl+K): every record type the user can see, grouped by type. */
export function GlobalSearch() {
  const [opened, setOpened] = useState(false);
  useHotkeys([['mod+K', () => setOpened(true)]]);
  return (
    <>
      <Tooltip label="Buscar (Ctrl+K)">
        <UnstyledButton onClick={() => setOpened(true)} aria-label="Buscar em todo o sistema"
          style={{ border: '1px solid var(--mantine-color-default-border)', borderRadius: 'var(--mantine-radius-md)', padding: '6px 10px' }}>
          <Group gap={8} wrap="nowrap">
            <IconSearch size={16} />
            <Text size="sm" c="dimmed" visibleFrom="md">Buscar placa, motorista, OS…</Text>
            <Kbd size="xs" visibleFrom="md">Ctrl K</Kbd>
          </Group>
        </UnstyledButton>
      </Tooltip>
      {opened && <SearchModal onClose={() => setOpened(false)} />}
    </>
  );
}

function SearchModal({ onClose }: { onClose: () => void }) {
  const [text, setText] = useState('');
  const [debounced] = useDebouncedValue(text, 250);
  const query = analyticsApi.useSearch(debounced);
  const navigate = useNavigate();
  const groups = useMemo(() => {
    const map = new Map<string, SearchResult[]>();
    for (const r of query.data?.results ?? []) map.set(r.type, [...(map.get(r.type) ?? []), r]);
    return [...map.entries()];
  }, [query.data]);
  const flat = groups.flatMap(([, rs]) => rs);
  const [active, setActive] = useState(0);
  useEffect(() => setActive(0), [query.data]);

  const go = (r: SearchResult) => { onClose(); navigate(r.link); };
  const tooShort = debounced.trim().length < 2;

  return (
    <Modal opened onClose={onClose} withCloseButton={false} size="lg" padding={0} yOffset="10vh">
      <TextInput autoFocus size="md" variant="unstyled" px="md" py="xs" placeholder="Busque por placa, motorista, nº de fogo, OS, despesa, documento…"
        leftSection={query.isFetching ? <Loader size={16} /> : <IconSearch size={18} />} value={text}
        onChange={(e) => setText(e.currentTarget.value)} aria-label="Buscar"
        onKeyDown={(e) => {
          if (e.key === 'ArrowDown') { e.preventDefault(); setActive((a) => Math.min(a + 1, flat.length - 1)); }
          if (e.key === 'ArrowUp') { e.preventDefault(); setActive((a) => Math.max(a - 1, 0)); }
          if (e.key === 'Enter' && flat[active]) go(flat[active]);
        }} />
      <ScrollArea.Autosize mah="60vh" style={{ borderTop: '1px solid var(--mantine-color-default-border)' }}>
        <Stack gap={0} p="xs">
          {tooShort && <Text size="sm" c="dimmed" p="sm">Digite ao menos 2 caracteres. Só aparecem registros que você tem permissão para ver.</Text>}
          {!tooShort && query.data && flat.length === 0 && <Text size="sm" c="dimmed" p="sm">Nada encontrado para "{debounced}".</Text>}
          {!tooShort && groups.map(([type, rs]) => (
            <div key={type}>
              <Text size="xs" fw={700} c="dimmed" tt="uppercase" px="sm" pt="sm" pb={4}>{SEARCH_TYPE_LABEL[type as keyof typeof SEARCH_TYPE_LABEL]}</Text>
              {rs.map((r) => {
                const index = flat.indexOf(r);
                return (
                  <UnstyledButton key={`${r.type}-${r.link}-${r.title}`} className={classes.row} onClick={() => go(r)} onMouseEnter={() => setActive(index)}
                    style={{ background: index === active ? 'var(--mantine-color-default-hover)' : undefined }}>
                    <Group justify="space-between" wrap="nowrap">
                      <div style={{ minWidth: 0 }}>
                        <Text size="sm" fw={600} truncate>{r.title}</Text>
                        {r.subtitle && <Text size="xs" c="dimmed" truncate>{r.subtitle}</Text>}
                      </div>
                      <Badge variant="light" color="gray">{SEARCH_TYPE_LABEL[r.type]}</Badge>
                    </Group>
                  </UnstyledButton>
                );
              })}
            </div>
          ))}
        </Stack>
      </ScrollArea.Autosize>
    </Modal>
  );
}
