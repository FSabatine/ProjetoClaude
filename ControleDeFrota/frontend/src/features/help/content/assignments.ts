import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'assignments',
  label: 'Alocações',
  description: 'Qual motorista é responsável por qual veículo, e como esse histórico é mantido.',
  requiredPermission: PERMISSIONS.assignments.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'assignments-assign',
    categoryId: 'assignments',
    title: 'Como alocar um motorista a um veículo',
    summary: 'Na aba "Motorista" do veículo (ou "Veículos" do motorista), escolha o motorista e confirme.',
    steps: [
      'Abra o veículo e vá na aba "Motorista".',
      'Escolha o motorista na busca.',
      'Confirme — a partir daí, esse motorista é o responsável pelo veículo.',
    ],
    whyItMatters: 'Um veículo tem no máximo um motorista responsável por vez, e um motorista tem no máximo um veículo — por isso o sistema pede confirmação quando já existe uma alocação ativa de qualquer um dos dois lados.',
    notes: [
      'Veículo inativo não recebe motorista.',
      'Motorista desligado, afastado ou com CNH vencida não recebe uma nova alocação.',
      'Veículo em manutenção ou indisponível pode manter o motorista que já tinha.',
    ],
    requiredPermission: PERMISSIONS.assignments.manage,
    relatedArticleIds: ['assignments-conflict', 'drivers-license'],
  },
  {
    id: 'assignments-conflict',
    categoryId: 'assignments',
    title: 'O que fazer quando aparece "o veículo está com..."',
    summary: 'Esse aviso aparece quando o veículo ou o motorista já têm uma alocação ativa — confirme a troca para encerrar a anterior e começar a nova.',
    example: 'Não foi possível alocar: o veículo está com Maria Silva. Confirme a troca para encerrar a alocação atual.',
    whyItMatters: 'A troca é feita numa única ação: a alocação anterior é encerrada no exato momento em que a nova começa, então o histórico nunca fica com uma lacuna.',
    requiredPermission: PERMISSIONS.assignments.manage,
  },
  {
    id: 'assignments-end',
    categoryId: 'assignments',
    title: 'Como encerrar uma alocação',
    summary: 'Use "Encerrar alocação" na aba do veículo ou do motorista, informando a data de término e, se quiser, um motivo.',
    requiredPermission: PERMISSIONS.assignments.manage,
  },
  {
    id: 'assignments-why-history',
    categoryId: 'assignments',
    title: 'Por que o histórico de alocações nunca é apagado',
    summary: 'Trocar o motorista de um veículo não apaga a alocação anterior — ela é encerrada e fica registrada.',
    whyItMatters: 'Isso permite responder, meses depois, "quem estava com esse veículo em tal data" — informação importante em caso de multa, sinistro ou auditoria.',
  },
];
