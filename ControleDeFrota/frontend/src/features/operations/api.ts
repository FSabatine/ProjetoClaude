import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { ListParams, PagedResult } from '../../api/crud';
import type {
  ChecklistChoice,
  ChecklistFrequency,
  ChecklistResponseType,
  ChecklistResult,
  DocumentOwnerType,
  DocumentStatus,
  OccurrenceSeverity,
  OccurrenceStatus,
  OccurrenceType,
  OdometerSource,
  OdometerStatus,
} from './labels';

// ---------------------------------------------------------------- types (mirror of the API DTOs)

export interface FileInfo {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
}

export interface Assignment {
  id: string;
  vehicleId: string;
  licensePlate: string;
  vehicleDescription: string;
  driverId: string;
  driverName: string;
  startedAt: string;
  endedAt: string | null;
  isActive: boolean;
  notes: string | null;
  endReason: string | null;
  createdByName: string | null;
}

export interface OdometerReading {
  id: string;
  vehicleId: string;
  odometerKm: number;
  readAt: string;
  source: OdometerSource;
  status: OdometerStatus;
  anomaly: string | null;
  notes: string | null;
  recordedByName: string | null;
  createdAt: string;
  reviewedAt: string | null;
  reviewedByName: string | null;
  reviewNotes: string | null;
}

export interface DocumentType {
  id: string;
  name: string;
  ownerType: DocumentOwnerType;
  hasExpiration: boolean;
  alertDaysBefore: number;
  isActive: boolean;
  documentCount: number;
}

export interface DocumentListItem {
  id: string;
  documentTypeId: string;
  documentTypeName: string;
  ownerType: DocumentOwnerType;
  ownerId: string | null;
  ownerName: string | null;
  number: string | null;
  issuedOn: string | null;
  expiresOn: string | null;
  status: DocumentStatus;
  daysUntilExpiration: number | null;
  fileCount: number;
}

export interface FleetDocument extends Omit<DocumentListItem, 'fileCount'> {
  typeHasExpiration: boolean;
  notes: string | null;
  replacedAt: string | null;
  replacedByDocumentId: string | null;
  files: FileInfo[];
  createdAt: string;
  updatedAt: string | null;
}

export interface OccurrenceListItem {
  id: string;
  type: OccurrenceType;
  severity: OccurrenceSeverity;
  status: OccurrenceStatus;
  occurredAt: string;
  description: string;
  vehicleId: string | null;
  licensePlate: string | null;
  driverId: string | null;
  driverName: string | null;
  source: 'Manual' | 'Checklist';
}

export interface Occurrence extends OccurrenceListItem {
  nextStatuses: OccurrenceStatus[];
  location: string | null;
  implementId: string | null;
  implementPlate: string | null;
  resolution: string | null;
  closedAt: string | null;
  closedByName: string | null;
  checklistExecutionId: string | null;
  files: FileInfo[];
  createdByName: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface OccurrenceRequest {
  vehicleId: string | null;
  driverId: string | null;
  implementId: string | null;
  type: OccurrenceType | null;
  severity: OccurrenceSeverity | null;
  occurredAt: string | null;
  location: string | null;
  description: string;
  fileIds: string[];
}

export interface ChecklistTemplateItem {
  id: string;
  position: number;
  section: string | null;
  label: string;
  responseType: ChecklistResponseType;
  isRequired: boolean;
  unit: string | null;
  requiresPhotoOnFail: boolean;
  failureOccurrenceType: OccurrenceType;
  failureSeverity: OccurrenceSeverity;
}

export interface ChecklistTemplateListItem {
  id: string;
  name: string;
  description: string | null;
  frequency: ChecklistFrequency;
  isActive: boolean;
  version: number;
  itemCount: number;
  executionCount: number;
}

export interface ChecklistTemplate {
  id: string;
  name: string;
  description: string | null;
  frequency: ChecklistFrequency;
  isActive: boolean;
  version: number;
  items: ChecklistTemplateItem[];
}

export type ChecklistTemplateItemRequest = Omit<ChecklistTemplateItem, 'id' | 'position'> & { id: string | null };

export interface ChecklistTemplateRequest {
  name: string;
  description: string | null;
  frequency: ChecklistFrequency;
  isActive: boolean;
  items: ChecklistTemplateItemRequest[];
}

export interface ChecklistListItem {
  id: string;
  performedAt: string;
  vehicleId: string;
  licensePlate: string;
  driverId: string | null;
  driverName: string | null;
  templateId: string;
  templateName: string;
  templateVersion: number;
  result: ChecklistResult;
  failedItems: number;
  performedByName: string | null;
}

export interface ChecklistAnswer {
  id: string;
  position: number;
  section: string | null;
  label: string;
  responseType: ChecklistResponseType;
  isRequired: boolean;
  unit: string | null;
  choice: ChecklistChoice | null;
  numberValue: number | null;
  textValue: string | null;
  comment: string | null;
  severity: OccurrenceSeverity | null;
  occurrenceId: string | null;
  files: FileInfo[];
}

export interface ChecklistExecution extends ChecklistListItem {
  frequency: ChecklistFrequency;
  odometerKm: number | null;
  odometerStatus: OdometerStatus | null;
  location: string | null;
  notes: string | null;
  answers: ChecklistAnswer[];
}

export interface ChecklistAnswerRequest {
  templateItemId: string;
  choice: ChecklistChoice | null;
  numberValue: number | null;
  textValue: string | null;
  comment: string | null;
  severity: OccurrenceSeverity | null;
  fileIds: string[];
}

export interface ChecklistExecutionRequest {
  vehicleId: string;
  templateId: string;
  templateVersion: number;
  driverId: string | null;
  odometerKm: number | null;
  location: string | null;
  notes: string | null;
  answers: ChecklistAnswerRequest[];
}

export interface PendingChecklist {
  vehicleId: string;
  licensePlate: string;
  driverName: string | null;
  templateId: string;
  templateName: string;
  frequency: ChecklistFrequency;
  lastPerformedAt: string | null;
}

export interface HistoryEntry {
  id: number;
  type: string;
  occurredAt: string;
  summary: string;
  userName: string | null;
  subjectType: string;
  subjectId: string;
}

// ---------------------------------------------------------------- hooks

const clean = (params: ListParams) =>
  Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''));

