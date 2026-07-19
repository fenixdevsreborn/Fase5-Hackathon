import { Outlet } from 'react-router-dom'
import { AppFooter } from './AppFooter'
import { AppHeader } from './AppHeader'

export function AppLayout() {
  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">Pular para o conteúdo</a>
      <AppHeader />
      <main id="main-content"><Outlet /></main>
      <AppFooter />
    </div>
  )
}
