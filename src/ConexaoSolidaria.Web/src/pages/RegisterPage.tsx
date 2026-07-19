import { Eye, EyeOff, RefreshCw, UserPlus } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { FeedbackBanner } from '../components/FeedbackBanner'
import { useAuth } from '../context/auth-context'
import { registerDonor } from '../services/auth-api'
import { ApiError } from '../services/http'

function formatCpf(value: string) {
  return value.replace(/\D/g, '').slice(0, 11).replace(/(\d{3})(\d)/, '$1.$2').replace(/(\d{3})(\d)/, '$1.$2').replace(/(\d{3})(\d{1,2})$/, '$1-$2')
}

export function RegisterPage() {
  const { session, setSession } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [cpf, setCpf] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (session) return <Navigate to={session.role === 'GestorONG' ? '/gestao' : '/campanhas'} replace />

  const from = (location.state as { from?: string } | null)?.from

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (password.length < 8) {
      setError('A senha deve conter pelo menos 8 caracteres.')
      return
    }

    setIsSubmitting(true)
    setError(null)
    try {
      const nextSession = await registerDonor({ nomeCompleto: name, email, cpf, senha: password })
      setSession(nextSession)
      navigate(from ?? '/campanhas', { replace: true })
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Não foi possível concluir o cadastro.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <section className="auth-page auth-page--register">
      <div className="auth-page__visual" aria-hidden="true" />
      <div className="auth-page__content">
        <form className="auth-form" onSubmit={handleSubmit} noValidate>
          <span className="eyebrow">Faça parte</span>
          <h1>Crie sua conta de doador</h1>
          <p>Seu cadastro permite registrar apoios às campanhas ativas.</p>
          {error && <FeedbackBanner tone="error" title="Revise seu cadastro" message={error} onDismiss={() => setError(null)} />}
          <div className="form-field"><label htmlFor="register-name">Nome completo</label><input id="register-name" autoComplete="name" value={name} onChange={(event) => setName(event.target.value)} required /></div>
          <div className="form-field"><label htmlFor="register-email">E-mail</label><input id="register-email" type="email" autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} required /></div>
          <div className="form-field"><label htmlFor="register-cpf">CPF</label><input id="register-cpf" inputMode="numeric" autoComplete="off" value={cpf} onChange={(event) => setCpf(formatCpf(event.target.value))} placeholder="000.000.000-00" required /></div>
          <div className="form-field"><label htmlFor="register-password">Senha</label><div className="password-field"><input id="register-password" type={showPassword ? 'text' : 'password'} autoComplete="new-password" minLength={8} value={password} onChange={(event) => setPassword(event.target.value)} required aria-describedby="password-help" /><button className="icon-button" type="button" onClick={() => setShowPassword((visible) => !visible)} aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'} title={showPassword ? 'Ocultar senha' : 'Mostrar senha'}>{showPassword ? <EyeOff aria-hidden="true" /> : <Eye aria-hidden="true" />}</button></div><small id="password-help">Use pelo menos 8 caracteres.</small></div>
          <button className="button button--primary button--wide" type="submit" disabled={isSubmitting}>{isSubmitting ? <><RefreshCw className="spin" size={18} aria-hidden="true" /> Criando conta...</> : <><UserPlus size={18} aria-hidden="true" /> Criar minha conta</>}</button>
          <p className="auth-form__switch">Já tem cadastro? <Link to="/entrar" state={{ from }}>Entrar</Link></p>
        </form>
      </div>
    </section>
  )
}
