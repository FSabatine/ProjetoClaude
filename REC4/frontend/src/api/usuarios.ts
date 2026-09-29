import apiClient from './client'
import type {
  GrupoCreateDto,
  GrupoDetail,
  GrupoDto,
  GrupoUpdateDto,
  PermissaoDto,
  UsuarioCreateDto,
  UsuarioDetail,
  UsuarioEmitenteCreateDto,
  UsuarioEscritorioCreateDto,
  UsuarioListItem,
  UsuarioPermissoesUpdateDto,
  UsuarioUpdateDto,
} from '../types/usuarios'

export async function listUsuarios(): Promise<UsuarioListItem[]> {
  const response = await apiClient.get<UsuarioListItem[]>('/usuarios')
  return response.data
}

export async function getUsuario(id: string): Promise<UsuarioDetail> {
  const response = await apiClient.get<UsuarioDetail>(`/usuarios/${id}`)
  return response.data
}

export async function createUsuario(dto: UsuarioCreateDto): Promise<UsuarioDetail> {
  const response = await apiClient.post<UsuarioDetail>('/usuarios', dto)
  return response.data
}

export async function updateUsuario(id: string, dto: UsuarioUpdateDto): Promise<UsuarioDetail> {
  const response = await apiClient.put<UsuarioDetail>(`/usuarios/${id}`, dto)
  return response.data
}

export async function setUsuarioPermissoes(id: string, dto: UsuarioPermissoesUpdateDto): Promise<UsuarioDetail> {
  const response = await apiClient.put<UsuarioDetail>(`/usuarios/${id}/permissoes`, dto)
  return response.data
}

export async function addUsuarioEscritorio(id: string, dto: UsuarioEscritorioCreateDto): Promise<UsuarioDetail> {
  const response = await apiClient.post<UsuarioDetail>(`/usuarios/${id}/escritorios`, dto)
  return response.data
}

export async function setUsuarioEscritorioPrincipal(id: string, escritorioId: string): Promise<UsuarioDetail> {
  const response = await apiClient.put<UsuarioDetail>(`/usuarios/${id}/escritorios/${escritorioId}/principal`)
  return response.data
}

export async function removeUsuarioEscritorio(id: string, escritorioId: string): Promise<UsuarioDetail> {
  const response = await apiClient.delete<UsuarioDetail>(`/usuarios/${id}/escritorios/${escritorioId}`)
  return response.data
}

export async function addUsuarioEmitente(id: string, dto: UsuarioEmitenteCreateDto): Promise<UsuarioDetail> {
  const response = await apiClient.post<UsuarioDetail>(`/usuarios/${id}/emitentes`, dto)
  return response.data
}

export async function removeUsuarioEmitente(id: string, pessoaId: string): Promise<UsuarioDetail> {
  const response = await apiClient.delete<UsuarioDetail>(`/usuarios/${id}/emitentes/${pessoaId}`)
  return response.data
}

export async function listGrupos(): Promise<GrupoDto[]> {
  const response = await apiClient.get<GrupoDto[]>('/grupos')
  return response.data
}

export async function getGrupo(id: number): Promise<GrupoDetail> {
  const response = await apiClient.get<GrupoDetail>(`/grupos/${id}`)
  return response.data
}

export async function createGrupo(dto: GrupoCreateDto): Promise<GrupoDetail> {
  const response = await apiClient.post<GrupoDetail>('/grupos', dto)
  return response.data
}

export async function updateGrupo(id: number, dto: GrupoUpdateDto): Promise<GrupoDetail> {
  const response = await apiClient.put<GrupoDetail>(`/grupos/${id}`, dto)
  return response.data
}

export async function listPermissoes(): Promise<PermissaoDto[]> {
  const response = await apiClient.get<PermissaoDto[]>('/permissoes')
  return response.data
}
