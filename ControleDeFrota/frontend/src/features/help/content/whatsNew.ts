import type { HelpWhatsNewEntry } from '../types';

/**
 * Só marcos realmente lançados (nunca um recurso planejado) — ver docs/CHANGELOG.md para a versão técnica completa.
 * Mais recente primeiro.
 */
export const WHATS_NEW: HelpWhatsNewEntry[] = [
  {
    id: 'whats-new-reports',
    title: 'Relatórios cruzados, comparação, saúde do veículo e busca global',
    description: 'Novo menu Relatórios (desempenho da frota, manutenção e problemas recorrentes), comparação de até 6 veículos com a média da frota e do tipo, nota de saúde operacional no veículo, destaques no painel, exportação em CSV/Excel/PDF, busca global (Ctrl+K) e linha do tempo do veículo com filtro por área.',
    date: 'outubro de 2026',
  },
  {
    id: 'whats-new-alerts',
    title: 'Alertas, "Requer atenção" e automações',
    description: 'O painel agora começa pelo que precisa de atenção. O sistema confere a frota a cada hora e cria alertas explicados (manutenção atrasada, consumo fora do padrão, custo acima da média do tipo, orçamento perto do limite, pneu com sulco baixo, despesa e documento vencidos), com central de alertas, notificações no sino e regras de automação configuráveis.',
    date: 'outubro de 2026',
  },
  {
    id: 'whats-new-financial',
    title: 'Gestão financeira',
    description: 'Painel financeiro, despesas, categorias e centros de custo configuráveis, despesas recorrentes geradas automaticamente, orçamentos e orçado x realizado, aba Financeiro do veículo, custo por km, TCO, ranking de veículos e relatórios financeiros — tudo integrado ao que já é registrado em combustível, manutenção e pneus.',
    date: 'outubro de 2026',
  },
  {
    id: 'whats-new-tires',
    title: 'Gestão de pneus',
    description: 'Cada pneu pelo número de fogo, diagrama de eixos do veículo, instalação, rodízio, inspeção de sulco e pressão, consertos, recapagens, custo por km, baixa, painel e relatórios de pneus.',
    date: 'outubro de 2026',
  },
  {
    id: 'whats-new-fuel',
    title: 'Gestão de combustível',
    description: 'Abastecimentos com total calculado, consumo de tanque cheio a tanque cheio, postos e preços de referência, alertas para revisão, painel e relatórios de combustível.',
    date: 'outubro de 2026',
  },
  {
    id: 'whats-new-maintenance',
    title: 'Manutenção da frota',
    description: 'Planos preventivos, solicitações de manutenção, ordens de serviço, oficinas e histórico de horímetro.',
    date: 'outubro de 2026',
  },
  {
    id: 'whats-new-operations',
    title: 'Controle operacional',
    description: 'Alocação de motoristas, histórico de hodômetro, documentos com vencimento, checklists e ocorrências.',
    date: 'setembro de 2026',
  },
];
