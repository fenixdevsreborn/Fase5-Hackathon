import type { AuthSession, LoginInput, RegisterInput } from '../types/api'
import { apiRequest } from './http'

const identityApiUrl = import.meta.env.VITE_IDENTITY_API_URL ?? '/identity-api'

export function login(input: LoginInput, signal?: AbortSignal) {
  return apiRequest<AuthSession>(`${identityApiUrl}/api/auth/login`, {
    method: 'POST',
    body: JSON.stringify(input),
    signal,
  })
}

export function registerDonor(input: RegisterInput, signal?: AbortSignal) {
  return apiRequest<AuthSession>(`${identityApiUrl}/api/auth/cadastro-doador`, {
    method: 'POST',
    body: JSON.stringify(input),
    signal,
  })
}
