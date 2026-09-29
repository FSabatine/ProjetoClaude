import { Accordion, Badge, Group, Paper, Skeleton, Stack, Table, Text, ThemeIcon } from '@mantine/core';
import { IconCheck, IconMinus } from '@tabler/icons-react';
import { PageHeader } from '../../components/PageHeader';
import { ErrorState } from '../../components/States';
import { usePermissionCatalog, useRoles } from '../users/users';

const MODULE_LABEL: Record<string, string> = {
  dashboard: 'Painel',
  companies: 'Empresas',
  users: 'Usuários',
  roles: 'Papéis',
  drivers: 'Motoristas',
  vehicles: 'Veículos',
  implements: 'Implementos',
  audit: 'Auditoria',
};

/** Read-only in Phase 1: system roles are seeded (DOMAIN.md). */
export function RolesPage() {
  const roles = useRoles();
  const catalog = usePermissionCatalog();
  const error = roles.error ?? catalog.error;

  const modules = [...new Set(catalog.data?.map((p) => p.module))];

  return (
    <>
      <PageHeader
        title="Papéis e permissões"
        description="Cada papel é um conjunto de permissões. O acesso de um usuário é a soma das permissões dos seus papéis."
      />
      {error ? (
        <Paper><ErrorState error={error} onRetry={() => { void roles.refetch(); void catalog.refetch(); }} /></Paper>
      ) : roles.isLoading || catalog.isLoading ? (
        <Stack>{[1, 2, 3, 4].map((i) => <Skeleton key={i} height={60} />)}</Stack>
      ) : (
        <Accordion variant="separated" radius="md" multiple>
          {roles.data?.map((role) => (
            <Accordion.Item key={role.id} value={String(role.id)}>
              <Accordion.Control>
                <Group justify="space-between" wrap="nowrap" pr="sm">
                  <div>
                    <Text fw={600}>{role.name}</Text>
                    <Text size="sm" c="dimmed">{role.description}</Text>
                  </div>
                  <Badge color="gray" style={{ flexShrink: 0 }}>{role.permissions.length} permissões</Badge>
                </Group>
              </Accordion.Control>
              <Accordion.Panel>
                <Table.ScrollContainer minWidth={420}>
                  <Table verticalSpacing={6}>
                    <Table.Tbody>
                      {modules.map((module) => (
                        <Table.Tr key={module}>
                          <Table.Td w={160}><Text size="sm" fw={500}>{MODULE_LABEL[module] ?? module}</Text></Table.Td>
                          <Table.Td>
                            <Group gap="xs">
                              {catalog.data!.filter((p) => p.module === module).map((p) => {
                                const granted = role.permissions.includes(p.key);
                                return (
                                  <Badge
                                    key={p.key}
                                    color={granted ? 'teal' : 'gray'}
                                    variant={granted ? 'light' : 'outline'}
                                    leftSection={
                                      <ThemeIcon size={14} variant="transparent" color={granted ? 'teal' : 'gray'}>
                                        {granted ? <IconCheck size={12} /> : <IconMinus size={12} />}
                                      </ThemeIcon>
                                    }
                                    style={{ textTransform: 'none', fontWeight: 500 }}
                                    aria-label={`${p.description}: ${granted ? 'concedida' : 'não concedida'}`}
                                  >
                                    {p.description}
                                  </Badge>
                                );
                              })}
                            </Group>
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Table.ScrollContainer>
              </Accordion.Panel>
            </Accordion.Item>
          ))}
        </Accordion>
      )}
    </>
  );
}
