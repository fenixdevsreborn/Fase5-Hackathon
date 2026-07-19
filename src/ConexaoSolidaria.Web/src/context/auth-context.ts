import { createContext, useContext } from 'react'
import type { AuthSession } from '../types/api'

export interface AuthContextValue {
  session: AuthSession | null
  setSession: (session: AuthSession) => void
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth deve ser usado dentro de AuthProvider.')
  return context
}
