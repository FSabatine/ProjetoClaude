import { Badge, Card, Group, Stack, Text, UnstyledButton } from '@mantine/core';
import { useMediaQuery } from '@mantine/hooks';
import { IconAlertOctagon, IconAlertTriangle, IconCircleCheck, IconCircleDashed, IconHelpCircle } from '@tabler/icons-react';
import { formatTread, groupPositions, TREAD_LEVEL_TEXT, treadLevel, type TreadLevel } from '../../lib/tires';
import { AXLE_TYPE, type AssetTirePosition } from './tires';
import classes from './TireLayoutDiagram.module.css';

type Level = TreadLevel | 'vacant';

/** Icon per level: the state is never told by color alone (seção 57). */
const LEVEL_ICON = { ok: IconCircleCheck, warning: IconAlertTriangle, critical: IconAlertOctagon, unknown: IconHelpCircle, vacant: IconCircleDashed };
const LEVEL_BADGE = { ok: 'teal', warning: 'orange', critical: 'red', unknown: 'gray', vacant: 'gray' } as const;

export function levelOf(p: AssetTirePosition, minMm: number, warningMm: number): Level {
  if (!p.tire) return 'vacant';
  const tread = treadLevel(p.tire.currentTreadDepthMm, minMm, warningMm);
  // A critical alert (damage) is as urgent as a tread at the minimum.
  return p.tire.alerts.some((a) => a.severity === 'Critical') ? 'critical' : tread;
}

export const LEVEL_TEXT: Record<Level, string> = { ...TREAD_LEVEL_TEXT, critical: 'Requer ação', vacant: 'Posição vazia' };

interface Props {
  positions: AssetTirePosition[];
  minTreadMm: number;
  warningTreadMm: number;
  selectedCode: string | null;
  onSelect: (code: string) => void;
}

/**
 * The vehicle/implement seen from above (seções 30, 31, 56): generated from the axle configuration — never drawn for one
 * vehicle type. Each wheel is a button with the tire, its tread and an icon of its condition. On phones the same data is a
 * list grouped by axle (seção 58), so nobody has to zoom into a drawing.
 */
export function TireLayoutDiagram({ positions, minTreadMm, warningTreadMm, selectedCode, onSelect }: Props) {
  const isPhone = useMediaQuery('(max-width: 48em)');
  const grouped = groupPositions(positions.map((p) => ({ ...p.position, item: p })));
  const level = (p: AssetTirePosition) => levelOf(p, minTreadMm, warningTreadMm);

  if (isPhone) {
    return (
      <Stack gap="sm">
        {grouped.axles.map((axle) => (
          <Stack key={axle.number} gap={6}>
            <Text size="xs" fw={600} c="dimmed" tt="uppercase">
              Eixo {axle.number}{axle.left[0]?.axleType ? ` · ${AXLE_TYPE[axle.left[0].axleType].label}` : ''}
            </Text>
            {[...axle.left, ...axle.right].map((p) => (
              <PositionCard key={p.code} item={p.item} level={level(p.item)} selected={selectedCode === p.code} onSelect={onSelect} />
            ))}
          </Stack>
        ))}
        {grouped.spares.length > 0 && (
          <Stack gap={6}>
            <Text size="xs" fw={600} c="dimmed" tt="uppercase">Estepe</Text>
            {grouped.spares.map((p) => (
              <PositionCard key={p.code} item={p.item} level={level(p.item)} selected={selectedCode === p.code} onSelect={onSelect} />
            ))}
          </Stack>
        )}
      </Stack>
    );
  }

  return (
    <Stack gap="xs" align="center">
      <Text size="xs" fw={700} c="dimmed" tt="uppercase">Frente</Text>
      <div className={classes.body} role="group" aria-label="Diagrama de pneus visto de cima">
        {grouped.axles.map((axle) => (
          <div key={axle.number} className={classes.axle}>
            <div className={classes.side}>
              {axle.left.map((p) => <Wheel key={p.code} item={p.item} level={level(p.item)} selected={selectedCode === p.code} onSelect={onSelect} />)}
            </div>
            <div className={classes.bar} aria-hidden>
              <Text size="xs" c="dimmed" className={classes.barLabel}>
                Eixo {axle.number}{axle.left[0]?.axleType ? ` · ${AXLE_TYPE[axle.left[0].axleType].label}` : ''}
              </Text>
            </div>
            <div className={classes.side}>
              {axle.right.map((p) => <Wheel key={p.code} item={p.item} level={level(p.item)} selected={selectedCode === p.code} onSelect={onSelect} />)}
            </div>
          </div>
        ))}
        {grouped.spares.length > 0 && (
          <div className={classes.spares}>
            <Text size="xs" c="dimmed">Estepe</Text>
            {grouped.spares.map((p) => <Wheel key={p.code} item={p.item} level={level(p.item)} selected={selectedCode === p.code} onSelect={onSelect} />)}
          </div>
        )}
      </div>
      <Text size="xs" fw={700} c="dimmed" tt="uppercase">Traseira</Text>
      <Legend />
    </Stack>
  );
}

