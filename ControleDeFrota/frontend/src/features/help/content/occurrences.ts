import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'occurrences',
  label: 'Ocorrências',
  description: 'Problemas, avarias e observações registradas sobre um veículo, motorista ou implemento.',
  requiredPermission: PERMISSIONS.occurrences.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'occurrences-overview',
    categoryId: 'occurrences',
    title: 'O que é uma ocorrência',
    summary: 'Um problema mecânico, avaria, acidente, pneu, equipamento faltando, relato do motorista ou observação geral.',
    whyItMatters: 'É a porta de entrada para problemas que podem virar uma manutenção — mas abrir uma ocorrência não cria uma ordem de serviço sozinho.',
    relatedArticleIds: ['occurrences-to-maintenance'],
  },
  {
    id: 'occurrences-create',
    categoryId: 'occurrences',
    title: 'Como registrar uma ocorrência',
    summary: 'Informe o veículo, motorista ou implemento envolvido, o tipo, a gravidade e o que aconteceu.',
    steps: [
      'Acesse "Ocorrências" e clique em "Registrar ocorrência" (ou use o botão na tela do veículo/motorista).',
      'Escolha o tipo e a gravidade (Baixa, Média, Alta ou Crítica — Crítica significa que o veículo não deve ser usado até ser avaliado).',
      'Descreva o que aconteceu e, se quiser, anexe fotos.',
      'Salve.',
    ],
    requiredPermission: PERMISSIONS.occurrences.create,
  },
  {
    id: 'occurrences-status-flow',
    categoryId: 'occurrences',
    title: 'Como funciona a situação de uma ocorrência',
    summary: 'Aberta → Em análise → Resolvida ou Cancelada. Resolvida e Cancelada são finais.',
    whyItMatters: 'Fechar uma ocorrência (resolver ou cancelar) exige escrever o que foi feito ou o motivo — isso nunca fica em branco.',
    notes: ['Se o mesmo problema voltar depois de resolvido, registre uma nova ocorrência; o histórico da antiga não é reaberto.'],
    requiredPermission: PERMISSIONS.occurrences.manage,
  },
  {
    id: 'occurrences-to-maintenance',
    categoryId: 'occurrences',
    title: 'Como transformar uma ocorrência em manutenção',
    summary: 'Na tela da ocorrência, o botão "Abrir solicitação de manutenção" cria a solicitação já vinculada a ela.',
    whyItMatters: 'Essa ação é sempre manual — nem toda ocorrência precisa virar manutenção (ex.: um relato genérico do motorista), então quem decide é o gestor, olhando caso a caso.',
    relatedArticleIds: ['maintenance-requests'],
  },
];
