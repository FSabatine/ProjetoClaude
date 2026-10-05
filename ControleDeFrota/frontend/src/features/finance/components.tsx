import type { ReactNode } from 'react';
import { ActionIcon, Alert, Group, Paper, Popover, Progress, Text } from '@mantine/core';
import { IconHelpCircle, IconInfoCircle } from '@tabler/icons-react';
import { StatusBadge } from '../../components/common';
import { BUDGET_STATUS, PAYMENT_STATUS, type BudgetStatus, type PaymentStatus } from './finance';

/** Contextual "[?]" next to a calculated metric — short plain-language explanation, no technical detail. */
export function InfoHint({ label, children }: { label: string; children: ReactNode }) {
  return (
    <Popover width={300} position="bottom-start" withArrow shadow="md">
      <Popover.Target>
        <ActionIcon variant="subtle" color="gray" size="sm" aria-label={`O que é ${label}?`}>
          <IconHelpCircle size={15} />
        </ActionIcon>
      </Popover.Target>
      <Popover.Dropdown>
        <Text size="sm" fw={600} mb={4}>{label}</Text>
        <Text size="sm" c="dimmed" component="div">{children}</Text>
      </Popover.Dropdown>
    </Popover>
  );
}

export function KpiTile({ label, value, hint, help }: { label: string; value: ReactNode; hint?: ReactNode; help?: ReactNode }) {
  return (
    <Paper p="md">
      <Group gap={2} wrap="nowrap">
        <Text size="xs" c="dimmed" fw={500} tt="uppercase">{label}</Text>
        {help && <InfoHint label={label}>{help}</InfoHint>}
      </Group>
      <Text fw={650} fz={{ base: 20, sm: 24 }} mt={2}>{value}</Text>
      {hint && <Text size="xs" c="dimmed">{hint}</Text>}
    </Paper>
  );
}

export function PaymentStatusBadge({ value }: { value: PaymentStatus }) {
  return <StatusBadge value={value} map={PAYMENT_STATUS} />;
}

export function BudgetStatusBadge({ value }: { value: BudgetStatus }) {
  return <StatusBadge value={value} map={BUDGET_STATUS} />;
}

const BUDGET_PROGRESS_COLOR: Record<BudgetStatus, string> = {
  UnderBudget: 'teal', NearBudget: 'orange', OverBudget: 'red', NoBudget: 'gray',
};

export function BudgetProgress({ utilizationPercent, status }: { utilizationPercent: number | null; status: BudgetStatus }) {
  if (utilizationPercent === null) return <Text size="sm" c="dimmed">Sem orçamento cadastrado para o período.</Text>;
  return <Progress value={Math.min(100, utilizationPercent)} color={BUDGET_PROGRESS_COLOR[status]} size="lg" radius="sm" />;
}

/** Shown whenever a blended total (combining fuel/maintenance/tires/manual expenses) is missing a slice by permission. */
export function PartialTotalsNotice() {
  return (
    <Alert color="gray" variant="light" icon={<IconInfoCircle size={16} />} p="xs">
      Totais parciais: você não tem permissão para ver os custos de todas as fontes somadas aqui.
    </Alert>
  );
}

export const COST_PER_KM_HELP = (
  <>Divide o custo total do período pelos quilômetros realmente rodados nele, a partir do histórico de hodômetro. Sem quilometragem suficiente, o sistema mostra isso em vez de um número.</>
);

export const TCO_HELP = (
  <>Soma o valor de aquisição do veículo com todo o custo operacional acumulado desde então. É uma análise de gestão operacional, não um cálculo contábil de depreciação.</>
);

export function InsufficientDataText({ text = 'Dados de quilometragem insuficientes para este período.' }: { text?: string }) {
  return <Text size="sm" c="dimmed">{text}</Text>;
}
