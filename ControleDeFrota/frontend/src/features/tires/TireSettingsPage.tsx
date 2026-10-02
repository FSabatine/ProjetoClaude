import { useEffect, useState } from 'react';
import {
  ActionIcon, Alert, Badge, Button, Checkbox, Group, Modal, NumberInput, Paper, Select, SimpleGrid, Skeleton, Stack, Switch, Table, Tabs, Text, TextInput,
} from '@mantine/core';
import { IconInfoCircle, IconPencil, IconPlus, IconTrash } from '@tabler/icons-react';
import { useSearchParams } from 'react-router-dom';
import { toSelectData } from '../../components/common';
import { Section } from '../../components/DetailLayout';
import { confirmDelete } from '../../components/forms';
import { notifyError, notifySuccess } from '../../components/notify';
import { PageHeader } from '../../components/PageHeader';
import { ErrorState } from '../../components/States';
import { previewPositions, type AxleDraft } from '../../lib/tires';
import { tireLayoutsApi, tireModelsApi, tireSettingsApi } from './api';
import { TireLayoutDiagram } from './TireLayoutDiagram';
import { TireModelFormModal } from './TireFormPage';
import { AXLE_TYPE, LAYOUT_TARGET, PRESSURE_UNIT, TIRE_APPLICATION, type TireLayout, type TireModel, type TireSettings } from './tires';

/** /configuracoes/pneus — company policy, axle configurations and the tire model catalog (tires.managesettings). */
export function TireSettingsPage() {
  const [params, setParams] = useSearchParams();
  const tab = ['limites', 'eixos', 'modelos'].includes(params.get('aba') ?? '') ? params.get('aba')! : 'limites';
  return (
    <>
      <PageHeader title="Configurações de pneus" description="Limites da política da empresa, configurações de eixos e catálogo de modelos." />
      <Tabs value={tab} onChange={(v) => setParams((p) => { const n = new URLSearchParams(p); n.set('aba', v ?? 'limites'); return n; }, { replace: true })}>
        <Tabs.List mb="md">
          <Tabs.Tab value="limites">Limites e alertas</Tabs.Tab>
          <Tabs.Tab value="eixos">Configurações de eixos</Tabs.Tab>
          <Tabs.Tab value="modelos">Modelos de pneu</Tabs.Tab>
        </Tabs.List>
        <Tabs.Panel value="limites"><Limits /></Tabs.Panel>
        <Tabs.Panel value="eixos"><Layouts /></Tabs.Panel>
        <Tabs.Panel value="modelos"><Models /></Tabs.Panel>
      </Tabs>
    </>
  );
}

