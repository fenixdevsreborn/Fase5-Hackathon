import { ArrowLeft, CheckCircle2, Heart, LockKeyhole, RefreshCw } from 'lucide-react'
import { type FormEvent, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { FeedbackBanner } from '../components/FeedbackBanner'
import { ProgressBar } from '../components/ProgressBar'
import { useAuth } from '../context/auth-context'
import { createDonation, getPublicCampaign } from '../services/campaigns-api'
import { ApiError } from '../services/http'
import type { DonationAccepted, PublicCampaign } from '../types/api'
import { formatCurrency } from '../utils/format'

const suggestedAmounts = [25, 50, 100, 200]

export function DonatePage() {
  const { id = '' } = useParams()
  const { session } = useAuth()
  const [campaign, setCampaign] = useState<PublicCampaign | null>(null)
  const [amount, setAmount] = useState('50')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [acceptedDonation, setAcceptedDonation] = useState<DonationAccepted | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    getPublicCampaign(id, controller.signal)
      .then((result) => {
        setCampaign(result)
        if (!result) setError('Esta campanha não está disponível para doações.')
      })
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof Error ? requestError.message : 'Não foi possível carregar a campanha.')
      })
      .finally(() => { if (!controller.signal.aborted) setIsLoading(false) })
    return () => controller.abort()
  }, [id])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!session || !campaign) return

    const parsedAmount = Number(amount.replace(',', '.'))
    if (!Number.isFinite(parsedAmount) || parsedAmount <= 0) {
      setError('Informe um valor de doação maior que zero.')
      return
    }

    setIsSubmitting(true)
    setError(null)
    try {
      setAcceptedDonation(await createDonation(campaign.id, parsedAmount, session.accessToken))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Não foi possível registrar a doação.')
    } finally {
      setIsSubmitting(false)
    }
  }

  if (isLoading) return <div className="page-loading" aria-live="polite"><RefreshCw className="spin" aria-hidden="true" /> Preparando sua doação...</div>
  if (!campaign) return <section className="section container"><FeedbackBanner tone="error" title="Campanha indisponível" message={error ?? undefined} /><Link className="button button--secondary" to="/campanhas">Ver outras campanhas</Link></section>

  if (acceptedDonation) {
    return (
      <section className="section container completion-state" aria-labelledby="donation-success-title">
        <CheckCircle2 size={54} aria-hidden="true" />
        <span className="eyebrow">Doação recebida</span>
        <h1 id="donation-success-title">Seu apoio está sendo processado</h1>
        <p>A intenção de {formatCurrency(acceptedDonation.valorDoacao)} para <strong>{campaign.titulo}</strong> foi enviada com sucesso. O total público será atualizado pelo processamento assíncrono.</p>
        <dl className="receipt-details"><div><dt>Identificador</dt><dd>{acceptedDonation.doacaoId}</dd></div><div><dt>Status</dt><dd>{acceptedDonation.status}</dd></div></dl>
        <div className="button-row"><Link className="button button--primary" to={`/campanhas/${campaign.id}`}>Voltar à campanha</Link><Link className="button button--secondary" to="/campanhas">Ver outras campanhas</Link></div>
      </section>
    )
  }

  return (
    <section className="section donation-flow">
      <div className="container donation-flow__container">
        <div className="donation-flow__summary">
          <Link className="back-link" to={`/campanhas/${campaign.id}`}><ArrowLeft size={17} aria-hidden="true" /> Voltar à campanha</Link>
          <img src="/images/hero-solidariedade.webp" alt="Voluntários preparando itens destinados às ações solidárias" />
          <span className="eyebrow">Você está apoiando</span>
          <h1>{campaign.titulo}</h1>
          <ProgressBar campaign={campaign} compact />
          <p><strong>{formatCurrency(campaign.valorTotalArrecadado)}</strong> de {formatCurrency(campaign.metaFinanceira)} arrecadados.</p>
        </div>

        <form className="form-panel" onSubmit={handleSubmit} noValidate>
          <div className="form-panel__heading"><Heart aria-hidden="true" /><div><h2>Escolha o valor</h2><p>Qualquer quantia faz parte desta transformação.</p></div></div>
          {error && <FeedbackBanner tone="error" title="Revise sua doação" message={error} onDismiss={() => setError(null)} />}
          <fieldset className="amount-options">
            <legend>Valores sugeridos</legend>
            {suggestedAmounts.map((suggestedAmount) => (
              <label key={suggestedAmount} className={amount === String(suggestedAmount) ? 'amount-option amount-option--selected' : 'amount-option'}>
                <input type="radio" name="suggestedAmount" value={suggestedAmount} checked={amount === String(suggestedAmount)} onChange={(event) => setAmount(event.target.value)} />
                {formatCurrency(suggestedAmount)}
              </label>
            ))}
          </fieldset>
          <div className="form-field">
            <label htmlFor="donation-amount">Outro valor</label>
            <div className="currency-input"><span>R$</span><input id="donation-amount" type="number" min="0.01" step="0.01" inputMode="decimal" value={amount} onChange={(event) => setAmount(event.target.value)} required aria-describedby="donation-help" /></div>
            <small id="donation-help">Informe um valor maior que zero.</small>
          </div>
          <div className="security-note"><LockKeyhole size={20} aria-hidden="true" /><p><strong>Etapa transparente:</strong> esta ação registra uma intenção de doação. Nenhum pagamento é cobrado nesta plataforma.</p></div>
          <button className="button button--accent button--wide" type="submit" disabled={isSubmitting}>{isSubmitting ? <><RefreshCw className="spin" size={18} aria-hidden="true" /> Enviando...</> : <><Heart size={18} aria-hidden="true" /> Confirmar intenção de doação</>}</button>
        </form>
      </div>
    </section>
  )
}
