import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'troubleshooting',
  label: 'Problemas comuns',
  description: 'O que fazer quando algo não aparece, não salva ou parece errado.',
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'trouble-menu-missing',
    categoryId: 'troubleshooting',
    title: 'Não encontro um menu ou um botão',
    summary: 'O sistema só mostra o que o seu papel permite. Se um módulo, botão ou valor não aparece, falta a permissão correspondente.',
    steps: ['Confira seus papéis no rodapé do menu lateral.', 'Peça ao administrador da empresa a permissão necessária (veja "Papéis e permissões").'],
    keywords: ['sumiu', 'não aparece', 'sem acesso', 'menu'],
    relatedArticleIds: ['roles-overview', 'roles-sensitive'],
  },
  {
    id: 'trouble-values-hidden',
    categoryId: 'troubleshooting',
    title: 'Os valores em R$ aparecem como "—" ou "sem acesso"',
    summary: 'Valores financeiros dependem das permissões de custo de cada módulo. Sem elas, a tela mostra a informação sem os valores.',
    notes: ['"Totais parciais" significa que você vê o custo de alguns módulos, mas não de todos — o total não inclui tudo.'],
    keywords: ['valor escondido', 'r$', 'totais parciais'],
    relatedArticleIds: ['financial-permissions', 'roles-sensitive'],
  },
  {
    id: 'trouble-session-expired',
    categoryId: 'troubleshooting',
    title: 'Fui desconectado ("sessão expirada")',
    summary: 'Por segurança a sessão expira após um período sem uso, ou quando a mesma conta é usada de forma suspeita. Basta entrar de novo.',
    keywords: ['sessão expirada', 'deslogou', 'login'],
  },
  {
    id: 'trouble-numbers-differ',
    categoryId: 'troubleshooting',
    title: 'Um número do painel não bate com o relatório',
    summary: 'Confira o período: o painel usa o mês atual ou os últimos 30 dias; os relatórios usam o período escolhido (padrão 90 dias).',
    notes: [
      'Alertas são atualizados a cada hora (a hora aparece no topo do "Requer atenção"); as filas de trabalho e os relatórios são em tempo real.',
      'Abastecimentos e despesas canceladas não entram em nenhum total.',
    ],
    keywords: ['diferente', 'não bate', 'divergência', 'número errado'],
    relatedArticleIds: ['alerts-attention', 'reports-fleet-performance'],
  },
  {
    id: 'trouble-alert-not-closing',
    categoryId: 'troubleshooting',
    title: 'Resolvi o problema, mas o alerta continua aberto',
    summary: 'O alerta é encerrado na próxima verificação automática (a cada hora). Para encerrar na hora, marque-o como resolvido ou, se tiver permissão, use "Verificar agora" nas regras de automação.',
    keywords: ['alerta não some', 'alerta continua'],
    relatedArticleIds: ['alerts-statuses'],
  },
  {
    id: 'trouble-export',
    categoryId: 'troubleshooting',
    title: 'A exportação não trouxe todas as linhas',
    summary: 'Cada arquivo traz no máximo 5.000 linhas. Use filtros (período, tipo de veículo) para exportar em partes.',
    keywords: ['exportar incompleto', 'excel faltando'],
    relatedArticleIds: ['reports-export'],
  },
];
