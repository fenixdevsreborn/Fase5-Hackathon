import { useMemo, useState, type PropsWithChildren } from 'react'
import type { AuthSession } from '../types/api'
import { AuthContext } from './auth-context'

const storageKey = 'conexao-solidaria.session'

function readStoredSession(): AuthSession | null {
  const storedSession = sessionStorage.getItem(storageKey)
  if (!storedSession) return null

  try {
    const session = JSON.parse(storedSession) as AuthSession
    if (new Date(session.expiraEm) <= new Date()) {
      sessionStorage.removeItem(storageKey)
      return null
    }
    return session
  } catch {
    sessionStorage.removeItem(storageKey)
    return null
  }
}

export function AuthProvider({ children }: PropsWithChildren) {
  const [session, setSessionState] = useState<AuthSession | null>(readStoredSession)

  const value = useMemo(
    () => ({
      session,
      setSession(nextSession: AuthSession) {
        sessionStorage.setItem(storageKey, JSON.stringify(nextSession))
        setSessionState(nextSession)
      },
      logout() {
        sessionStorage.removeItem(storageKey)
        setSessionState(null)
      },
    }),
    [session],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
