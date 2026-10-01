import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'documents',
  label: 'Documentos',
  description: 'CRLV, seguro, exames e outros documentos com vencimento, de veículos, motoristas, implementos ou da empresa.',
  requiredPermission: PERMISSIONS.documents.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'documents-overview',
    categoryId: 'documents',
    title: 'O que pode ter documentos',
    summary: 'Veículos, motoristas, implementos e a própria empresa podem ter documentos — cada um com seu catálogo de tipos.',
    whyItMatters: 'A CNH do motorista não é um "documento": a validade dela fica no próprio cadastro do motorista, não aqui.',
  },
  {
    id: 'documents-add',
    categoryId: 'documents',
    title: 'Como adicionar um documento',
    summary: 'Escolha o tipo, o dono (veículo, motorista, implemento ou empresa), a data de vencimento e anexe o arquivo.',
    steps: [
      'Acesse "Documentos" (ou a aba Documentos do registro) e clique em "Novo documento".',
      'Escolha o tipo (CRLV, seguro, exame toxicológico, ASO, etc.) e o dono.',
      'Informe a data de vencimento, se o tipo tiver validade.',
      'Anexe o arquivo (PDF, JPG ou PNG, até 10 MB).',
      'Salve.',
    ],
    requiredPermission: PERMISSIONS.documents.manage,
    relatedArticleIds: ['documents-status'],
  },
  {
    id: 'documents-status',
    categoryId: 'documents',
    title: 'O que significam os status de um documento',
    summary: 'Válido, Vencendo, Vencido, Sem validade ou Substituído.',
    example:
      'Válido\nFalta mais que o prazo de alerta do tipo para vencer.\n\n' +
      'Vencendo\nJá entrou na janela de alerta configurada para esse tipo de documento.\n\n' +
      'Vencido\nA data de vencimento já passou.\n\n' +
      'Sem validade\nEsse tipo de documento não vence (ex.: um documento informativo).\n\n' +
      'Substituído\nFoi trocado por uma renovação mais recente; sai dos alertas, mas continua no histórico.',
    whyItMatters: 'O status nunca é escolhido manualmente — é sempre calculado a partir da data de vencimento, então está sempre certo no dia em que você olha.',
  },
  {
    id: 'documents-renew',
    categoryId: 'documents',
    title: 'Como renovar um documento',
    summary: 'Cadastre o novo documento marcando que ele substitui o anterior — o antigo some dos alertas, mas fica no histórico.',
    requiredPermission: PERMISSIONS.documents.manage,
  },
  {
    id: 'documents-delete',
    categoryId: 'documents',
    title: 'Como excluir um documento cadastrado por engano',
    summary: 'Use a exclusão apenas para um cadastro errado — renovação é diferente de exclusão.',
    requiredPermission: PERMISSIONS.documents.delete,
  },
];
