import { useEffect, useMemo, useState } from 'react';
import { ActionIcon, Divider, Drawer, Group, Paper, ScrollArea, Stack, Text, TextInput, UnstyledButton } from '@mantine/core';
import { useMediaQuery } from '@mantine/hooks';
import {
  IconAlertTriangle, IconArrowLeft, IconBuilding, IconChecklist, IconChevronRight, IconCompass, IconFileText, IconGauge,
  IconLayoutDashboard, IconMessageCircleQuestion, IconSearch, IconShieldLock, IconSparkles, IconSteeringWheel, IconTool,
  IconTruck, IconTruckLoading, IconUserCheck, IconUsers, IconX,
} from '@tabler/icons-react';
import { useLocation } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { EmptyState } from '../../components/States';
import { trackHelpEvent } from './analytics';
import { resolveContextualCategory } from './context';
import { getArticleById, getArticlesForCategory, getCategoryById, HELP_ARTICLES, HELP_CATEGORIES, WHATS_NEW } from './content';
import { HelpArticleListItem, HelpArticleView } from './HelpArticleView';
import { searchArticles } from './search';
import type { HelpCategory } from './types';

type Screen = { type: 'home' } | { type: 'category'; id: string } | { type: 'article'; id: string };

/** Um ícone por categoria (seção 4 do pedido) — mesma biblioteca (Tabler) já usada no resto do app. */
const CATEGORY_ICON: Record<string, typeof IconCompass> = {
  gettingStarted: IconCompass,
  dashboard: IconLayoutDashboard,
  companies: IconBuilding,
  users: IconUsers,
  rolesPermissions: IconShieldLock,
  drivers: IconSteeringWheel,
  vehicles: IconTruck,
  implements: IconTruckLoading,
  assignments: IconUserCheck,
  mileage: IconGauge,
  documents: IconFileText,
  checklists: IconChecklist,
  occurrences: IconAlertTriangle,
  maintenance: IconTool,
  searchAndFilters: IconSearch,
  faq: IconMessageCircleQuestion,
};

function CategoryRow({ category, onClick }: { category: HelpCategory; onClick: () => void }) {
  const Icon = CATEGORY_ICON[category.id] ?? IconCompass;
  return (
    <UnstyledButton onClick={onClick} p="sm" style={{ borderRadius: 'var(--mantine-radius-md)' }}>
      <Group justify="space-between" wrap="nowrap">
        <Group gap="sm" wrap="nowrap">
          <Icon size={20} stroke={1.6} />
          <div>
            <Text size="sm" fw={600}>{category.label}</Text>
            <Text size="xs" c="dimmed">{category.description}</Text>
          </div>
        </Group>
        <IconChevronRight size={16} />
      </Group>
    </UnstyledButton>
  );
}

/**
 * Central de Ajuda (seções 3/4/29 do pedido): um Drawer que não tira o usuário da tela em que está.
 * Navegação interna (home → categoria → artigo) fica numa pilha local — nunca muda a URL do app.
 */
