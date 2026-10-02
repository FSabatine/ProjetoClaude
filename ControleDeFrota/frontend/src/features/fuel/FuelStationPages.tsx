import { useEffect, useState } from 'react';
import { ActionIcon, Badge, Button, Group, Modal, NumberInput, Paper, Select, SimpleGrid, Stack, Switch, Table, Text, Textarea, TextInput } from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { IconBuildingStore, IconPencil, IconPlus, IconTrash } from '@tabler/icons-react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { AddressFields, addressFromApi } from '../../components/AddressFields';
import { ListToolbar, RowActions, StatusBadge } from '../../components/common';
import { DataTable, type Column } from '../../components/DataTable';
import { HeaderFact, Section } from '../../components/DetailLayout';
import { EntityFormPage } from '../../components/EntityFormPage';
import { confirmDelete, FormActions, MASKS, MaskedInput } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { EmptyState } from '../../components/States';
import { submitWithFeedback } from '../../components/useApiForm';
import { useListParams } from '../../hooks/useListParams';
import { formatCnpj, formatCurrency, formatDate, formatDateTime, formatPhone, formatPlate, formatZipCode, toApiDate } from '../../lib/format';
import { formatQuantity, formatUnitPrice } from '../../lib/fuel';
import { optional, required, rules } from '../../lib/validators';
import { fuelStationsApi, fuelTypesApi, fuelingsApi } from './api';
import { FUELING_STATUS, type FuelStation } from './fuel';

const ACTIVE = {
  true: { label: 'Ativos' },
  false: { label: 'Inativos' },
};
const FILTERS = ['isActive'] as const;

export function FuelStationListPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const list = useListParams(FILTERS, { by: 'name', direction: 'Asc' });
  const query = fuelStationsApi.useList(list.apiParams);
  const remove = fuelStationsApi.useRemove();
  const [editing, setEditing] = useState<FuelStation | null | undefined>(undefined);
  const canManage = can(PERMISSIONS.fuel.managestations);

  const newButton = canManage && <Button leftSection={<IconPlus size={18} />} onClick={() => setEditing(null)}>Novo posto</Button>;

  const columns: Column<FuelStation>[] = [
    { key: 'name', header: 'Posto', sortKey: 'name', render: (s) => (
      <Group gap="xs"><Text size="sm" fw={600}>{s.name}</Text>{s.isInternal && <Badge color="blue">Tanque próprio</Badge>}</Group>
    ) },
    { key: 'city', header: 'Cidade', secondary: true, render: (s) => [s.address.city, s.address.state].filter(Boolean).join(' / ') || '—' },
    { key: 'fuelings', header: 'Abastecimentos', align: 'right', render: (s) => s.fuelingCount.toLocaleString('pt-BR') },
    { key: 'last', header: 'Último', secondary: true, render: (s) => formatDate(s.lastFueledOn) },
    { key: 'active', header: 'Situação', render: (s) => <Badge color={s.isActive ? 'teal' : 'gray'}>{s.isActive ? 'Ativo' : 'Inativo'}</Badge> },
  ];

  return (
    <>
      <PageHeader title="Postos de combustível" description="Onde a frota abastece, incluindo o tanque próprio da empresa." action={newButton} />
      <ListToolbar search={list.search} onSearch={list.setSearch} searchPlaceholder="Buscar por nome, CNPJ ou cidade"
        filters={[{ key: 'isActive', placeholder: 'Situação', data: Object.entries(ACTIVE).map(([value, { label }]) => ({ value, label })), value: list.filters.isActive, onChange: (v) => list.setFilter('isActive', v) }]} />
      <DataTable
        columns={columns} data={query.data} isLoading={query.isFetching} error={query.error} onRetry={() => void query.refetch()}
        getRowId={(s) => s.id} sort={list.sort} onSortChange={list.setSort} onPageChange={list.setPage}
        onRowClick={(s) => navigate(`/postos/${s.id}`)}
        rowActions={(s) => canManage && (
          <RowActions onEdit={() => setEditing(s)} onDelete={() => confirmDelete({
            entity: 'posto', name: s.name,
            onConfirm: () => remove.mutate(s.id, { onSuccess: () => notifySuccess('Posto excluído.'), onError: (e) => notifyError(e, 'O posto não foi excluído') }),
          })} />
        )}
        renderCard={(s) => (
          <Stack gap={4}>
            <Group gap="xs"><Text fw={600} size="sm">{s.name}</Text>{s.isInternal && <Badge color="blue">Tanque próprio</Badge>}</Group>
            <Text size="xs" c="dimmed">{s.fuelingCount} abastecimento(s){s.lastFueledOn ? ` · último em ${formatDate(s.lastFueledOn)}` : ''}</Text>
          </Stack>
        )}
        emptyState={<EmptyState icon={<IconBuildingStore size={28} />} title="Nenhum posto cadastrado"
          description="Cadastre os postos onde a frota abastece para comparar preços e volumes." action={newButton} />}
        hasFilters={list.hasFilters} onClearFilters={list.clearFilters}
      />
      <FuelStationFormModal opened={editing !== undefined} onClose={() => setEditing(undefined)} station={editing ?? undefined} />
    </>
  );
}

