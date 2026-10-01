import type { HelpAnalyticsEvent } from './types';

/**
 * Ponto de extensão (seção 40 do pedido): quando houver um destino real (produto, BI), este é o único
 * lugar a trocar — nenhum componente da Central de Ajuda precisa mudar. Por enquanto só loga em
 * desenvolvimento (sem coleta de dado pessoal, sem plataforma de analytics ainda).
 */
export function trackHelpEvent(event: HelpAnalyticsEvent): void {
  // eslint-disable-next-line no-console -- stub só para desenvolvimento, até existir um destino real
  if (import.meta.env.DEV) console.debug('[help]', event);
}
