import { createContext, useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { configureApiClient } from '../api/client'
import { login as loginRequest } from '../api/auth'
import type { UsuarioLogado } from '../types/auth'

export interface AuthContextValue {
  usuario: UsuarioLogado | null
  token: string | null
  permissoes: string[]
  isAuthenticated: boolean
  login: (login: string, senha: string) => Promise<void>
  logout: () => void
}

// eslint-disable-next-line react-refresh/only-export-components
export const AuthContext = createContext<AuthContextValue | undefined>(undefined)

interface AuthProviderProps {
  children: ReactNode
}

/**
 * Holds auth state (token + logged in user) in memory only - no localStorage/sessionStorage.
 * A page reload always forces a fresh login, by design.
 */
export function AuthProvider({ children }: AuthProviderProps) {
  const [usuario, setUsuario] = useState<UsuarioLogado | null>(null)
  const [token, setToken] = useState<string | null>(null)
  // The api client interceptor needs synchronous access to the latest token, so
  // we mirror it into a ref rather than relying on the (possibly stale) closure state.
  const tokenRef = useRef<string | null>(null)
  const navigate = useNavigate()

  const logout = useCallback(() => {
    tokenRef.current = null
    setToken(null)
    setUsuario(null)
    navigate('/login', { replace: true })
  }, [navigate])

  useEffect(() => {
    configureApiClient(
      () => tokenRef.current,
      () => logout(),
    )
  }, [logout])

  const login = useCallback(async (loginValue: string, senha: string) => {
    const result = await loginRequest(loginValue, senha)
    tokenRef.current = result.token
    setToken(result.token)
    setUsuario(result.usuario)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      usuario,
      token,
      permissoes: usuario?.permissoes ?? [],
      isAuthenticated: token !== null,
      login,
      logout,
    }),
    [usuario, token, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
