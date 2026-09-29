import apiClient from './client'
import type { PagedResult } from '../types/common'
import type {
  ContaBancariaCreateDto,
  ContaBancariaDto,
  EnderecoCreateDto,
  EnderecoDto,
  PerfilDto,
  PessoaCreateDto,
  PessoaDto,
  PessoaUpdateDto,
} from '../types/pessoas'

export async function listPessoas(page: number, pageSize: number): Promise<PagedResult<PessoaDto>> {
  const response = await apiClient.get<PagedResult<PessoaDto>>('/pessoas', { params: { page, pageSize } })
  return response.data
}

// Usado para popular dropdowns (ex.: "Vínculo Pessoa" filtrado a Colaboradores, busca de Emitentes)
// - pageSize alto porque o dropdown precisa da lista inteira, não paginada.
export async function listPessoasPorPerfil(perfilId: number): Promise<PessoaDto[]> {
  const response = await apiClient.get<PagedResult<PessoaDto>>('/pessoas', { params: { page: 1, pageSize: 200, perfilId } })
  return response.data.items
}

export async function getPessoa(id: string): Promise<PessoaDto> {
  const response = await apiClient.get<PessoaDto>(`/pessoas/${id}`)
  return response.data
}

export async function createPessoa(dto: PessoaCreateDto): Promise<PessoaDto> {
  const response = await apiClient.post<PessoaDto>('/pessoas', dto)
  return response.data
}

export async function updatePessoa(id: string, dto: PessoaUpdateDto): Promise<PessoaDto> {
  const response = await apiClient.put<PessoaDto>(`/pessoas/${id}`, dto)
  return response.data
}

export async function softDeletePessoa(id: string): Promise<void> {
  await apiClient.delete(`/pessoas/${id}`)
}

export async function setPerfis(id: string, perfilIds: number[]): Promise<PessoaDto> {
  const response = await apiClient.put<PessoaDto>(`/pessoas/${id}/perfis`, perfilIds)
  return response.data
}

export async function listPerfis(): Promise<PerfilDto[]> {
  const response = await apiClient.get<PerfilDto[]>('/perfis')
  return response.data
}

export async function addEndereco(id: string, dto: EnderecoCreateDto): Promise<EnderecoDto> {
  const response = await apiClient.post<EnderecoDto>(`/pessoas/${id}/enderecos`, dto)
  return response.data
}

export async function setEnderecoPrincipal(id: string, enderecoId: string): Promise<void> {
  await apiClient.put(`/pessoas/${id}/enderecos/${enderecoId}/principal`)
}

export async function addContaBancaria(id: string, dto: ContaBancariaCreateDto): Promise<ContaBancariaDto> {
  const response = await apiClient.post<ContaBancariaDto>(`/pessoas/${id}/contas-bancarias`, dto)
  return response.data
}

export async function setContaPrincipal(id: string, contaId: string): Promise<void> {
  await apiClient.put(`/pessoas/${id}/contas-bancarias/${contaId}/principal`)
}