export function FuelStationFormModal({ opened, onClose, station }: { opened: boolean; onClose: () => void; station?: FuelStation }) {
  const save = fuelStationsApi.useSave(station?.id);
  const initialValues = {
    name: station?.name ?? '',
    cnpj: station?.cnpj ? formatCnpj(station.cnpj) : '',
    phone: station?.phone ? formatPhone(station.phone) : '',
    contactName: station?.contactName ?? '',
    address: { ...addressFromApi(station?.address), zipCode: station?.address.zipCode ? formatZipCode(station.address.zipCode) : '' },
    isInternal: station?.isInternal ?? false,
    isActive: station?.isActive ?? true,
    notes: station?.notes ?? '',
  };
  const form = useForm({
    initialValues,
    validateInputOnBlur: true,
    validate: { name: required('Nome'), cnpj: optional(rules.cnpj), phone: optional(rules.phone), address: { zipCode: optional(rules.zipCode) } },
  });
  useEffect(() => {
    if (opened) form.setValues(initialValues);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the modal (re)opens
  }, [opened, station]);

  const handleSubmit = submitWithFeedback({
    form, mutation: save,
    successMessage: station ? 'Posto atualizado com sucesso.' : 'Posto cadastrado com sucesso.',
    errorTitle: 'O posto não foi salvo',
    onSuccess: () => onClose(),
    toBody: (v) => ({
      ...v,
      cnpj: v.cnpj || null, phone: v.phone || null, contactName: v.contactName || null, notes: v.notes || null,
      address: Object.fromEntries(Object.entries(v.address).map(([k, value]) => [k, value || null])) as typeof v.address,
    }),
  });

  return (
    <Modal opened={opened} onClose={onClose} title={station ? 'Editar posto' : 'Novo posto'} centered size="lg">
      <form onSubmit={handleSubmit} noValidate>
        <Stack>
          <TextInput label="Nome" withAsterisk maxLength={150} data-autofocus {...form.getInputProps('name')} />
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <MaskedInput label="CNPJ" mask={MASKS.cnpj} uppercase {...form.getInputProps('cnpj')} />
            <MaskedInput label="Telefone" mask={MASKS.phone} inputMode="tel" {...form.getInputProps('phone')} />
          </SimpleGrid>
          <TextInput label="Contato" maxLength={100} {...form.getInputProps('contactName')} />
          <AddressFields form={form} required={false} />
          <Switch label="Tanque próprio da empresa" description="Bomba interna. O controle de estoque do tanque ainda não faz parte do sistema."
            {...form.getInputProps('isInternal', { type: 'checkbox' })} />
          <Switch label="Ativo" description="Postos inativos não aparecem para novos abastecimentos." {...form.getInputProps('isActive', { type: 'checkbox' })} />
          <Textarea label="Observações" autosize minRows={2} maxLength={2000} {...form.getInputProps('notes')} />
          <FormActions saving={save.isPending} onCancel={onClose} submitLabel={station ? 'Salvar alterações' : 'Cadastrar posto'} />
        </Stack>
      </form>
    </Modal>
  );
}

