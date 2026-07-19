import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../context/auth-context'
import type { UserRole } from '../types/api'

export function ProtectedRoute({ role }: { role: UserRole }) {
  const { session } = useAuth()
  const location = useLocation()

  if (!session) {
    return <Navigate to="/entrar" replace state={{ from: `${location.pathname}${location.search}` }} />
  }

  if (session.role !== role) {
    return <Navigate to={session.role === 'GestorONG' ? '/gestao' : '/campanhas'} replace />
  }

  return <Outlet />
}
