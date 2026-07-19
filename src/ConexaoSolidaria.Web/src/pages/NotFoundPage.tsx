import { ArrowLeft } from 'lucide-react'
import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return <section className="section container state-block"><span className="not-found-code">404</span><h1>Página não encontrada</h1><p>O endereço pode ter mudado ou não existe.</p><Link className="button button--primary" to="/"><ArrowLeft size={18} aria-hidden="true" /> Voltar ao início</Link></section>
}
