import { AxiosError } from 'axios';

/** Backend contract: RFC 9457 ProblemDetails with pt-BR title and optional field errors. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
  traceId?: string;
}

export interface ApiError {
  status: number;
  message: string;
  fieldErrors: Record<string, string>;
  traceId?: string;
}

const NETWORK_ERROR = 'Não foi possível conectar ao servidor. Verifique sua conexão com a internet e tente novamente.';
const GENERIC_ERROR = 'Não foi possível concluir a operação. Tente novamente em instantes.';

export function toApiError(error: unknown): ApiError {
  if (error instanceof AxiosError) {
    if (!error.response) return { status: 0, message: NETWORK_ERROR, fieldErrors: {} };

    const problem = (error.response.data ?? {}) as ProblemDetails;
    const fieldErrors = Object.fromEntries(Object.entries(problem.errors ?? {}).map(([field, messages]) => [field, messages[0]]));
    let message = problem.title ?? GENERIC_ERROR;
    // Never show a bare "500": give an actionable message plus the code support can search for.
    if (error.response.status >= 500 && problem.traceId) message = `${GENERIC_ERROR} Se o problema persistir, informe ao suporte o código ${problem.traceId}.`;
    return { status: error.response.status, message, fieldErrors, traceId: problem.traceId };
  }
  return { status: 0, message: GENERIC_ERROR, fieldErrors: {} };
}
