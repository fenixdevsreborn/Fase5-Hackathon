import type { ApiProblem } from '../types/api'

export class ApiError extends Error {
  readonly status: number
  readonly fieldErrors: Record<string, string[]>

  constructor(message: string, status: number, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

interface ApiRequestOptions extends RequestInit {
  token?: string
}

export async function apiRequest<T>(url: string, options: ApiRequestOptions = {}): Promise<T> {
  const { token, headers, ...requestOptions } = options
  const response = await fetch(url, {
    ...requestOptions,
    headers: {
      Accept: 'application/json',
      ...(requestOptions.body ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...headers,
    },
  })

  if (!response.ok) {
    let problem: ApiProblem = {}

    try {
      problem = (await response.json()) as ApiProblem
    } catch {
      // Some infrastructure errors do not include a JSON body.
    }

    const message =
      problem.mensagem ??
      problem.title ??
      (response.status >= 500
        ? 'O serviço está temporariamente indisponível. Tente novamente em instantes.'
        : 'Não foi possível concluir a solicitação.')

    throw new ApiError(message, response.status, problem.errors)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}
