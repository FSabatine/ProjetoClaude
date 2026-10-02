import { useEffect, useState } from 'react';
import { ActionIcon, Badge, Button, Group, Modal, NumberInput, Paper, Select, SimpleGrid, Skeleton, Stack, Switch, Table, Text, TextInput } from '@mantine/core';
import { IconPencil, IconPlus, IconTrash } from '@tabler/icons-react';
import { toApiError } from '../../api/errors';
import { toSelectData } from '../../components/common';
import { Section } from '../../components/DetailLayout';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { ErrorState } from '../../components/States';
import { fuelSettingsApi, fuelTypesApi } from './api';
import { FUEL_CATEGORY, FUEL_UNIT, type FuelCategory, type FuelSettings, type FuelType, type FuelUnit } from './fuel';

interface TypeForm {
  id?: string;
  name: string;
  code: string;
  category: FuelCategory | null;
  unit: FuelUnit;
  isActive: boolean;
  description: string;
  fuelingCount: number;
}

const EMPTY: TypeForm = { name: '', code: '', category: 'Diesel', unit: 'Liter', isActive: true, description: '', fuelingCount: 0 };

/** /configuracoes/combustivel: the fuel catalog (never hard-coded, ADR-031) and the anomaly thresholds (seção 21). */
export function FuelSettingsPage() {
  return (
    <>
      <PageHeader title="Combustível" description="Tipos de combustível usados pela frota e os limites que geram alertas de revisão."
        breadcrumbs={[{ label: 'Configurações' }, { label: 'Combustível' }]} />
      <Stack gap="md">
        <FuelTypesSection />
        <ThresholdsSection />
      </Stack>
    </>
  );
}