function Limits() {
  const query = tireSettingsApi.useGet();
  const save = tireSettingsApi.useSave();
  const [v, setV] = useState<TireSettings | null>(null);
  useEffect(() => { if (query.data) setV(query.data); }, [query.data]);
  if (query.error) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  if (!v) return <Skeleton height={320} />;
  const n = (x: number | string) => (x === '' ? 0 : Number(x));

  return (
    <form onSubmit={(e) => {
      e.preventDefault();
      save.mutate(v, { onSuccess: () => notifySuccess('Limites salvos.'), onError: (err) => notifyError(err, 'Os limites não foram salvos') });
    }}>
      <Stack gap="md">
        <Alert color="blue" icon={<IconInfoCircle size={18} />} p="xs">
          Estes valores são a política da sua empresa para alertas e acompanhamento. Eles não substituem as exigências legais nem as recomendações do fabricante.
        </Alert>
        <Section title="Sulco">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <NumberInput label="Sulco mínimo (substituição)" suffix=" mm" decimalScale={1} decimalSeparator="," allowNegative={false} value={v.minTreadDepthMm}
              onChange={(x) => setV({ ...v, minTreadDepthMm: n(x) })} description="Abaixo ou igual: alerta crítico “no mínimo da empresa”." />
            <NumberInput label="Aviso de sulco" suffix=" mm" decimalScale={1} decimalSeparator="," allowNegative={false} value={v.treadWarningDepthMm}
              onChange={(x) => setV({ ...v, treadWarningDepthMm: n(x) })} description="Abaixo ou igual: “perto da substituição”." />
            <NumberInput label="Perda de sulco para revisão" suffix=" mm a cada 1.000 km" decimalScale={2} decimalSeparator="," allowNegative={false}
              value={v.rapidWearMmPer1000Km} onChange={(x) => setV({ ...v, rapidWearMmPer1000Km: n(x) })} description="0 desliga. Comparado entre duas medições." />
          </SimpleGrid>
        </Section>
        <Section title="Inspeção, idade e vida">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <NumberInput label="Intervalo de inspeção" suffix=" dias" allowDecimal={false} allowNegative={false} value={v.inspectionIntervalDays}
              onChange={(x) => setV({ ...v, inspectionIntervalDays: n(x) })} description="Pneus instalados. 0 desliga." />
            <NumberInput label="Idade para revisão" suffix=" anos" allowDecimal={false} allowNegative={false} value={v.maxAgeYears}
              onChange={(x) => setV({ ...v, maxAgeYears: n(x) })} description="Contada pela data de fabricação (DOT). 0 desliga." />
            <NumberInput label="Vida mínima esperada" suffix=" km" thousandSeparator="." decimalSeparator="," allowDecimal={false} allowNegative={false}
              value={v.minExpectedLifeKm} onChange={(x) => setV({ ...v, minExpectedLifeKm: n(x) })} description="Pneu baixado antes disso fica para revisão. 0 desliga." />
          </SimpleGrid>
        </Section>
        <Section title="Pressão">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <Select label="Unidade padrão" data={toSelectData(PRESSURE_UNIT)} value={v.pressureUnit} allowDeselect={false}
              onChange={(x) => setV({ ...v, pressureUnit: (x ?? 'Psi') as TireSettings['pressureUnit'] })} />
            <NumberInput label="Tolerância em relação à referência" suffix=" %" allowDecimal={false} allowNegative={false} value={v.pressureTolerancePercent}
              onChange={(x) => setV({ ...v, pressureTolerancePercent: n(x) })} description="A referência é definida por eixo, na configuração de eixos." />
          </SimpleGrid>
        </Section>
        <Section title="Manutenção">
          <Switch checked={v.autoMaintenanceRequestOnUnfit} onChange={(e) => setV({ ...v, autoMaintenanceRequestOnUnfit: e.currentTarget.checked })}
            label="Abrir solicitação de manutenção quando um pneu de veículo for inspecionado como “impróprio para uso”"
            description="Desligado: ninguém é acionado automaticamente; abra a solicitação pelo botão, se for o caso." />
        </Section>
        <Group justify="flex-end"><Button type="submit" loading={save.isPending}>Salvar limites</Button></Group>
      </Stack>
    </form>
  );
}

