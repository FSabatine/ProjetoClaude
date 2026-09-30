import type { StatusOption } from '../../components/common';
import { STATUS_COLOR } from '../../theme';

// Phase 2 enums → pt-BR labels and badge colors. The ONE place for these texts (DEVELOPMENT_GUIDELINES.md).

/** Derived status (ADR-018): stored condition + current driver. */
export const VEHICLE_OPERATIONAL_STATUS = {
  Available: { label: 'Disponível', color: STATUS_COLOR.positive },
  Assigned: { label: 'Alocado', color: 'cyan' },
  OnTrip: { label: 'Em viagem', color: STATUS_COLOR.active },
  Unavailable: { label: 'Indisponível', color: STATUS_COLOR.danger },
  UnderMaintenance: { label: 'Em manutenção', color: STATUS_COLOR.warning },
  Inactive: { label: 'Inativo', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type VehicleOperationalStatus = keyof typeof VEHICLE_OPERATIONAL_STATUS;

export const DOCUMENT_STATUS = {
  Valid: { label: 'Válido', color: STATUS_COLOR.positive },
  ExpiringSoon: { label: 'Vencendo', color: STATUS_COLOR.warning },
  Expired: { label: 'Vencido', color: STATUS_COLOR.danger },
  NoExpiration: { label: 'Sem validade', color: STATUS_COLOR.neutral },
  Replaced: { label: 'Substituído', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type DocumentStatus = keyof typeof DOCUMENT_STATUS;

export const DOCUMENT_OWNER_TYPE = {
  Vehicle: { label: 'Veículo' },
  Driver: { label: 'Motorista' },
  Implement: { label: 'Implemento' },
  Company: { label: 'Empresa' },
};
export type DocumentOwnerType = keyof typeof DOCUMENT_OWNER_TYPE;

export const OCCURRENCE_TYPE = {
  MechanicalIssue: { label: 'Problema mecânico' },
  TireProblem: { label: 'Pneu' },
  Accident: { label: 'Acidente' },
  VehicleDamage: { label: 'Avaria no veículo' },
  MissingEquipment: { label: 'Equipamento faltando' },
  DocumentationProblem: { label: 'Problema de documentação' },
  DriverReport: { label: 'Relato do motorista' },
  GeneralObservation: { label: 'Observação geral' },
};
export type OccurrenceType = keyof typeof OCCURRENCE_TYPE;

export const OCCURRENCE_SEVERITY = {
  Low: { label: 'Baixa', color: STATUS_COLOR.neutral },
  Medium: { label: 'Média', color: 'yellow' },
  High: { label: 'Alta', color: STATUS_COLOR.warning },
  Critical: { label: 'Crítica', color: STATUS_COLOR.danger },
} satisfies Record<string, StatusOption>;
export type OccurrenceSeverity = keyof typeof OCCURRENCE_SEVERITY;

export const OCCURRENCE_STATUS = {
  Open: { label: 'Aberta', color: STATUS_COLOR.danger },
  InAnalysis: { label: 'Em análise', color: STATUS_COLOR.active },
  Resolved: { label: 'Resolvida', color: STATUS_COLOR.positive },
  Cancelled: { label: 'Cancelada', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type OccurrenceStatus = keyof typeof OCCURRENCE_STATUS;

/** Verb shown on the button that moves an occurrence to each status. */
export const OCCURRENCE_ACTION: Record<OccurrenceStatus, string> = {
  Open: 'Reabrir',
  InAnalysis: 'Iniciar análise',
  Resolved: 'Resolver',
  Cancelled: 'Cancelar ocorrência',
};

export const CHECKLIST_FREQUENCY = {
  OnDemand: { label: 'Sob demanda' },
  Daily: { label: 'Diário' },
  Weekly: { label: 'Semanal' },
};
export type ChecklistFrequency = keyof typeof CHECKLIST_FREQUENCY;

export const CHECKLIST_RESPONSE_TYPE = {
  PassFail: { label: 'Conforme / Não conforme' },
  Number: { label: 'Número' },
  Text: { label: 'Texto' },
};
export type ChecklistResponseType = keyof typeof CHECKLIST_RESPONSE_TYPE;

export const CHECKLIST_CHOICE = {
  Pass: { label: 'Conforme', color: STATUS_COLOR.positive },
  Fail: { label: 'Não conforme', color: STATUS_COLOR.danger },
  NotApplicable: { label: 'Não se aplica', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type ChecklistChoice = keyof typeof CHECKLIST_CHOICE;

export const CHECKLIST_RESULT = {
  Approved: { label: 'Aprovado', color: STATUS_COLOR.positive },
  Failed: { label: 'Com não conformidades', color: STATUS_COLOR.danger },
} satisfies Record<string, StatusOption>;
export type ChecklistResult = keyof typeof CHECKLIST_RESULT;

export const ODOMETER_SOURCE = {
  Registration: { label: 'Cadastro' },
  Manual: { label: 'Registro manual' },
  Checklist: { label: 'Checklist' },
  Correction: { label: 'Correção' },
};
export type OdometerSource = keyof typeof ODOMETER_SOURCE;

export const ODOMETER_STATUS = {
  Valid: { label: 'Válida', color: STATUS_COLOR.positive },
  PendingReview: { label: 'Suspeita — em revisão', color: STATUS_COLOR.warning },
  Rejected: { label: 'Rejeitada', color: STATUS_COLOR.neutral },
} satisfies Record<string, StatusOption>;
export type OdometerStatus = keyof typeof ODOMETER_STATUS;
