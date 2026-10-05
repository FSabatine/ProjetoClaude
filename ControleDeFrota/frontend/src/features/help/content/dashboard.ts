import { PERMISSIONS } from '../../../auth/permissions';
import type { HelpArticle, HelpCategory } from '../types';

export const CATEGORY: HelpCategory = {
  id: 'dashboard',
  label: 'Painel',
  description: 'O que cada número do painel significa e para onde ele leva.',
  requiredPermission: PERMISSIONS.dashboard.view,
};

export const ARTICLES: HelpArticle[] = [
  {
    id: 'dashboard-overview',
    categoryId: 'dashboard',
    title: 'Os blocos do Painel',
    summary: 'Requer atenção, Alertas prioritários, Frota, Manutenção, Financeiro, Operação e Quilometragem do mês — cada bloco só aparece se você tiver permissão para o módulo correspondente.',
    example:
      'Requer atenção\nO que precisa de alguém agora (manutenções atrasadas, consumo fora do padrão, pneus no limite, revisões e aprovações pendentes…), com a quantidade e um clique para os registros.\n\n' +
      'Alertas prioritários\nOs 5 alertas mais importantes da central de alertas, cada um com a explicação do motivo.\n\n' +
      'Frota\nQuantos veículos estão Disponíveis, Alocados, Em viagem, Indisponíveis, Em manutenção ou Inativos.\n\n' +
      'Manutenção\nQuantos itens vencem hoje, estão vencendo, atrasados, em andamento, aguardando peças ou foram concluídos no mês.\n\n' +
      'Financeiro\nCusto do mês, custo por km e quanto foi gasto no mês com combustível, manutenção, pneus e outras despesas (para quem pode ver valores).\n\n' +
      'Operação\nDocumentos vencendo/vencidos, checklists pendentes hoje, ocorrências em aberto e veículos sem leitura recente de hodômetro.\n\n' +
      'Quilometragem do mês\nQuanto a frota rodou, a média por veículo e qual tem o maior hodômetro.',
    whyItMatters: 'Todo número do painel é clicável e leva para a lista filtrada correspondente — ele nunca é só um número solto.',
    relatedArticleIds: ['gs-notifications', 'alerts-attention', 'alerts-overview'],
  },
];
