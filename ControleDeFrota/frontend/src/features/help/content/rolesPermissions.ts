import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'rolesPermissions',
  label: 'Papéis e permissões',
  description: 'O que cada papel de acesso permite fazer no sistema.',
  requiredPermission: PERMISSIONS.roles.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'roles-overview',
    categoryId: 'rolesPermissions',
    title: 'Os papéis de acesso do sistema',
    summary: 'Cada usuário tem um ou mais papéis; o acesso dele é a soma das permissões de todos eles.',
    example:
      'Administrador da plataforma\nAcesso total, inclusive a todas as empresas.\n\n' +
      'Administrador\nAcesso total dentro da própria empresa.\n\n' +
      'Gestor de frota\nCadastros, alocações, hodômetro, documentos, checklists, ocorrências e manutenção.\n\n' +
      'Operações\nOperação do dia a dia: motoristas, alocações, hodômetro, documentos, checklists, ocorrências; pode solicitar manutenção.\n\n' +
      'Manutenção\nPlanos de manutenção, oficinas, solicitações e ordens de serviço; acompanha veículos e ocorrências.\n\n' +
      'Financeiro\nConsulta a frota e os custos de manutenção para fins financeiros.\n\n' +
      'Motorista\nReservado para uma versão futura (acesso do motorista pelo celular).\n\n' +
      'Visualizador\nSó consulta, não altera nada.',
    whyItMatters:
      'Um usuário pode ter mais de um papel ao mesmo tempo (por exemplo, Operações + Manutenção) — isso é mais flexível ' +
      'do que criar um papel novo para cada combinação possível.',
    notes: ['Só os papéis que realmente existem no sistema hoje estão listados aqui.'],
    relatedArticleIds: ['users-create'],
  },
  {
    id: 'roles-how-assigned',
    categoryId: 'rolesPermissions',
    title: 'Como as permissões são decididas',
    summary: 'O acesso é sempre decidido pela permissão específica da ação, nunca pelo nome do papel.',
    whyItMatters:
      'Isso significa que, se um papel novo precisar das mesmas permissões de "Operações" mas sem uma delas, ' +
      'basta criar um papel com a combinação certa — o sistema nunca pergunta "esse usuário é Operações?", só ' +
      '"esse usuário tem a permissão de registrar leitura de hodômetro?".',
    notes: ['Por segurança, ninguém consegue atribuir a outra pessoa uma permissão que ele próprio não tem.'],
  },
];
