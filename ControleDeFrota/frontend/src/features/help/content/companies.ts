import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'companies',
  label: 'Empresas',
  description: 'Como o sistema separa os dados de cada empresa e como editar as informações da sua.',
  requiredPermission: PERMISSIONS.companies.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'companies-overview',
    categoryId: 'companies',
    title: 'O que é uma empresa no sistema',
    summary: 'Cada empresa enxerga apenas os próprios veículos, motoristas e registros — é o que chamamos de "multiempresa".',
    whyItMatters:
      'Se a sua organização administra mais de uma empresa (ex.: um grupo com várias transportadoras), cada uma tem ' +
      'sua própria frota, seus próprios usuários e seus próprios dados, completamente isolados uns dos outros.',
    requiredPermission: PERMISSIONS.companies.view,
  },
  {
    id: 'companies-register',
    categoryId: 'companies',
    title: 'Como cadastrar uma empresa',
    summary: 'Só o administrador da plataforma cadastra novas empresas.',
    steps: [
      'Acesse "Empresas" no menu (visível apenas para o administrador da plataforma).',
      'Clique em "Nova empresa".',
      'Preencha razão social, CNPJ e o endereço completo — todos obrigatórios.',
      'Salve. A empresa já pode receber seu primeiro usuário administrador.',
    ],
    notes: ['O CNPJ aceita tanto o formato numérico tradicional quanto o novo formato alfanumérico da Receita Federal.'],
    requiredPermission: PERMISSIONS.companies.manage,
    relatedArticleIds: ['users-create'],
  },
  {
    id: 'companies-edit',
    categoryId: 'companies',
    title: 'Como editar os dados da empresa',
    summary: 'Em "Minha empresa" você edita razão social, nome fantasia, contato e endereço.',
    steps: [
      'Acesse "Minha empresa" no menu.',
      'Altere os campos necessários.',
      'Salve — as alterações valem imediatamente para todos os usuários da empresa.',
    ],
    notes: ['Um usuário comum edita apenas a própria empresa; só o administrador da plataforma edita qualquer uma.'],
    requiredPermission: PERMISSIONS.companies.view,
  },
  {
    id: 'companies-inactive',
    categoryId: 'companies',
    title: 'O que acontece quando uma empresa fica inativa',
    summary: 'Uma empresa inativa impede o login e a renovação de sessão de todos os seus usuários.',
    whyItMatters: 'É o jeito de suspender o acesso de uma empresa inteira sem apagar o cadastro nem o histórico dela.',
    notes: [
      'Um usuário não consegue inativar nem excluir a própria empresa.',
      'Não é possível excluir uma empresa que ainda tem usuários ativos — o caminho normal é inativar.',
    ],
    requiredPermission: PERMISSIONS.companies.manage,
  },
];
