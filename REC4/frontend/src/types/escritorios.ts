export interface EscritorioDto {
  id: string
  nome: string
  isActive: boolean
}

export interface EscritorioCreateDto {
  nome: string
}

export interface EscritorioUpdateDto {
  nome: string
  isActive: boolean
}