const get = <T>(url: string, params?: ListParams) => api.get<T>(url, { params: params && clean(params) }).then((r) => r.data);

function usePaged<T>(key: unknown[], url: string, params: ListParams, enabled = true) {
  return useQuery({ queryKey: [...key, params], queryFn: () => get<PagedResult<T>>(url, params), placeholderData: keepPreviousData, enabled });
}

/**
 * Operational changes ripple across screens (vehicle header, dashboard, lists, history), so a successful mutation
 * refreshes every cached query. Cheap at this scale and never shows stale operational state.
 */
function useOperation<TBody, TResult>(fn: (body: TBody) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries() });
}

export const assignmentsApi = {
  useForVehicle: (vehicleId: string, params: ListParams = {}, enabled = true) =>
    usePaged<Assignment>(['assignments', 'vehicle', vehicleId], `/vehicles/${vehicleId}/assignments`, params, enabled),
  useForDriver: (driverId: string, params: ListParams = {}, enabled = true) =>
    usePaged<Assignment>(['assignments', 'driver', driverId], `/drivers/${driverId}/assignments`, params, enabled),
  useAssign: (vehicleId: string) =>
    useOperation((body: { driverId: string; startedAt: string | null; endCurrent: boolean; notes: string | null }) =>
      api.post<Assignment>(`/vehicles/${vehicleId}/assignments`, body).then((r) => r.data)),
  useEnd: () =>
    useOperation(({ id, ...body }: { id: string; endedAt: string | null; reason: string | null }) =>
      api.post<Assignment>(`/assignments/${id}/end`, body).then((r) => r.data)),
};

export const mileageApi = {
  useReadings: (vehicleId: string, params: ListParams = {}) =>
    usePaged<OdometerReading>(['mileage', vehicleId], `/vehicles/${vehicleId}/odometer-readings`, params),
  useRecord: (vehicleId: string) =>
    useOperation((body: { odometerKm: number; readAt: string | null; notes: string | null; isCorrection: boolean }) =>
      api.post<OdometerReading>(`/vehicles/${vehicleId}/odometer-readings`, body).then((r) => r.data)),
  useReview: () =>
    useOperation(({ id, approve, notes }: { id: string; approve: boolean; notes: string | null }) =>
      api.post<OdometerReading>(`/odometer-readings/${id}/${approve ? 'approve' : 'reject'}`, { notes }).then((r) => r.data)),
};

