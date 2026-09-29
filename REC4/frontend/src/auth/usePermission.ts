import { useAuth } from './useAuth'

/**
 * UX convenience only - the real security boundary is the backend.
 * Checks the permission claims already present on the logged in user
 * (`usuario.permissoes`, e.g. "Pessoa.Visualizar").
 */
export function usePermission(chave: string): boolean {
  const { permissoes } = useAuth()
  return permissoes.includes(chave)
}