function FuelTypesSection() {
  const query = fuelTypesApi.useList(true);
  const [form, setForm] = useState<TypeForm | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const save = fuelTypesApi.useSave(form?.id);
  const remove = fuelTypesApi.useRemove();

  const submit = () => form && save.mutate(
    { name: form.name, code: form.code, category: form.category!, unit: form.unit, isActive: form.isActive, description: form.description || null },
    {
      onSuccess: () => { notifySuccess('Combustível salvo.'); setForm(null); },
      onError: (e) => { setErrors(toApiError(e).fieldErrors); notifyError(e, 'O combustível não foi salvo'); },
    });

  return (
    <Section title="Tipos de combustível" description="O consumo é calculado em km por unidade do combustível (L, m³ ou kWh)."
      action={<Button leftSection={<IconPlus size={18} />} onClick={() => { setErrors({}); setForm({ ...EMPTY }); }}>Novo combustível</Button>}>
      {query.isLoading && <Skeleton height={200} />}
      {query.error && <ErrorState error={query.error} onRetry={() => void query.refetch()} />}
      {query.data && (
        <Table.ScrollContainer minWidth={520}>
          <Table fz="sm" highlightOnHover>
            <Table.Thead><Table.Tr><Table.Th>Nome</Table.Th><Table.Th>Código</Table.Th><Table.Th>Categoria</Table.Th><Table.Th>Unidade</Table.Th><Table.Th ta="right">Abastecimentos</Table.Th><Table.Th>Situação</Table.Th><Table.Th /></Table.Tr></Table.Thead>
            <Table.Tbody>
              {query.data.map((t: FuelType) => (
                <Table.Tr key={t.id}>
                  <Table.Td fw={500}>{t.name}</Table.Td>
                  <Table.Td ff="monospace">{t.code}</Table.Td>
                  <Table.Td>{FUEL_CATEGORY[t.category].label}</Table.Td>
                  <Table.Td>{FUEL_UNIT[t.unit].label}</Table.Td>
                  <Table.Td ta="right">{t.fuelingCount.toLocaleString('pt-BR')}</Table.Td>
                  <Table.Td><Badge color={t.isActive ? 'teal' : 'gray'}>{t.isActive ? 'Ativo' : 'Inativo'}</Badge></Table.Td>
                  <Table.Td w={88}>
                    <Group gap={4} wrap="nowrap">
                      <ActionIcon variant="subtle" color="gray" aria-label={`Editar ${t.name}`} onClick={() => { setErrors({}); setForm({ ...t, description: t.description ?? '' }); }}>
                        <IconPencil size={16} />
                      </ActionIcon>
                      {t.fuelingCount === 0 && (
                        <ActionIcon variant="subtle" color="red" aria-label={`Excluir ${t.name}`} onClick={() => confirmDelete({
                          entity: 'combustível', name: t.name,
                          onConfirm: () => remove.mutate(t.id, { onSuccess: () => notifySuccess('Combustível excluído.'), onError: (e) => notifyError(e, 'O combustível não foi excluído') }),
                        })}><IconTrash size={16} /></ActionIcon>
                      )}
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
      <Modal opened={!!form} onClose={() => setForm(null)} centered title={form?.id ? 'Editar combustível' : 'Novo combustível'}>
        {form && (
          <Stack>
            <TextInput label="Nome" withAsterisk maxLength={60} value={form.name} error={errors.name} data-autofocus
              onChange={(e) => setForm({ ...form, name: e.currentTarget.value })} />
            <SimpleGrid cols={2}>
              <TextInput label="Código" withAsterisk maxLength={20} value={form.code} error={errors.code} description="Ex.: S10"
                onChange={(e) => setForm({ ...form, code: e.currentTarget.value.toUpperCase() })} />
              <Select label="Categoria" withAsterisk data={toSelectData(FUEL_CATEGORY)} value={form.category} error={errors.category}
                onChange={(v) => setForm({ ...form, category: v as FuelCategory })} />
            </SimpleGrid>
            <Select label="Unidade" withAsterisk data={toSelectData(FUEL_UNIT)} value={form.unit} error={errors.unit}
              disabled={form.fuelingCount > 0} description={form.fuelingCount > 0 ? 'Não muda depois de usado: mudaria o significado do consumo já calculado.' : undefined}
              onChange={(v) => setForm({ ...form, unit: v as FuelUnit })} />
            <TextInput label="Descrição" maxLength={300} value={form.description} onChange={(e) => setForm({ ...form, description: e.currentTarget.value })} />
            <Switch label="Ativo" description="Combustíveis inativos não aparecem para novos abastecimentos." checked={form.isActive}
              onChange={(e) => setForm({ ...form, isActive: e.currentTarget.checked })} />
            <Group justify="flex-end">
              <Button variant="default" onClick={() => setForm(null)}>Cancelar</Button>
              <Button loading={save.isPending} onClick={submit}>Salvar</Button>
            </Group>
          </Stack>
        )}
      </Modal>
    </Section>
  );
}

function ThresholdsSection() {
  const query = fuelSettingsApi.useGet();
  const save = fuelSettingsApi.useSave();
  const [values, setValues] = useState<FuelSettings | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  useEffect(() => { if (query.data) setValues(query.data); }, [query.data]);

  if (query.error) return <Paper><ErrorState error={query.error} onRetry={() => void query.refetch()} /></Paper>;
  if (!values) return <Skeleton height={220} />;

  const number = (key: keyof Omit<FuelSettings, 'requireDriver'>, label: string, description: string, suffix: string, max: number) => (
    <NumberInput label={label} description={description} suffix={suffix} min={0} max={max} allowDecimal={false} allowNegative={false}
      value={values[key]} error={errors[key]} onChange={(v) => setValues({ ...values, [key]: Number(v) || 0 })} />
  );

  return (
    <Section title="Limites dos alertas" description="Um alerta não bloqueia o registro: ele marca o abastecimento para revisão. Mudar um limite vale para os próximos registros.">
      <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="md">
        {number('tankTolerancePercent', 'Tolerância do tanque', 'Quanto acima da capacidade cadastrada ainda é aceito sem alerta.', ' %', 200)}
        {number('priceDeviationPercent', 'Variação de preço', 'Diferença em relação ao preço de referência ou à média dos últimos 30 dias.', ' %', 200)}
        {number('consumptionDeviationPercent', 'Variação de consumo', 'Diferença em relação ao consumo esperado do veículo, para mais ou para menos.', ' %', 200)}
        {number('minHoursBetweenFuelings', 'Intervalo mínimo entre abastecimentos', 'Dois abastecimentos do mesmo veículo mais próximos que isso geram alerta. 0 desliga.', ' h', 48)}
      </SimpleGrid>
      <Switch label="Exigir o motorista em todo abastecimento" checked={values.requireDriver}
        onChange={(e) => setValues({ ...values, requireDriver: e.currentTarget.checked })} />
      <Group justify="flex-end">
        <Button loading={save.isPending} onClick={() => save.mutate(values, {
          onSuccess: () => { setErrors({}); notifySuccess('Limites salvos com sucesso.'); },
          onError: (e) => { setErrors(toApiError(e).fieldErrors); notifyError(e, 'Os limites não foram salvos'); },
        })}>Salvar limites</Button>
      </Group>
      <Text size="xs" c="dimmed">Valores atuais da empresa. Sem configuração própria, o sistema usa 5%, 20%, 20% e 2 h.</Text>
    </Section>
  );
}