export function HelpDrawer({ opened, onClose }: { opened: boolean; onClose: () => void }) {
  const { can } = useAuth();
  const location = useLocation();
  const isPhone = useMediaQuery('(max-width: 48em)');
  const [stack, setStack] = useState<Screen[]>([{ type: 'home' }]);
  const [query, setQuery] = useState('');
  const screen = stack[stack.length - 1];

  const visibleCategories = useMemo(
    () => HELP_CATEGORIES.filter((c) => !c.requiredPermission || can(c.requiredPermission)),
    [can],
  );
  const visibleArticles = useMemo(
    () => HELP_ARTICLES.filter((a) => !a.requiredPermission || can(a.requiredPermission)),
    [can],
  );

  const contextualCategoryId = resolveContextualCategory(location.pathname, location.search);
  const orderedCategories = useMemo(() => {
    const match = visibleCategories.find((c) => c.id === contextualCategoryId);
    return match ? [match, ...visibleCategories.filter((c) => c.id !== match.id)] : visibleCategories;
  }, [visibleCategories, contextualCategoryId]);

  const trimmedQuery = query.trim();
  const searchResults = useMemo(() => (trimmedQuery ? searchArticles(trimmedQuery, visibleArticles) : []), [trimmedQuery, visibleArticles]);

  useEffect(() => {
    if (!trimmedQuery) return;
    trackHelpEvent(searchResults.length > 0
      ? { type: 'HelpSearchPerformed', query: trimmedQuery, resultCount: searchResults.length }
      : { type: 'HelpSearchNoResult', query: trimmedQuery });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only react to the query changing, not every result recomputation
  }, [trimmedQuery]);

  const push = (next: Screen) => setStack((s) => [...s, next]);
  const openArticle = (id: string) => { setQuery(''); push({ type: 'article', id }); };
  const openCategory = (id: string) => { setQuery(''); push({ type: 'category', id }); };
  const back = () => setStack((s) => (s.length > 1 ? s.slice(0, -1) : s));
  const handleClose = () => {
    onClose();
    setStack([{ type: 'home' }]);
    setQuery('');
  };

  const title = trimmedQuery
    ? 'Resultados da busca'
    : screen.type === 'home' ? 'Central de Ajuda'
    : screen.type === 'category' ? (getCategoryById(screen.id)?.label ?? 'Categoria')
    : (getArticleById(screen.id)?.title ?? 'Artigo');

  const canGoBack = stack.length > 1 || !!trimmedQuery;

  return (
    <Drawer opened={opened} onClose={handleClose} position="right" size={isPhone ? '100%' : 'md'}
      withCloseButton={false} padding={0} trapFocus>
      <Stack gap={0} h="100%">
        <Group p="md" justify="space-between" wrap="nowrap" gap="xs">
          <Group gap="xs" wrap="nowrap" style={{ minWidth: 0 }}>
            {canGoBack && (
              <ActionIcon variant="subtle" color="gray" aria-label="Voltar" onClick={() => (trimmedQuery ? setQuery('') : back())}>
                <IconArrowLeft size={18} />
              </ActionIcon>
            )}
            <Text fw={700} size="md" truncate>{title}</Text>
          </Group>
          <ActionIcon variant="subtle" color="gray" aria-label="Fechar ajuda" onClick={handleClose}>
            <IconX size={18} />
          </ActionIcon>
        </Group>
        <Divider />
        <div style={{ padding: 'var(--mantine-spacing-md)', paddingBottom: 0 }}>
          <TextInput
            placeholder="Buscar no manual…"
            leftSection={<IconSearch size={16} />}
            aria-label="Buscar no manual"
            value={query}
            onChange={(e) => setQuery(e.currentTarget.value)}
          />
        </div>

        <ScrollArea style={{ flex: 1 }} p="md">
          {trimmedQuery ? (
            searchResults.length === 0 ? (
              <EmptyState icon={<IconSearch size={28} />} title="Nenhum resultado"
                description="Tente outras palavras, ou navegue pelas categorias depois de limpar a busca." />
            ) : (
              <Stack gap={4}>
                {searchResults.map((a) => <HelpArticleListItem key={a.id} article={a} onClick={() => openArticle(a.id)} />)}
              </Stack>
            )
          ) : screen.type === 'home' ? (
            <Stack gap="lg">
              {WHATS_NEW.length > 0 && (
                <Paper p="sm" withBorder>
                  <Group gap={6} mb={6}>
                    <IconSparkles size={16} />
                    <Text size="sm" fw={700}>Novidades</Text>
                  </Group>
                  <Stack gap={8}>
                    {WHATS_NEW.map((n) => (
                      <div key={n.id}>
                        <Text size="sm" fw={600}>{n.title}</Text>
                        <Text size="xs" c="dimmed">{n.description}</Text>
                        <Text size="xs" c="dimmed" fs="italic">{n.date}</Text>
                      </div>
                    ))}
                  </Stack>
                </Paper>
              )}
              <div>
                <Text size="xs" fw={600} c="dimmed" tt="uppercase" mb={6}>Categorias</Text>
                <Stack gap={2}>
                  {orderedCategories.map((c) => <CategoryRow key={c.id} category={c} onClick={() => openCategory(c.id)} />)}
                </Stack>
              </div>
            </Stack>
          ) : screen.type === 'category' ? (
            <Stack gap={4}>
              {getArticlesForCategory(screen.id)
                .filter((a) => visibleArticles.includes(a))
                .map((a) => <HelpArticleListItem key={a.id} article={a} onClick={() => openArticle(a.id)} />)}
            </Stack>
          ) : (
            (() => {
              const article = getArticleById(screen.id);
              return article ? <HelpArticleView article={article} onOpenArticle={openArticle} /> : (
                <EmptyState icon={<IconSearch size={28} />} title="Artigo não encontrado" description="Este artigo pode ter sido removido ou renomeado." />
              );
            })()
          )}
        </ScrollArea>
      </Stack>
    </Drawer>
  );
}
