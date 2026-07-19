import { HeartHandshake, LogOut, Menu, UserRound, X } from 'lucide-react'
import { useState } from 'react'
import { Link, NavLink, useLocation } from 'react-router-dom'
import { useAuth } from '../context/auth-context'

export function AppHeader() {
  const [isMenuOpen, setIsMenuOpen] = useState(false)
  const { session, logout } = useAuth()
  const location = useLocation()

  const closeMenu = () => setIsMenuOpen(false)

  return (
    <header className="site-header">
      <div className="container site-header__inner">
        <Link className="brand" to="/" onClick={closeMenu} aria-label="Conexão Solidária, página inicial">
          <span className="brand__mark"><HeartHandshake size={24} aria-hidden="true" /></span>
          <span>Conexão <strong>Solidária</strong></span>
        </Link>

        <button
          className="icon-button menu-toggle"
          type="button"
          aria-expanded={isMenuOpen}
          aria-controls="main-navigation"
          aria-label={isMenuOpen ? 'Fechar menu' : 'Abrir menu'}
          title={isMenuOpen ? 'Fechar menu' : 'Abrir menu'}
          onClick={() => setIsMenuOpen((open) => !open)}
        >
          {isMenuOpen ? <X aria-hidden="true" /> : <Menu aria-hidden="true" />}
        </button>

        <nav id="main-navigation" className={isMenuOpen ? 'main-nav main-nav--open' : 'main-nav'} aria-label="Navegação principal">
          <NavLink to="/" end onClick={closeMenu}>Início</NavLink>
          <NavLink to="/campanhas" onClick={closeMenu}>Campanhas</NavLink>
          {session?.role === 'GestorONG' && <NavLink to="/gestao" onClick={closeMenu}>Gestão</NavLink>}
          <div className="main-nav__account">
            {session ? (
              <>
                <span className="user-summary"><UserRound size={17} aria-hidden="true" /> {session.nomeCompleto.split(' ')[0]}</span>
                <button className="button button--ghost button--small" type="button" onClick={() => { logout(); closeMenu() }}>
                  <LogOut size={17} aria-hidden="true" /> Sair
                </button>
              </>
            ) : (
              <>
                <Link className="button button--ghost button--small" to="/entrar" state={{ from: location.pathname }} onClick={closeMenu}>Entrar</Link>
                <Link className="button button--primary button--small" to="/cadastro" onClick={closeMenu}>Criar conta</Link>
              </>
            )}
          </div>
        </nav>
      </div>
    </header>
  )
}
