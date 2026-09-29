import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api, refreshAccessToken, setAccessToken, setSessionExpiredHandler } from '../api/client';
import type { Permission } from './permissions';

export interface UserProfile {
  id: string;
  name: string;
  email: string;
  companyId: string;
  companyName: string;
  roles: string[];
  permissions: string[];
}

interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: UserProfile;
}

type SessionStatus = 'loading' | 'authenticated' | 'anonymous';

interface AuthContextValue {
  status: SessionStatus;
  user: UserProfile | null;
  /** True when the session ended by itself (expired/revoked), so the login page can explain why. */
  sessionExpired: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  can: (permission: Permission) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<SessionStatus>('loading');
  const [user, setUser] = useState<UserProfile | null>(null);
  const [sessionExpired, setSessionExpired] = useState(false);

  const clearSession = useCallback(() => {
    setAccessToken(null);
    setUser(null);
    setStatus('anonymous');
    queryClient.clear();
  }, [queryClient]);

  // Restores the session after F5 through the HttpOnly refresh cookie (ADR-007).
  useEffect(() => {
    setSessionExpiredHandler(() => {
      setSessionExpired(true);
      clearSession();
    });
    let cancelled = false;
    refreshAccessToken().then(async (token) => {
      if (cancelled) return;
      if (!token) {
        setStatus('anonymous');
        return;
      }
      try {
        const { data } = await api.get<UserProfile>('/auth/me');
        if (!cancelled) {
          setUser(data);
          setStatus('authenticated');
        }
      } catch {
        if (!cancelled) clearSession();
      }
    });
    return () => {
      cancelled = true;
    };
  }, [clearSession]);

  const login = useCallback(async (email: string, password: string) => {
    const { data } = await api.post<LoginResponse>('/auth/login', { email, password });
    setAccessToken(data.accessToken);
    setUser(data.user);
    setSessionExpired(false);
    setStatus('authenticated');
  }, []);

  const logout = useCallback(async () => {
    try {
      await api.post('/auth/logout');
    } finally {
      setSessionExpired(false);
      clearSession();
    }
  }, [clearSession]);

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      user,
      sessionExpired,
      login,
      logout,
      can: (permission) => !!user?.permissions.includes(permission),
    }),
    [status, user, sessionExpired, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>');
  return context;
}

export const useCan = (permission: Permission) => useAuth().can(permission);
