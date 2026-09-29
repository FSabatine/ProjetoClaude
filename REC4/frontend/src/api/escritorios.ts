import apiClient from './client'
import type { EscritorioCreateDto, EscritorioDto, EscritorioUpdateDto } from '../types/escritorios'

export async function listEscritorios(): Promise<EscritorioDto[]> {
  const response = await apiClient.get<EscritorioDto[]>('/escritorios')
  return response.data
}

export async function getEscritorio(id: string): Promise<EscritorioDto> {
  const response = await apiClient.get<EscritorioDto>(`/escritorios/${id}`)
  return response.data
}

export async function createEscritorio(dto: EscritorioCreateDto): Promise<EscritorioDto> {
  const response = await apiClient.post<EscritorioDto>('/escritorios', dto)
  return response.data
}

export async function updateEscritorio(id: string, dto: EscritorioUpdateDto): Promise<EscritorioDto> {
  const response = await apiClient.put<EscritorioDto>(`/escritorios/${id}`, dto)
  return response.data
}
