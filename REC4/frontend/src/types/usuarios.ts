export type TipoUsuario = 0 | 1 | 2 // 0 = Administrador, 1 = Gestor, 2 = Operador

export interface UsuarioListItem {
  id: string
  nome: string
  login: string
  grupoNome: string
  isActive: boolean
}

export interface UsuarioEscritorioDto {
  id: string
  nome: string
  isPrincipal: boolean
}

export interface UsuarioEmitenteDto {
  pessoaId: string
  nome: string
}

export interface UsuarioDetail {
  id: string
  nome: string
  login: string
  grupoId: number
  grupoNome: string
  isActive: boolean
  pessoaId: string | null
  pessoaNome: string | null
  tipoUsuario: TipoUsuario
  limiteDiasEdicaoFinanceiro: number | null
  temPermissaoPersonalizada: boolean
  permissoesEfetivas: string[]
  escritorios: UsuarioEscritorioDto[]
  emitentes: UsuarioEmitenteDto[]
  createdAt: string
  updatedAt: string
}

export interface UsuarioCreateDto {
  nome: string
  login: string
  senha: string
  grupoId: number
  pessoaId: string | null
  tipoUsuario: TipoUsuario
  limiteDiasEdicaoFinanceiro: number | null
}

export interface UsuarioUpdateDto {
  nome: string
  grupoId: number
  isActive: boolean
  pessoaId: string | null
  tipoUsuario: TipoUsuario
  limiteDiasEdicaoFinanceiro: number | null
}

export interface UsuarioPermissoesUpdateDto {
  personalizado: boolean
  permissaoIds: number[]
}

export interface UsuarioEscritorioCreateDto {
  escritorioId: string
  isPrincipal: boolean
}

export interface UsuarioEmitenteCreateDto {
  pessoaId: string
}

export interface GrupoDto {
  id: number
  nome: string
}

export interface GrupoDetail {
  id: number
  nome: string
  permissoes: PermissaoDto[]
}

export interface GrupoCreateDto {
  nome: string
  permissaoIds: number[]
}

export interface GrupoUpdateDto {
  nome: string
  permissaoIds: number[]
}

export interface PermissaoDto {
  id: number
  chave: string
  descricao: string | null
}
