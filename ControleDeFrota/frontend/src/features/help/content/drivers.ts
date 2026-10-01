import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'drivers',
  label: 'Motoristas',
  description: 'Cadastro, CNH, situação e histórico dos motoristas.',
  requiredPermission: PERMISSIONS.drivers.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'drivers-register',
    categoryId: 'drivers',
    title: 'Como cadastrar um motorista',
    summary: 'Informe os dados pessoais e da CNH — o CPF e o número da CNH precisam ser válidos e únicos na empresa.',
    steps: [
      'Acesse "Motoristas" e clique em "Novo motorista".',
      'Preencha nome completo, CPF e data de nascimento (o motorista precisa ter entre 18 e 100 anos).',
      'Informe o número, a categoria e a validade da CNH.',
      'Endereço é opcional; se preenchido, CEP e UF precisam ser válidos.',
      'Salve.',
    ],
    notes: ['O mesmo CPF pode existir em duas empresas diferentes — cada empresa tem seu próprio cadastro, independente.'],
    requiredPermission: PERMISSIONS.drivers.create,
    relatedArticleIds: ['drivers-license', 'assignments-assign'],
  },
  {
    id: 'drivers-edit',
    categoryId: 'drivers',
    title: 'Como editar um motorista',
    summary: 'Abra o motorista na lista e ajuste os dados — inclusive a validade da CNH quando ela for renovada.',
    requiredPermission: PERMISSIONS.drivers.update,
  },
  {
    id: 'drivers-deactivate',
    categoryId: 'drivers',
    title: 'Situação do motorista: Ativo, Afastado e Desligado',
    summary: 'Afastado mantém o veículo reservado com ele; Desligado exige encerrar a alocação antes.',
    example:
      'Ativo\nPode receber uma nova alocação normalmente.\n\n' +
      'Afastado (férias, licença)\nMantém o veículo que já tinha, mas não recebe um novo enquanto estiver afastado.\n\n' +
      'Desligado\nNão pode ter alocação ativa — encerre a alocação antes de desligar o motorista.',
    whyItMatters: 'Um motorista com histórico operacional (alocações, checklists, ocorrências) não pode ser excluído — o caminho é marcá-lo como Desligado, preservando o histórico.',
    requiredPermission: PERMISSIONS.drivers.update,
    relatedArticleIds: ['assignments-end'],
  },
  {
    id: 'drivers-license',
    categoryId: 'drivers',
    title: 'CNH: categoria, validade e alertas de vencimento',
    summary: 'Uma CNH vencida ou a vencer em até 30 dias aparece como alerta no Painel.',
    whyItMatters:
      'Um motorista com a CNH vencida não pode receber uma nova alocação de veículo (exigência de trânsito) — ' +
      'uma CNH que vence hoje ainda é considerada válida.',
    notes: ['Exames médicos e cursos do motorista são cadastrados como Documentos, não fazem parte do cadastro da CNH em si.'],
    relatedArticleIds: ['documents-overview'],
  },
  {
    id: 'drivers-history',
    categoryId: 'drivers',
    title: 'Histórico do motorista',
    summary: 'A aba "Histórico", na tela do motorista, mostra tudo que aconteceu com ele: alocações, checklists, ocorrências e documentos.',
    whyItMatters: 'É uma linha do tempo única, útil para reconstruir "o que aconteceu com esse motorista" sem precisar visitar cada módulo separadamente.',
  },
];
