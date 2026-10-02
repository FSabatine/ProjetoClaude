import type { HelpWhatsNewEntry } from '../types';

/**
 * Só marcos realmente lançados (nunca um recurso planejado) — ver docs/CHANGELOG.md para a versão técnica completa.
 * Mais recente primeiro.
 */
export const WHATS_NEW: HelpWhatsNewEntry[] = [
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