function Layouts() {
  const query = tireLayoutsApi.useList(undefined, true);
  const remove = tireLayoutsApi.useRemove();
  const [editing, setEditing] = useState<TireLayout | 'new' | null>(null);
  if (query.error) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  return (
    <Stack gap="md">
      <Group justify="space-between">
        <Text size="sm" c="dimmed">Cada veículo e implemento aponta para uma configuração. As posições do diagrama são geradas a partir dos eixos.</Text>
        <Button leftSection={<IconPlus size={18} />} onClick={() => setEditing('new')}>Nova configuração</Button>
      </Group>
      {!query.data ? <Skeleton height={240} /> : (
        <SimpleGrid cols={{ base: 1, md: 2, xl: 3 }}>
          {query.data.map((l) => (
            <Paper key={l.id} p="md">
              <Group justify="space-between" wrap="nowrap" align="flex-start">
                <div style={{ minWidth: 0 }}>
                  <Group gap="xs"><Text fw={650}>{l.name}</Text>{!l.isActive && <Badge color="gray">Inativa</Badge>}</Group>
                  <Text size="sm" c="dimmed">{LAYOUT_TARGET[l.target].label} · {l.axles.length} eixo(s) · {l.tirePositionCount} pneus{l.spareCount ? ` + ${l.spareCount} estepe` : ''}</Text>
                  <Text size="xs" c="dimmed">Em uso por {l.assetCount} cadastro(s)</Text>
                </div>
                <Group gap={4} wrap="nowrap">
                  <ActionIcon variant="subtle" aria-label={`Editar ${l.name}`} onClick={() => setEditing(l)}><IconPencil size={18} /></ActionIcon>
                  {l.assetCount === 0 && (
                    <ActionIcon variant="subtle" color="red" aria-label={`Excluir ${l.name}`} onClick={() => confirmDelete({
                      entity: 'configuração', name: l.name,
                      onConfirm: () => remove.mutate(l.id, { onSuccess: () => notifySuccess('Configuração excluída.'), onError: (e) => notifyError(e, 'A configuração não foi excluída') }),
                    })}><IconTrash size={18} /></ActionIcon>
                  )}
                </Group>
              </Group>
            </Paper>
          ))}
        </SimpleGrid>
      )}
      {editing && <LayoutEditor layout={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}
    </Stack>
  );
}

const newAxle = (type = 'Drive', isDual = true): AxleDraft => ({ type, isDual, isRequired: true, allowedSize: null, recommendedPressurePsi: null });

function LayoutEditor({ layout, onClose }: { layout?: TireLayout; onClose: () => void }) {
  const save = tireLayoutsApi.useSave(layout?.id);
  const [name, setName] = useState(layout?.name ?? '');
  const [target, setTarget] = useState<string>(layout?.target ?? 'Vehicle');
  const [description, setDescription] = useState(layout?.description ?? '');
  const [spares, setSpares] = useState<number>(layout?.spareCount ?? 1);
  const [active, setActive] = useState(layout?.isActive ?? true);
  const [axles, setAxles] = useState<AxleDraft[]>(layout?.axles.map((a) => ({ ...a })) ?? [newAxle('Steer', false), newAxle()]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const preview = previewPositions(axles, spares).map((position) => ({ position: { ...position, axleType: position.axleType as TireLayout['axles'][number]['type'] | null }, tire: null }));
  const setAxle = (i: number, change: Partial<AxleDraft>) => setAxles((list) => list.map((a, j) => (j === i ? { ...a, ...change } : a)));

  const submit = () => save.mutate({
    name, target, description: description || null, spareCount: spares, isActive: active,
    axles: axles.map((a) => ({ ...a, type: a.type as TireLayout['axles'][number]['type'], allowedSize: a.allowedSize || null })),
  }, {
    onSuccess: () => { notifySuccess('Configuração salva.'); onClose(); },
    onError: (e) => setErrors(notifyError(e, 'A configuração não foi salva').fieldErrors),
  });

  return (
    <Modal opened onClose={onClose} title={layout ? `Editar · ${layout.name}` : 'Nova configuração de eixos'} size="xl" centered>
      <form onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
          <Stack gap="sm">
            <TextInput label="Nome" withAsterisk maxLength={80} value={name} onChange={(e) => setName(e.currentTarget.value)} error={errors.name} data-autofocus />
            <SimpleGrid cols={2}>
              <Select label="Para" data={toSelectData(LAYOUT_TARGET)} value={target} allowDeselect={false} onChange={(v) => setTarget(v ?? 'Vehicle')}
                disabled={!!layout && layout.assetCount > 0} />
              <NumberInput label="Estepes" min={0} max={2} allowDecimal={false} value={spares} onChange={(v) => setSpares(Number(v) || 0)} />
            </SimpleGrid>
            <TextInput label="Descrição" maxLength={300} value={description} onChange={(e) => setDescription(e.currentTarget.value)} />
            <Text size="sm" fw={600}>Eixos (da frente para trás)</Text>
            {axles.map((a, i) => (
              <Paper key={i} p="xs" withBorder>
                <Group gap="xs" align="flex-end" wrap="wrap">
                  <Text size="sm" fw={600} w={54}>Eixo {i + 1}</Text>
                  <Select aria-label={`Tipo do eixo ${i + 1}`} data={toSelectData(AXLE_TYPE)} value={a.type} allowDeselect={false} w={130} onChange={(v) => setAxle(i, { type: v ?? 'Drive' })} />
                  <Checkbox label="Rodado duplo" checked={a.isDual} onChange={(e) => setAxle(i, { isDual: e.currentTarget.checked })} />
                  <Checkbox label="Obrigatório" checked={a.isRequired} onChange={(e) => setAxle(i, { isRequired: e.currentTarget.checked })} />
                  <ActionIcon variant="subtle" color="red" aria-label={`Remover eixo ${i + 1}`} disabled={axles.length === 1}
                    onClick={() => setAxles((list) => list.filter((_, j) => j !== i))}><IconTrash size={16} /></ActionIcon>
                </Group>
                <Group gap="xs" mt={6} grow>
                  <TextInput size="xs" label="Medida exigida" placeholder="Qualquer" value={a.allowedSize ?? ''} onChange={(e) => setAxle(i, { allowedSize: e.currentTarget.value || null })} />
                  <NumberInput size="xs" label="Pressão de referência" suffix=" psi" allowNegative={false} value={a.recommendedPressurePsi ?? ''}
                    onChange={(v) => setAxle(i, { recommendedPressurePsi: v === '' ? null : Number(v) })} />
                </Group>
              </Paper>
            ))}
            {errors.axles && <Text size="sm" c="red">{errors.axles}</Text>}
            <Button variant="default" leftSection={<IconPlus size={16} />} onClick={() => setAxles((list) => [...list, newAxle(target === 'Implement' ? 'Trailer' : 'Drive')])}
              disabled={axles.length >= 10}>Adicionar eixo</Button>
            <Switch label="Ativa" checked={active} onChange={(e) => setActive(e.currentTarget.checked)} />
          </Stack>
          <Stack gap="xs">
            <Text size="sm" fw={600}>Pré-visualização</Text>
            <TireLayoutDiagram positions={preview} minTreadMm={0} warningTreadMm={0} selectedCode={null} onSelect={() => undefined} />
            {layout && layout.assetCount > 0 && (
              <Alert color="orange" p="xs">Em uso por {layout.assetCount} cadastro(s): posições com pneu instalado não podem ser removidas.</Alert>
            )}
          </Stack>
        </SimpleGrid>
        <Group justify="flex-end" mt="md">
          <Button variant="default" onClick={onClose}>Voltar</Button>
          <Button type="submit" loading={save.isPending}>Salvar configuração</Button>
        </Group>
      </form>
    </Modal>
  );
}

function Models() {
  const query = tireModelsApi.useList(true);
  const remove = tireModelsApi.useRemove();
  const [editing, setEditing] = useState<TireModel | 'new' | null>(null);
  if (query.error) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  return (
    <Stack gap="md">
      <Group justify="space-between">
        <Text size="sm" c="dimmed">Marca, modelo, medida e especificações compartilhadas pelos pneus do mesmo modelo.</Text>
        <Button leftSection={<IconPlus size={18} />} onClick={() => setEditing('new')}>Novo modelo</Button>
      </Group>
      <Paper>
        {!query.data ? <Skeleton height={200} /> : query.data.length === 0 ? <Text p="md" size="sm" c="dimmed">Nenhum modelo cadastrado.</Text> : (
          <Table.ScrollContainer minWidth={640}>
            <Table verticalSpacing="sm" highlightOnHover>
              <Table.Thead><Table.Tr><Table.Th>Marca / modelo</Table.Th><Table.Th>Medida</Table.Th><Table.Th>Aplicação</Table.Th><Table.Th ta="right">Pneus</Table.Th><Table.Th /></Table.Tr></Table.Thead>
              <Table.Tbody>
                {query.data.map((m) => (
                  <Table.Tr key={m.id}>
                    <Table.Td><Text size="sm" fw={600}>{m.brand} {m.name}</Text>{!m.isActive && <Badge size="xs" color="gray">Inativo</Badge>}</Table.Td>
                    <Table.Td>{m.size}</Table.Td>
                    <Table.Td>{TIRE_APPLICATION[m.application].label}</Table.Td>
                    <Table.Td ta="right">{m.tireCount}</Table.Td>
                    <Table.Td>
                      <Group gap={4} justify="flex-end" wrap="nowrap">
                        <ActionIcon variant="subtle" aria-label={`Editar ${m.brand} ${m.name}`} onClick={() => setEditing(m)}><IconPencil size={16} /></ActionIcon>
                        {m.tireCount === 0 && (
                          <ActionIcon variant="subtle" color="red" aria-label={`Excluir ${m.brand} ${m.name}`} onClick={() => confirmDelete({
                            entity: 'modelo', name: `${m.brand} ${m.name} ${m.size}`,
                            onConfirm: () => remove.mutate(m.id, { onSuccess: () => notifySuccess('Modelo excluído.'), onError: (e) => notifyError(e, 'O modelo não foi excluído') }),
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
      </Paper>
      <TireModelFormModal opened={!!editing} onClose={() => setEditing(null)} model={editing === 'new' || !editing ? undefined : editing} />
    </Stack>
  );
}
