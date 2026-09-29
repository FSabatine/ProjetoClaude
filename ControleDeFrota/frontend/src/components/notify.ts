import { notifications } from '@mantine/notifications';
import { toApiError, type ApiError } from '../api/errors';

export const notifySuccess = (message: string) =>
  notifications.show({ color: 'teal', title: 'Tudo certo', message, autoClose: 3500 });

/** Shows what happened and how to fix it (never a bare status code). Returns the parsed error for field mapping. */
export function notifyError(error: unknown, title = 'Não foi possível concluir'): ApiError {
  const apiError = toApiError(error);
  notifications.show({ color: 'red', title, message: apiError.message, autoClose: 7000 });
  return apiError;
}
