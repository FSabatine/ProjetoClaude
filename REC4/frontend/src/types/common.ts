// Shared problem+json error shape returned across the whole REC4 API.
export interface ProblemDetailsFieldErrors {
  [field: string]: string[]
}

export interface ProblemDetailsErrorItem {
  propertyName: string
  errorMessage: string
}

export interface ProblemDetails {
  title: string
  status: number
  errors?: ProblemDetailsFieldErrors | ProblemDetailsErrorItem[] | null
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}
