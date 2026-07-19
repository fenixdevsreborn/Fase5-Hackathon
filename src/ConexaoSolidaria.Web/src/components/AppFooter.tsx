import { HeartHandshake, Mail, MapPin } from 'lucide-react'
import { Link } from 'react-router-dom'

export function AppFooter() {
  return (
    <footer className="site-footer">
      <div className="container site-footer__grid">
        <div>
          <Link className="brand brand--footer" to="/"><HeartHandshake size={24} aria-hidden="true" /> Conexão <strong>Solidária</strong></Link>
          <p>Uma iniciativa digital da ONG Esperança Solidária para aproximar pessoas, campanhas e impacto.</p>
        </div>
        <div>
          <h2>Navegue</h2>
          <Link to="/campanhas">Campanhas ativas</Link>
          <Link to="/cadastro">Seja um doador</Link>
          <Link to="/entrar">Área de acesso</Link>
        </div>
        <div>
          <h2>Esperança Solidária</h2>
          <span><MapPin size={17} aria-hidden="true" /> Brasil</span>
          <span><Mail size={17} aria-hidden="true" /> Atendimento pela plataforma</span>
        </div>
      </div>
      <div className="container site-footer__bottom">© {new Date().getFullYear()} Conexão Solidária. Transparência que aproxima.</div>
    </footer>
  )
}
