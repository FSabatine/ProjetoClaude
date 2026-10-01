import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'faq',
  label: 'Perguntas frequentes',
  description: 'Respostas diretas para as dúvidas mais comuns.',
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'faq-cannot-assign-driver',
    categoryId: 'faq',
    title: 'Por que não consigo alocar esse motorista a esse veículo?',
    summary: 'Confira: o veículo está inativo? O motorista está desligado, afastado ou com a CNH vencida? Um dos dois já tem uma alocação ativa?',
    whyItMatters: 'Qualquer uma dessas condições bloqueia a alocação. Se for só um conflito (os dois já têm alocação ativa com outra pessoa), o sistema pergunta se você quer confirmar a troca.',
    keywords: ['alocar', 'motorista', 'veículo', 'bloqueado'],
    relatedArticleIds: ['assignments-assign', 'drivers-license'],
  },
  {
    id: 'faq-cannot-save-mileage',
    categoryId: 'faq',
    title: 'Por que não consigo salvar essa quilometragem?',
    summary: 'A leitura é menor que a última registrada (precisa de uma correção, não de uma leitura comum), ou a data informada é futura.',
    keywords: ['hodômetro', 'km', 'salvar'],
    relatedArticleIds: ['mileage-decrease-rule'],
  },
  {
    id: 'faq-vehicle-unavailable',
    categoryId: 'faq',
    title: 'Por que meu veículo está marcado como indisponível?',
    summary: '"Indisponível" é uma condição manual, para quando o veículo está fora de uso por um motivo que não é manutenção (documento pendente, sinistro, aguardando papelada).',
    whyItMatters: 'Se for manutenção, a condição certa é "Em manutenção", e ela passa a ser controlada pela ordem de serviço, não editada manualmente.',
    keywords: ['indisponível', 'situação'],
    relatedArticleIds: ['vehicles-status'],
  },
  {
    id: 'faq-overdue-maintenance',
    categoryId: 'faq',
    title: 'O que significa uma manutenção atrasada?',
    summary: 'O item do plano de manutenção passou do vencimento e também da carência configurada.',
    whyItMatters: 'A ação recomendada é abrir uma solicitação de manutenção (ou uma ordem de serviço direta) para esse veículo.',
    keywords: ['atrasada', 'overdue', 'vencida'],
    relatedArticleIds: ['maintenance-alerts'],
  },
  {
    id: 'faq-find-expired-document',
    categoryId: 'faq',
    title: 'Como encontro os documentos vencidos?',
    summary: 'Em "Documentos", filtre por situação "Vencido" (ou "Vencendo") — ou clique no número correspondente no Painel.',
    keywords: ['documento vencido', 'filtro'],
    relatedArticleIds: ['documents-status', 'search-lists'],
  },
  {
    id: 'faq-report-vehicle-problem',
    categoryId: 'faq',
    title: 'Como reporto um problema no veículo?',
    summary: 'Registre uma ocorrência (ou reprove o item correspondente num checklist, se estiver inspecionando o veículo naquele momento).',
    keywords: ['problema', 'reportar', 'avaria'],
    relatedArticleIds: ['occurrences-create', 'checklists-run'],
  },
  {
    id: 'faq-workorder-wont-complete',
    categoryId: 'faq',
    title: 'Por que não consigo concluir a ordem de serviço?',
    summary: 'Algum item obrigatório ainda está pendente — marque-o como concluído ou dispensado antes de fechar a ordem.',
    keywords: ['ordem de serviço', 'concluir', 'fechar'],
    relatedArticleIds: ['maintenance-workorders-execution'],
  },
];
