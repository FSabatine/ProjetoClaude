import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'searchAndFilters',
  label: 'Busca e filtros',
  description: 'Como encontrar rápido o registro que você procura, em qualquer lista do sistema.',
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'search-lists',
    categoryId: 'searchAndFilters',
    title: 'Busca, filtros, ordenação e paginação',
    summary: 'Toda lista do sistema tem busca, filtros e ordenação por coluna, e guarda isso no endereço da página.',
    steps: [
      'Digite na caixa de busca — a lista é filtrada automaticamente, sem precisar apertar Enter.',
      'Use os filtros ao lado da busca para refinar por situação, tipo, data, etc. — dá para combinar vários ao mesmo tempo.',
      'Clique no cabeçalho de uma coluna para ordenar por ela; clique de novo para inverter a ordem.',
      'Quando os filtros não encontram nada, aparece um botão "Limpar filtros".',
    ],
    whyItMatters: 'Como a busca, os filtros e a ordenação ficam no endereço (URL) da página, você pode copiar o link e mandar para outra pessoa, ou voltar com o botão do navegador e encontrar a lista exatamente como estava.',
    notes: ['No celular, as listas aparecem como cartões em vez de tabela, para caber melhor na tela.'],
  },
];
