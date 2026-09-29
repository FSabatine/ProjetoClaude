import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';

/**
 * ADR-007: the access token lives only in memory (never localStorage/sessionStorage).
 * The refresh token is an HttpOnly cookie the browser sends to /api/v1/auth only.
 */
let accessToken: string | null = null;
let onSessionExpired: (() => void) | null = null;
let refreshInFlight: Promise<string | null> | null = null;

export const setAccessToken = (token: string | null) => {
  accessToken = token;
};

export const setSessionExpiredHandler = (handler: () => void) => {
  onSessionExpired = handler;
};

export const api = axios.create({ baseURL: '/api/v1', withCredentials: true });

api.interceptors.request.use((config) => {
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`;
  return config;
});

interface LoginPayload {
  accessToken: string;
}

/** Single-flight refresh: concurrent 401s share one /auth/refresh call. */
export function refreshAccessToken(): Promise<string | null> {
  refreshInFlight ??= axios
    .post<LoginPayload>('/api/v1/auth/refresh', null, { withCredentials: true })
    .then((response) => {
      setAccessToken(response.data.accessToken);
      return response.data.accessToken;
    })
    .catch(() => {
      setAccessToken(null);
      return null;
    })
    .finally(() => {
      refreshInFlight = null;
    });
  return refreshInFlight;
}

type RetriableConfig = InternalAxiosRequestConfig & { _retried?: boolean };

api.interceptors.response.use(undefined, async (error: AxiosError) => {
  const original = error.config as RetriableConfig | undefined;
  const isAuthCall = original?.url?.startsWith('/auth/');
  if (error.response?.status !== 401 || !original || original._retried || isAuthCall) throw error;

  original._retried = true;
  const token = await refreshAccessToken();
  if (!token) {
    onSessionExpired?.();
    throw error;
  }
  original.headers.Authorization = `Bearer ${token}`;
  return api(original);
});
