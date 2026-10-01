import { useEffect } from 'react';
import { Alert, Button, Group, List, Paper, Stack, Text, Title } from '@mantine/core';
import { IconBulb, IconChevronRight } from '@tabler/icons-react';
import { trackHelpEvent } from './analytics';
import { getArticleById } from './content';
import type { HelpArticle } from './types';

/** Artigo estruturado (seção 25 do pedido): nem toda seção aparece em todo artigo. */
export function HelpArticleView({ article, onOpenArticle }: { article: HelpArticle; onOpenArticle: (id: string) => void }) {
  useEffect(() => {
    trackHelpEvent({ type: 'HelpArticleViewed', articleId: article.id });
  }, [article.id]);

  const related = (article.relatedArticleIds ?? []).map(getArticleById).filter((a): a is HelpArticle => !!a);

  return (
    <Stack gap="md">
      <div>
        <Title order={3} fz="lg">{article.title}</Title>
        <Text size="sm" c="dimmed" mt={4}>{article.summary}</Text>
      </div>

      {article.whyItMatters && (
        <Alert color="blue" variant="light" icon={<IconBulb size={18} />} title="Por que isso importa">
          <Text size="sm">{article.whyItMatters}</Text>
        </Alert>
      )}

      {article.steps && article.steps.length > 0 && (
        <div>
          <Text fw={600} size="sm" mb={6}>Passo a passo</Text>
          <List type="ordered" size="sm" spacing={4}>
            {article.steps.map((step, i) => <List.Item key={i}>{step}</List.Item>)}
          </List>
        </div>
      )}

      {article.example && (
        <Paper p="sm" bg="var(--mantine-color-default-hover)">
          <Text size="xs" fw={600} c="dimmed" mb={4} tt="uppercase">Exemplo</Text>
          <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>{article.example}</Text>
        </Paper>
      )}

      {article.notes && article.notes.length > 0 && (
        <div>
          <Text fw={600} size="sm" mb={6}>Importante</Text>
          <List size="sm" spacing={4}>
            {article.notes.map((note, i) => <List.Item key={i}>{note}</List.Item>)}
          </List>
        </div>
      )}

      {related.length > 0 && (
        <div>
          <Text fw={600} size="sm" mb={6}>Relacionados</Text>
          <Stack gap={4}>
            {related.map((r) => (
              <Button key={r.id} variant="subtle" justify="space-between" rightSection={<IconChevronRight size={14} />}
                onClick={() => onOpenArticle(r.id)}>
                {r.title}
              </Button>
            ))}
          </Stack>
        </div>
      )}
    </Stack>
  );
}

/** Badge curto usado nas listas de artigos (categoria/busca). */
export function HelpArticleListItem({ article, onClick }: { article: HelpArticle; onClick: () => void }) {
  return (
    <Button variant="subtle" onClick={onClick} fullWidth justify="space-between" rightSection={<IconChevronRight size={14} />}
      styles={{ label: { flex: 1, textAlign: 'left' }, root: { height: 'auto', padding: 'var(--mantine-spacing-sm)' } }}>
      <Group justify="space-between" wrap="nowrap" style={{ flex: 1 }}>
        <div style={{ textAlign: 'left' }}>
          <Text size="sm" fw={600}>{article.title}</Text>
          <Text size="xs" c="dimmed" lineClamp={2}>{article.summary}</Text>
        </div>
      </Group>
    </Button>
  );
}
