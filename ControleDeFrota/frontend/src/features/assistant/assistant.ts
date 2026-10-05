import { useMutation, useQuery } from '@tanstack/react-query';
import { api } from '../../api/client';

export interface AssistantAnswer {
  mode: 'Calculated' | 'AiExplained';
  answer: string;
  reason: string | null;
  evidence: string[];
  suggestedAction: string | null;
  sources: { label: string; link: string }[];
  toolsUsed: string[];
  focusVehicleId: string | null;
  insufficientData: boolean;
  notice: string | null;
  suggestions: string[];
}

export interface AssistantContext {
  vehicleId: string | null;
  page: string | null;
}

const VEHICLE_ROUTE = /^\/veiculos\/([0-9a-f-]{36})/i;

/** Where the user is → what the assistant should focus on (spec §6: no need to repeat the context). */
export function contextFromPath(pathname: string): AssistantContext {
  const vehicle = VEHICLE_ROUTE.exec(pathname);
  if (vehicle) return { vehicleId: vehicle[1], page: 'vehicle' };
  if (pathname.startsWith('/financeiro')) return { vehicleId: null, page: 'finance' };
  if (/^\/(ordens-servico|solicitacoes-manutencao|planos-manutencao|oficinas)/.test(pathname)) return { vehicleId: null, page: 'maintenance' };
  if (/^\/(combustivel|abastecimentos|postos)/.test(pathname)) return { vehicleId: null, page: 'fuel' };
  if (pathname.startsWith('/pneus')) return { vehicleId: null, page: 'tires' };
  if (pathname.startsWith('/alertas')) return { vehicleId: null, page: 'alerts' };
  if (pathname === '/') return { vehicleId: null, page: 'dashboard' };
  return { vehicleId: null, page: null };
}

/** Contextual shortcuts shown first in the assistant panel. */
export function contextualQuestions(context: AssistantContext): string[] {
  switch (context.page) {
    case 'vehicle': return ['Analise este veículo', 'Por que o custo deste veículo mudou?'];
    case 'finance': return ['Explique os custos deste mês', 'Qual categoria aumentou mais?', 'Estamos dentro do orçamento?'];
    case 'maintenance': return ['Quais problemas estão se repetindo na manutenção?', 'Qual veículo apresenta maior custo de manutenção?'];
    case 'fuel': return ['Existe algum consumo fora do padrão?', 'Qual veículo teve pior consumo este mês?'];
    case 'tires': return ['Quais pneus estão próximos de substituição?', 'Quais veículos possuem maior custo com pneus?'];
    case 'dashboard': return ['O que merece atenção agora?', 'Como está a frota? Algum resumo?'];
    default: return [];
  }
}

const OPEN_EVENT = 'fleet:assistant-open';

/** Opens the assistant panel from anywhere (contextual buttons), optionally asking a question right away. */
export function openAssistant(question?: string) {
  window.dispatchEvent(new CustomEvent(OPEN_EVENT, { detail: { question } }));
}

export function onAssistantOpen(handler: (question?: string) => void) {
  const listener = (e: Event) => handler((e as CustomEvent<{ question?: string }>).detail?.question);
  window.addEventListener(OPEN_EVENT, listener);
  return () => window.removeEventListener(OPEN_EVENT, listener);
}

export const assistantApi = {
  useStatus: (enabled: boolean) =>
    useQuery({ queryKey: ['assistant', 'status'], queryFn: () => api.get<{ aiEnabled: boolean; suggestions: string[] }>('/assistant/status').then((r) => r.data), enabled, staleTime: 300_000 }),
  useAsk: () =>
    useMutation({
      mutationFn: (body: { question: string; vehicleId: string | null; page: string | null }) =>
        api.post<AssistantAnswer>('/assistant/ask', body).then((r) => r.data),
    }),
};
