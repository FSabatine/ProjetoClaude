import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'mileage',
  label: 'Quilometragem',
  description: 'Como registrar e acompanhar o hodômetro dos veículos.',
  requiredPermission: PERMISSIONS.mileage.record,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'mileage-update',
    categoryId: 'mileage',
    title: 'Como atualizar a quilometragem de um veículo',
    summary: 'Na aba "Quilometragem" do veículo, registre a nova leitura do hodômetro.',
    steps: [
      'Abra o veículo e vá na aba "Quilometragem".',
      'Clique em "Registrar leitura" e informe o valor atual do hodômetro.',
      'Salve — o hodômetro do veículo é atualizado na hora.',
    ],
    whyItMatters: 'O hodômetro do cadastro do veículo não pode mais ser editado diretamente: ele só muda através de uma leitura registrada aqui, o que garante que sempre existe um histórico de como ele chegou a esse valor.',
    relatedArticleIds: ['mileage-decrease-rule', 'mileage-suspicious'],
  },
  {
    id: 'mileage-why-history',
    categoryId: 'mileage',
    title: 'Por que existe um histórico de leituras',
    summary: 'Cada leitura fica registrada com data, origem (manual, checklist, cadastro ou correção) e quem a lançou.',
    whyItMatters: 'Isso permite calcular quantos km a frota rodou no mês, identificar veículos sem leitura recente e auditar qualquer correção feita no passado.',
  },
  {
    id: 'mileage-decrease-rule',
    categoryId: 'mileage',
    title: 'Por que não consigo salvar uma quilometragem menor que a anterior',
    summary: 'O sistema nunca aceita uma leitura menor que a última válida — isso evita erro de digitação ou retrocesso indevido.',
    example: 'A leitura (9.000 km) é menor que a última leitura válida (10.300 km em 01/10/2026). Confira o valor.',
    whyItMatters: 'Se o hodômetro foi realmente trocado (painel novo) ou a leitura anterior estava errada, isso não é um erro de digitação — é um caso para "Correção".',
  },
  {
    id: 'mileage-suspicious',
    categoryId: 'mileage',
    title: 'Por que uma leitura ficou "suspeita — em revisão"',
    summary: 'Um aumento acima de 1.500 km por dia é tratado como suspeito (geralmente um dígito a mais) e fica pendente de revisão.',
    whyItMatters: 'Uma leitura suspeita não é aplicada ao hodômetro atual até ser revisada — assim, um erro de digitação não trava os registros seguintes nem distorce os relatórios.',
    steps: [
      'Quem tem permissão para gerenciar hodômetro vê as leituras pendentes na aba Quilometragem.',
      'Aprovar aplica a leitura normalmente.',
      'Rejeitar exige uma justificativa e descarta a leitura.',
    ],
    requiredPermission: PERMISSIONS.mileage.manage,
  },
  {
    id: 'mileage-correction',
    categoryId: 'mileage',
    title: 'Como corrigir o hodômetro (ex.: troca de painel)',
    summary: 'Uma correção, diferente de uma leitura comum, pode ser menor que a anterior — mas exige o motivo.',
    requiredPermission: PERMISSIONS.mileage.manage,
  },
];
