import type { ReactNode } from 'react';
import { Anchor, Breadcrumbs, Group, Stack, Text, Title } from '@mantine/core';
import { Link } from 'react-router-dom';

interface PageHeaderProps {
  title: string;
  description?: string;
  /** The single primary action of the page (UX_UI.md). */
  action?: ReactNode;
  breadcrumbs?: { label: string; to?: string }[];
}

export function PageHeader({ title, description, action, breadcrumbs }: PageHeaderProps) {
  return (
    <Stack gap={6} mb="lg">
      {breadcrumbs && (
        <Breadcrumbs separator="›" fz="sm">
          {breadcrumbs.map((b) =>
            b.to ? (
              <Anchor key={b.label} component={Link} to={b.to} c="dimmed" fz="sm">
                {b.label}
              </Anchor>
            ) : (
              <Text key={b.label} fz="sm" c="dimmed">
                {b.label}
              </Text>
            ),
          )}
        </Breadcrumbs>
      )}
      <Group justify="space-between" align="flex-start" gap="md" wrap="wrap">
        <div style={{ minWidth: 0, flex: '1 1 260px' }}>
          <Title order={2} fz={{ base: 22, sm: 26 }}>
            {title}
          </Title>
          {description && (
            <Text c="dimmed" size="sm" mt={4}>
              {description}
            </Text>
          )}
        </div>
        {action}
      </Group>
    </Stack>
  );
}