export function FuelStationDetailPage() {
  const { id } = useParams();
  const detail = fuelStationsApi.useDetail(id);
  return <EntityFormPage id={id} detail={detail}>{(s) => s && <StationView station={s} />}</EntityFormPage>;
}

function StationView({ station: s }: { station: FuelStation }) {
  const navigate = useNavigate();
  const { can } = useAuth();
  const [editing, setEditing] = useState(false);
  const canManage = can(PERMISSIONS.fuel.managestations);
  const canSeePrices = canManage || can(PERMISSIONS.fuel.viewcosts);
  const [page, setPage] = useState(1);
  const fuelings = fuelingsApi.useList({ fuelStationId: s.id, page, pageSize: 10, sortBy: 'fueledAt', sortDirection: 'Desc' });
  const address = [s.address.street && `${s.address.street}, ${s.address.number ?? 's/n'}`, s.address.neighborhood, s.address.city && `${s.address.city}/${s.address.state ?? ''}`]
    .filter(Boolean).join(' · ');

  return (
    <>
      <PageHeader title={s.name} description={address || (s.isInternal ? 'Tanque próprio da empresa' : 'Endereço não informado')}
        breadcrumbs={[{ label: 'Postos de combustível', to: '/postos' }, { label: s.name }]}
        action={canManage && <Button variant="default" leftSection={<IconPencil size={18} />} onClick={() => setEditing(true)}>Editar</Button>} />
      <Stack gap="md">
        <Paper p="md">
          <SimpleGrid cols={{ base: 2, md: 4 }}>
            <HeaderFact label="Situação" value={<Badge color={s.isActive ? 'teal' : 'gray'}>{s.isActive ? 'Ativo' : 'Inativo'}</Badge>} />
            <HeaderFact label="Tipo" value={s.isInternal ? 'Tanque próprio' : 'Posto externo'} />
            <HeaderFact label="CNPJ" value={s.cnpj ? formatCnpj(s.cnpj) : '—'} />
            <HeaderFact label="Abastecimentos" value={s.fuelingCount.toLocaleString('pt-BR')} hint={s.lastFueledOn && `último em ${formatDate(s.lastFueledOn)}`} />
          </SimpleGrid>
        </Paper>
        {canSeePrices && <PricesSection stationId={s.id} canManage={canManage} />}
        <Section title="Abastecimentos neste posto">
          {(fuelings.data?.items.length ?? 0) === 0 ? <Text size="sm" c="dimmed">Nenhum abastecimento registrado neste posto.</Text> : (
            <Table.ScrollContainer minWidth={520}>
              <Table highlightOnHover fz="sm">
                <Table.Thead><Table.Tr><Table.Th>Data</Table.Th><Table.Th>Veículo</Table.Th><Table.Th>Combustível</Table.Th><Table.Th ta="right">Quantidade</Table.Th><Table.Th ta="right">Total</Table.Th><Table.Th>Situação</Table.Th></Table.Tr></Table.Thead>
                <Table.Tbody>
                  {fuelings.data!.items.map((f) => (
                    <Table.Tr key={f.id} style={{ cursor: 'pointer' }} onClick={() => navigate(`/abastecimentos/${f.id}`)}>
                      <Table.Td>{formatDateTime(f.fueledAt)}</Table.Td>
                      <Table.Td ff="monospace">{formatPlate(f.licensePlate)}</Table.Td>
                      <Table.Td>{f.fuelTypeName}</Table.Td>
                      <Table.Td ta="right">{formatQuantity(f.quantity, f.unit)}</Table.Td>
                      <Table.Td ta="right">{formatCurrency(f.totalAmount)}</Table.Td>
                      <Table.Td><StatusBadge value={f.status} map={FUELING_STATUS} /></Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          )}
          {(fuelings.data?.totalPages ?? 0) > 1 && (
            <Group justify="flex-end" gap="xs">
              <Button variant="default" size="xs" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</Button>
              <Text size="xs" c="dimmed">{page} de {fuelings.data!.totalPages}</Text>
              <Button variant="default" size="xs" disabled={page >= fuelings.data!.totalPages} onClick={() => setPage((p) => p + 1)}>Próxima</Button>
            </Group>
          )}
        </Section>
      </Stack>
      <FuelStationFormModal opened={editing} onClose={() => setEditing(false)} station={s} />
    </>
  );
}

/** Reference prices over time (seção 22). Fuelings always keep what was actually paid. */
function PricesSection({ stationId, canManage }: { stationId: string; canManage: boolean }) {
  const prices = fuelStationsApi.usePrices(stationId);
  const types = fuelTypesApi.useList();
  const add = fuelStationsApi.useAddPrice(stationId);
  const remove = fuelStationsApi.useRemovePrice(stationId);
  const [fuelTypeId, setFuelTypeId] = useState<string | null>(null);
  const [price, setPrice] = useState<number | string>('');
  const [effectiveFrom, setEffectiveFrom] = useState<Date | null>(new Date());

  const submit = () => {
    if (!fuelTypeId || price === '' || !effectiveFrom) return;
    add.mutate({ fuelTypeId, price: Number(price), effectiveFrom: toApiDate(effectiveFrom)!, notes: null }, {
      onSuccess: () => { notifySuccess('Preço registrado.'); setPrice(''); },
      onError: (e) => notifyError(e, 'O preço não foi registrado'),
    });
  };

  return (
    <Section title="Preços de referência" description="Preço praticado ou negociado com o posto. Usado como sugestão e para alertar preços fora do normal.">
      {canManage && (
        <Group align="flex-end" gap="sm" wrap="wrap">
          <Select label="Combustível" data={(types.data ?? []).map((t) => ({ value: t.id, label: t.name }))} value={fuelTypeId} onChange={setFuelTypeId} style={{ flex: '1 1 180px' }} />
          <NumberInput label="Preço" prefix="R$ " decimalSeparator="," decimalScale={4} allowNegative={false} value={price} onChange={setPrice} style={{ flex: '0 1 140px' }} />
          <DateInput label="A partir de" valueFormat="DD/MM/YYYY" value={effectiveFrom} onChange={setEffectiveFrom} style={{ flex: '0 1 150px' }} />
          <Button onClick={submit} loading={add.isPending} disabled={!fuelTypeId || price === '' || !effectiveFrom}>Registrar preço</Button>
        </Group>
      )}
      {(prices.data?.length ?? 0) === 0 ? <Text size="sm" c="dimmed">Nenhum preço de referência registrado.</Text> : (
        <Table.ScrollContainer minWidth={420}>
          <Table fz="sm">
            <Table.Thead><Table.Tr><Table.Th>Combustível</Table.Th><Table.Th ta="right">Preço</Table.Th><Table.Th>Vigência</Table.Th><Table.Th>Registrado por</Table.Th>{canManage && <Table.Th />}</Table.Tr></Table.Thead>
            <Table.Tbody>
              {prices.data!.map((p) => (
                <Table.Tr key={p.id}>
                  <Table.Td>{p.fuelTypeName}</Table.Td>
                  <Table.Td ta="right">{formatUnitPrice(p.price, p.unit)}</Table.Td>
                  <Table.Td>a partir de {formatDate(p.effectiveFrom)}</Table.Td>
                  <Table.Td c="dimmed">{p.recordedByName ?? '—'}</Table.Td>
                  {canManage && (
                    <Table.Td w={48}>
                      <ActionIcon variant="subtle" color="red" aria-label={`Excluir preço de ${p.fuelTypeName}`} onClick={() => confirmDelete({
                        entity: 'preço', name: `${p.fuelTypeName} ${formatUnitPrice(p.price, p.unit)}`,
                        onConfirm: () => remove.mutate(p.id, { onSuccess: () => notifySuccess('Preço excluído.'), onError: (e) => notifyError(e, 'O preço não foi excluído') }),
                      })}><IconTrash size={16} /></ActionIcon>
                    </Table.Td>
                  )}
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
    </Section>
  );
}
