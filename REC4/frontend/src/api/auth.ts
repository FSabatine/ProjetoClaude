import apiClient from './client'
import type { LoginResponse, MeResponse } from '../types/auth'

export async function login(loginValue: string, senha: string): Promise<LoginResponse> {
  const response = await apiClient.post<LoginResponse>('/auth/login', { login: loginValue, senha })
  return response.data
}

export async function getMe(): Promise<MeResponse> {
  const response = await apiClient.get<MeResponse>('/usuarios/me')
  return response.data
}
