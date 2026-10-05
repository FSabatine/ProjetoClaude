import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'gettingStarted',
  label: 'Primeiros passos',
  description: 'Uma visão geral do sistema antes de entrar nos detalhes de cada módulo.',
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'gs-overview',
    categoryId: 'gettingStarted',
    title: 'O que é o Controle de Frota',
    summary: 'Um sistema para gerenciar veículos, motoristas, manutenção e tudo o que acontece na operação diária da frota.',
    whyItMatters:
      'Ele reúne em um só lugar o cadastro da frota (veículos, implementos, motoristas), o controle do dia a dia ' +
      '(quem está com qual veículo, quilometragem, documentos vencendo, checklists, ocorrências) e a manutenção ' +
      '(planos preventivos, solicitações e ordens de serviço).',
    steps: [
      'Faça login com seu e-mail e senha.',
      'O Painel mostra um resumo da frota e os alertas mais urgentes.',
      'O menu à esquerda leva a cada módulo que você tem permissão para ver.',
      'Cada módulo tem uma lista (com busca e filtros) e, para veículos e motoristas, uma tela de detalhe com abas.',
    ],
    keywords: ['sistema', 'o que é', 'introdução'],
    relatedArticleIds: ['gs-navigation', 'dashboard-overview'],
  },
  {
    id: 'gs-navigation',
    categoryId: 'gettingStarted',
    title: 'Como navegar pelo sistema',
    summary: 'O menu lateral mostra só os módulos que o seu perfil permite acessar.',
    steps: [
      'No menu à esquerda, os itens estão agrupados em Operação, Configurações e Administração.',
      'Você só vê um item de menu se tiver a permissão correspondente — se um módulo não aparece, seu perfil não dá acesso a ele.',
      'Clique em um veículo ou motorista na lista para abrir a tela de detalhe (o "hub"), com abas para cada assunto (documentos, checklists, ocorrências, manutenção, histórico…).',
      'A edição do cadastro fica numa tela separada (botão "Editar"); a tela de detalhe é sempre a de consulta e acompanhamento.',
    ],
    notes: ['No celular, toque no ícone de menu (≡) no canto superior esquerdo para abrir a navegação.'],
    relatedArticleIds: ['gs-overview', 'search-lists'],
  },
  {
    id: 'gs-theme',
    categoryId: 'gettingStarted',
    title: 'Como alternar entre tema claro e escuro',
    summary: 'O ícone de sol/lua no topo da tela alterna o tema a qualquer momento.',
    steps: [
      'No cabeçalho, ao lado do seu nome, clique no ícone de sol (tema claro) ou lua (tema escuro).',
      'A escolha é lembrada nas próximas vezes que você acessar o sistema.',
    ],
    keywords: ['dark mode', 'modo escuro', 'aparência'],
  },
  {
    id: 'gs-help',
    categoryId: 'gettingStarted',
    title: 'Como usar a Central de Ajuda',
    summary: 'O ícone de interrogação (?) no cabeçalho abre este manual sem precisar sair da tela em que você está.',
    steps: [
      'Clique no ícone "?" ao lado do botão de tema, a qualquer momento.',
      'Use a busca para perguntar algo com suas próprias palavras (ex.: "como alocar motorista").',
      'Ou navegue pelas categorias — elas aparecem priorizadas de acordo com a tela em que você está.',
      'Dentro de um artigo, "Relacionados" leva a outros artigos sobre o mesmo assunto, sem fechar a ajuda.',
    ],
    relatedArticleIds: ['search-lists'],
  },
  {
    id: 'gs-notifications',
    categoryId: 'gettingStarted',
    title: 'Como funcionam os alertas do sistema',
    summary: 'O sistema confere a frota a cada hora e mostra o que precisa de atenção no Painel ("Requer atenção" e "Alertas prioritários"), na central de Alertas e no sino do cabeçalho.',
    whyItMatters:
      'Manutenções atrasadas, consumo fora do padrão, custos acima da média, orçamentos perto do limite, pneus no limite, ' +
      'despesas e documentos vencidos viram alertas explicados, com os números que os geraram e uma sugestão do que fazer.',
    notes: ['Os avisos são apenas dentro do sistema (sino). Envio por e-mail, push ou WhatsApp ainda não existe nesta versão.'],
    relatedArticleIds: ['dashboard-overview', 'alerts-overview', 'alerts-notifications'],
  },
];
