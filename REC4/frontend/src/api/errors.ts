import axios from 'axios'
import type { ProblemDetails, ProblemDetailsErrorItem, ProblemDetailsFieldErrors } from '../types/common'

function isFieldErrorItemArray(errors: unknown): errors is ProblemDetailsErrorItem[] {
  return (
    Array.isArray(errors) &&
    errors.every((item) => typeof item === 'object' && item !== null && 'errorMessage' in item)
  )
}

function isFieldErrorMap(errors: unknown): errors is ProblemDetailsFieldErrors {
  return typeof errors === 'object' && errors !== null && !Array.isArray(errors)
}

/**
 * Extracts a human-readable message from a REC4 API error response.
 * The API returns problem+json: { title, status, errors? }, where `errors`
 * is either a field->messages map (ASP.NET ModelState style) or an array of
 * { propertyName, errorMessage } (FluentValidation style). Falls back to
 * `title`, then to the raw error message, then to a generic fallback.
 */
export function extractErrorMessage(error: unknown, fallback = 'Ocorreu um erro inesperado.'): string {
  if (!axios.isAxiosError(error)) {
    if (error instanceof Error && error.message) {
      return error.message
    }
    return fallback
  }

  const data = error.response?.data as ProblemDetails | undefined
  if (!data) {
    return error.message || fallback
  }

  const messages: string[] = []

  if (isFieldErrorItemArray(data.errors)) {
    for (const item of data.errors) {
      if (item.errorMessage) {
        messages.push(item.errorMessage)
      }
    }
  } else if (isFieldErrorMap(data.errors)) {
    for (const field of Object.keys(data.errors)) {
      const fieldMessages = data.errors[field]
      if (Array.isArray(fieldMessages)) {
        messages.push(...fieldMessages)
      }
    }
  }

  if (messages.length > 0) {
    return messages.join(' ')
  }

  if (data.title) {
    return data.title
  }

  return error.message || fallback
}
