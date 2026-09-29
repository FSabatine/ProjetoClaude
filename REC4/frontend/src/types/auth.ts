export interface UsuarioLogado {
  id: string
  nome: string
  login: string
  grupo: string
  permissoes: string[]
}

export interface LoginResponse {
  token: string
  expiresAtUtc: string
  usuario: UsuarioLogado
}

export interface MeResponse {
  id: string
  nome: string
  login: string
  grupo: string
  isActive: boolean
  permissoes: string[]
}