export const documentTypesApi = {
  useList: (ownerType?: DocumentOwnerType | null, includeInactive = false) =>
    useQuery({
      queryKey: ['document-types', ownerType ?? 'all', includeInactive],
      queryFn: () => get<DocumentType[]>('/document-types', { ownerType, includeInactive: includeInactive ? 'true' : undefined }),
    }),
  useSave: () =>
    useOperation(({ id, ...body }: { id?: string; name: string; ownerType: DocumentOwnerType; hasExpiration: boolean; alertDaysBefore: number; isActive: boolean }) =>
      (id ? api.put<DocumentType>(`/document-types/${id}`, body) : api.post<DocumentType>('/document-types', body)).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/document-types/${id}`).then(() => undefined)),
};

export const documentsApi = {
  useList: (params: ListParams, enabled = true) => usePaged<DocumentListItem>(['documents'], '/documents', params, enabled),
  useDetail: (id: string | null) =>
    useQuery({ queryKey: ['documents', 'detail', id], queryFn: () => get<FleetDocument>(`/documents/${id}`), enabled: !!id }),
  useCreate: () =>
    useOperation((body: {
      documentTypeId: string; ownerId: string | null; number: string | null; issuedOn: string | null; expiresOn: string | null;
      notes: string | null; fileIds: string[]; replacesDocumentId: string | null;
    }) => api.post<FleetDocument>('/documents', body).then((r) => r.data)),
  useUpdate: () =>
    useOperation(({ id, ...body }: { id: string; number: string | null; issuedOn: string | null; expiresOn: string | null; notes: string | null; fileIds: string[] }) =>
      api.put<FleetDocument>(`/documents/${id}`, body).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/documents/${id}`).then(() => undefined)),
};

export const filesApi = {
  upload: (file: Blob, fileName: string) => {
    const form = new FormData();
    form.append('file', file, fileName);
    return api.post<FileInfo>('/files', form).then((r) => r.data);
  },
  /** Files need the Bearer token, so they are fetched as blobs (an <img src> could not send it). */
  download: (id: string) => api.get<Blob>(`/files/${id}`, { responseType: 'blob' }).then((r) => r.data),
  useRemove: () => useOperation((id: string) => api.delete(`/files/${id}`).then(() => undefined)),
};

export const checklistTemplatesApi = {
  useList: (includeInactive = false) =>
    useQuery({
      queryKey: ['checklist-templates', includeInactive],
      queryFn: () => get<ChecklistTemplateListItem[]>('/checklist-templates', { includeInactive: includeInactive ? 'true' : undefined }),
    }),
  useDetail: (id: string | null | undefined) =>
    useQuery({ queryKey: ['checklist-templates', 'detail', id], queryFn: () => get<ChecklistTemplate>(`/checklist-templates/${id}`), enabled: !!id }),
  useSave: (id: string | undefined) =>
    useOperation((body: ChecklistTemplateRequest) =>
      (id ? api.put<ChecklistTemplate>(`/checklist-templates/${id}`, body) : api.post<ChecklistTemplate>('/checklist-templates', body)).then((r) => r.data)),
  useRemove: () => useOperation((id: string) => api.delete(`/checklist-templates/${id}`).then(() => undefined)),
};

export const checklistsApi = {
  useList: (params: ListParams, enabled = true) => usePaged<ChecklistListItem>(['checklists'], '/checklists', params, enabled),
  usePending: (enabled = true) => useQuery({ queryKey: ['checklists', 'pending'], queryFn: () => get<PendingChecklist[]>('/checklists/pending'), enabled }),
  useDetail: (id: string | undefined) =>
    useQuery({ queryKey: ['checklists', 'detail', id], queryFn: () => get<ChecklistExecution>(`/checklists/${id}`), enabled: !!id }),
  useSubmit: () => useOperation((body: ChecklistExecutionRequest) => api.post<ChecklistExecution>('/checklists', body).then((r) => r.data)),
};

export const occurrencesApi = {
  useList: (params: ListParams, enabled = true) => usePaged<OccurrenceListItem>(['occurrences'], '/occurrences', params, enabled),
  useDetail: (id: string | undefined) =>
    useQuery({ queryKey: ['occurrences', 'detail', id], queryFn: () => get<Occurrence>(`/occurrences/${id}`), enabled: !!id }),
  useSave: (id?: string) =>
    useOperation((body: OccurrenceRequest) =>
      (id ? api.put<Occurrence>(`/occurrences/${id}`, body) : api.post<Occurrence>('/occurrences', body)).then((r) => r.data)),
  useChangeStatus: (id: string) =>
    useOperation((body: { status: OccurrenceStatus; resolution: string | null }) =>
      api.post<Occurrence>(`/occurrences/${id}/status`, body).then((r) => r.data)),
};

export const historyApi = {
  useFor: (owner: 'vehicles' | 'drivers' | 'tires', id: string, params: ListParams) =>
    usePaged<HistoryEntry>(['history', owner, id], `/${owner}/${id}/history`, params),
};
