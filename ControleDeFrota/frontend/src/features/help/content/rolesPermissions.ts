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
      'Gestor de frota\nTudo da gestão da frota: cadastros, operação, manutenção, combustível, pneus, financeiro, alertas, regras de automação, assistente, mapa e rastreadores.\n\n' +
      'Operações\nDia a dia: motoristas, alocações, hodômetro, documentos, checklists, ocorrências, abastecimentos (sem valores da frota), inspeção de pneus, solicitação de manutenção, alertas, assistente e mapa.\n\n' +
      'Manutenção\nManutenção completa (planos, oficinas, solicitações, ordens de serviço e custos), borracharia (pneus), consumo de combustível, alertas, assistente e mapa.\n\n' +
      'Financeiro\nDespesas, orçamentos, centros de custo e todos os custos (combustível, manutenção, pneus), alertas e assistente.\n\n' +
      'Motorista\nReservado para uma versão futura (acesso do motorista pelo celular).\n\n' +
      'Visualizador\nSó consulta (sem valores financeiros), alertas, assistente e mapa — não altera nada.',
    whyItMatters:
      'Um usuário pode ter mais de um papel ao mesmo tempo (por exemplo, Operações + Manutenção) — isso é mais flexível ' +
      'do que criar um papel novo para cada combinação possível.',
    notes: ['Só os papéis que realmente existem no sistema hoje estão listados aqui.'],
    relatedArticleIds: ['users-create', 'roles-sensitive'],
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
  {
    id: 'roles-sensitive',
    categoryId: 'rolesPermissions',
    title: 'Permissões que protegem informação sensível',
    summary: 'Algumas permissões não liberam telas, e sim informação: valores em R$, dados pessoais e configurações de integração.',
    notes: [
      'Valores em R$: cada módulo tem a sua ("Visualizar custos de manutenção", "…de combustível", "…de pneus", "Visualizar valores financeiros"). Totais que somam vários módulos só aparecem completos com todas elas; faltando uma, o sistema avisa "totais parciais".',
      'Alertas, destaques, assistente, busca e exportações seguem as mesmas regras: ninguém vê por esses caminhos algo que não veria nas telas.',
      'Nome de motorista e documentos de motorista exigem "Visualizar motoristas".',
      '"Configurar rastreadores e integrações" dá acesso às chaves dos rastreadores — conceda só a quem administra o sistema.',
      '"Usar o assistente de análise da frota" permite perguntar; as respostas continuam limitadas às permissões de cada pessoa.',
    ],
    keywords: ['valores', 'custos', 'dados pessoais', 'sigilo', 'permissão de custo'],
    relatedArticleIds: ['roles-overview', 'financial-permissions', 'alerts-permissions', 'assistant-permissions'],
  },
];
