import { Eye, EyeOff, LogIn, RefreshCw } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { FeedbackBanner } from '../components/FeedbackBanner'
import { useAuth } from '../context/auth-context'
import { login } from '../services/auth-api'
import { ApiError } from '../services/http'

export function LoginPage() {
  const { session, setSession } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (session) return <Navigate to={session.role === 'GestorONG' ? '/gestao' : '/campanhas'} replace />

  const from = (location.state as { from?: string } | null)?.from

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setIsSubmitting(true)
    setError(null)
    try {
      const nextSession = await login({ email, senha: password })
      setSession(nextSession)
      navigate(from ?? (nextSession.role === 'GestorONG' ? '/gestao' : '/campanhas'), { replace: true })
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Não foi possível entrar.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <section className="auth-page">
      <div className="auth-page__visual" aria-hidden="true" />
      <div className="auth-page__content">
        <form className="auth-form" onSubmit={handleSubmit} noValidate>
          <span className="eyebrow">Boas-vindas</span>
          <h1>Entre na sua conta</h1>
          <p>Acesse como doador ou gestor da organização.</p>
          {from?.includes('/doar') && <FeedbackBanner tone="info" title="Entre para continuar sua doação." />}
          {error && <FeedbackBanner tone="error" title="Não foi possível entrar" message={error} onDismiss={() => setError(null)} />}
          <div className="form-field"><label htmlFor="login-email">E-mail</label><input id="login-email" type="email" autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} required /></div>
          <div className="form-field"><label htmlFor="login-password">Senha</label><div className="password-field"><input id="login-password" type={showPassword ? 'text' : 'password'} autoComplete="current-password" value={password} onChange={(event) => setPassword(event.target.value)} required /><button className="icon-button" type="button" onClick={() => setShowPassword((visible) => !visible)} aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'} title={showPassword ? 'Ocultar senha' : 'Mostrar senha'}>{showPassword ? <EyeOff aria-hidden="true" /> : <Eye aria-hidden="true" />}</button></div></div>
          <button className="button button--primary button--wide" type="submit" disabled={isSubmitting}>{isSubmitting ? <><RefreshCw className="spin" size={18} aria-hidden="true" /> Entrando...</> : <><LogIn size={18} aria-hidden="true" /> Entrar</>}</button>
          <p className="auth-form__switch">Ainda não tem conta? <Link to="/cadastro" state={{ from }}>Cadastre-se como doador</Link></p>
        </form>
      </div>
    </section>
  )
}