function describe(item: AssetTirePosition, level: Level) {
  const where = item.position.label;
  if (!item.tire) return `${where}: ${item.position.isRequired ? 'posição vazia' : 'posição opcional vazia'}`;
  return `${where}: pneu ${item.tire.code}, sulco ${formatTread(item.tire.currentTreadDepthMm)}, ${LEVEL_TEXT[level].toLowerCase()}`;
}

function Wheel({ item, level, selected, onSelect }: { item: AssetTirePosition; level: Level; selected: boolean; onSelect: (code: string) => void }) {
  const Icon = LEVEL_ICON[level];
  return (
    <UnstyledButton
      className={[classes.tire, classes[level], selected ? classes.selected : '', !item.tire ? classes.empty : ''].join(' ')}
      onClick={() => onSelect(item.position.code)}
      aria-label={describe(item, level)}
      aria-pressed={selected}
      title={describe(item, level)}
    >
      <Text size="10px" fw={700} c="dimmed">{item.position.code}</Text>
      <Icon size={18} aria-hidden />
      <Text size="10px" fw={600} ta="center" lh={1.1}>
        {item.tire ? formatTread(item.tire.currentTreadDepthMm) : item.position.isRequired ? 'vazio' : 'opcional'}
      </Text>
    </UnstyledButton>
  );
}

function PositionCard({ item, level, selected, onSelect }: { item: AssetTirePosition; level: Level; selected: boolean; onSelect: (code: string) => void }) {
  const Icon = LEVEL_ICON[level];
  return (
    <Card padding="sm" withBorder onClick={() => onSelect(item.position.code)} style={{ cursor: 'pointer', borderWidth: selected ? 2 : 1 }}
      component="button" aria-pressed={selected} aria-label={describe(item, level)}>
      <Group justify="space-between" wrap="nowrap" gap="xs">
        <div style={{ minWidth: 0, textAlign: 'left' }}>
          <Text size="sm" fw={600}>{item.position.label}</Text>
          <Text size="xs" c="dimmed" truncate>
            {item.tire ? `${item.tire.code} · ${item.tire.brand} ${item.tire.modelName}` : item.position.isRequired ? 'Sem pneu' : 'Sem pneu (opcional)'}
          </Text>
        </div>
        <Badge color={LEVEL_BADGE[level]} variant="light" leftSection={<Icon size={12} />} style={{ flexShrink: 0 }}>
          {item.tire ? formatTread(item.tire.currentTreadDepthMm) : 'vazio'}
        </Badge>
      </Group>
    </Card>
  );
}

function Legend() {
  const items: Level[] = ['ok', 'warning', 'critical', 'unknown', 'vacant'];
  return (
    <Group gap="md" justify="center" wrap="wrap" aria-label="Legenda">
      {items.map((l) => {
        const Icon = LEVEL_ICON[l];
        return (
          <Group key={l} gap={4} wrap="nowrap">
            <Icon size={14} color={`var(--mantine-color-${LEVEL_BADGE[l]}-filled)`} aria-hidden />
            <Text size="xs" c="dimmed">{LEVEL_TEXT[l]}</Text>
          </Group>
        );
      })}
    </Group>
  );
}
