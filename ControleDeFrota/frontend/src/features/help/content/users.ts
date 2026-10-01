import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'users',
  label: 'Usuários',
  description: 'Quem acessa o sistema e como gerenciar essas contas.',
  requiredPermission: PERMISSIONS.users.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'users-create',
    categoryId: 'users',
    title: 'Como criar um usuário',
    summary: 'Em "Usuários", cadastre nome, e-mail e ao menos um papel de acesso.',
    steps: [
      'Acesse "Usuários" no menu e clique em "Novo usuário".',
      'Informe nome e e-mail (o e-mail é o login, único em todo o sistema).',
      'Escolha ao menos um papel de acesso (ex.: Gestor de frota, Operações).',
      'Salve. Uma senha inicial é definida pelo administrador na criação.',
    ],
    notes: ['Você não consegue atribuir a outra pessoa um papel com permissões que você mesmo não tem.'],
    requiredPermission: PERMISSIONS.users.manage,
    relatedArticleIds: ['roles-overview'],
  },
  {
    id: 'users-edit',
    categoryId: 'users',
    title: 'Como editar ou trocar os papéis de um usuário',
    summary: 'Abra o usuário na lista e ajuste nome, papéis ou situação.',
    steps: [
      'Acesse "Usuários", clique no usuário desejado.',
      'Ajuste os campos necessários, inclusive os papéis atribuídos.',
      'Salve.',
    ],
    notes: ['Ninguém remove os próprios papéis — isso evita perder por acidente o último acesso administrativo.'],
    requiredPermission: PERMISSIONS.users.manage,
  },
  {
    id: 'users-deactivate',
    categoryId: 'users',
    title: 'Como desativar um usuário',
    summary: 'Desativar revoga o acesso imediatamente, sem apagar o histórico do que essa pessoa fez.',
    whyItMatters: 'Ao desativar (ou excluir) um usuário, todas as sessões abertas dele são encerradas na hora.',
    notes: ['Ninguém consegue desativar ou excluir a própria conta.'],
    requiredPermission: PERMISSIONS.users.manage,
  },
  {
    id: 'users-auth',
    categoryId: 'users',
    title: 'Como funciona o login',
    summary: 'Você entra com e-mail e senha; a sessão fica ativa enquanto você usa o sistema e expira depois de um tempo sem uso.',
    notes: [
      'Depois de 5 tentativas de senha incorreta seguidas, a conta fica bloqueada por 15 minutos por segurança.',
      'Ao trocar de papel ou ser desativado, o acesso é atualizado em poucos minutos, mesmo que você já esteja logado.',
    ],
  },
  {
    id: 'users-password-reset',
    categoryId: 'users',
    title: 'Como redefinir a senha de um usuário',
    summary: 'Hoje a redefinição de senha é feita pelo administrador, direto na tela do usuário.',
    steps: ['Acesse "Usuários", abra o usuário.', 'Use a opção de redefinir senha e informe a nova senha ao usuário por um canal seguro.'],
    notes: ['Ainda não existe "esqueci minha senha" por e-mail nesta versão — essa função depende de um serviço de e-mail configurado.'],
    requiredPermission: PERMISSIONS.users.manage,
  },
];
