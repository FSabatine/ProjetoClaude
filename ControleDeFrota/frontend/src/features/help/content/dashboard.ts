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
    summary: 'Frota, Atenção, Quilometragem do mês, Manutenção e Alertas — cada bloco só aparece se você tiver permissão para o módulo correspondente.',
    example:
      'Frota\nQuantos veículos estão Disponíveis, Alocados, Em viagem, Indisponíveis, Em manutenção ou Inativos.\n\n' +
      'Atenção\nDocumentos vencendo/vencidos, checklists pendentes hoje, ocorrências em aberto e veículos sem leitura recente de hodômetro.\n\n' +
      'Quilometragem do mês\nQuanto a frota rodou, a média por veículo e qual tem o maior hodômetro.\n\n' +
      'Manutenção\nQuantos itens vencem hoje, estão vencendo, atrasados, em andamento, aguardando peças ou foram concluídos no mês.\n\n' +
      'Alertas\nOs 10 mais urgentes de todos os blocos, mais críticos primeiro — clique em um para ir direto à aba certa do registro.',
    whyItMatters: 'Todo número do painel é clicável e leva para a lista filtrada correspondente — ele nunca é só um número solto.',
    relatedArticleIds: ['gs-notifications'],
  },
];
