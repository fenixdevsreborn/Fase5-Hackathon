import { ArrowLeft, CalendarDays, Heart, RefreshCw, Share2, Timer } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ErrorState } from '../components/AsyncStates'
import { FeedbackBanner } from '../components/FeedbackBanner'
import { ProgressBar } from '../components/ProgressBar'
import { getPublicCampaign } from '../services/campaigns-api'
import type { PublicCampaign } from '../types/api'
import { getDaysRemaining, getRemainingAmount } from '../utils/campaign'
import { formatCurrency, formatDate } from '../utils/format'

export function CampaignDetailPage() {
  const { id = '' } = useParams()
  const [campaign, setCampaign] = useState<PublicCampaign | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [shared, setShared] = useState(false)
  const [requestVersion, setRequestVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    queueMicrotask(() => {
      if (!controller.signal.aborted) {
        setIsLoading(true)
        setError(null)
      }
    })
    getPublicCampaign(id, controller.signal)
      .then((result) => {
        setCampaign(result)
        if (!result) setError('Esta campanha não está ativa ou não foi encontrada.')
      })
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof Error ? requestError.message : 'Não foi possível carregar a campanha.')
      })
      .finally(() => { if (!controller.signal.aborted) setIsLoading(false) })
    return () => controller.abort()
  }, [id, requestVersion])

  async function shareCampaign() {
    const shareData = { title: campaign?.titulo, text: campaign?.descricao, url: window.location.href }
    if (navigator.share) await navigator.share(shareData)
    else await navigator.clipboard.writeText(window.location.href)
    setShared(true)
  }

  if (isLoading) return <div className="page-loading" aria-live="polite"><RefreshCw className="spin" aria-hidden="true" /> Carregando campanha...</div>
  if (error || !campaign) return <section className="section container"><ErrorState message={error ?? 'Campanha não encontrada.'} onRetry={() => setRequestVersion((version) => version + 1)} /></section>

  const remainingAmount = getRemainingAmount(campaign)
  const daysRemaining = getDaysRemaining(campaign.dataFim)

  return (
    <>
      <section className="campaign-detail">
        <div className="container">
          <Link className="back-link" to="/campanhas"><ArrowLeft size={17} aria-hidden="true" /> Voltar para campanhas</Link>
          {shared && <FeedbackBanner tone="success" title="Link da campanha pronto para compartilhar." onDismiss={() => setShared(false)} />}
          <div className="campaign-detail__grid">
            <div className="campaign-detail__story">
              <img src="/images/hero-solidariedade.webp" alt="Equipe de voluntários organizando alimentos e materiais para doação" />
              <span className="eyebrow">Campanha ativa</span>
              <h1>{campaign.titulo}</h1>
              <p className="campaign-detail__description">{campaign.descricao}</p>
              <div className="campaign-dates">
                <span><CalendarDays aria-hidden="true" /> Início em {formatDate(campaign.dataInicio)}</span>
                <span><Timer aria-hidden="true" /> Encerra em {formatDate(campaign.dataFim)}</span>
              </div>
            </div>
            <aside className="donation-panel" aria-label="Resumo financeiro da campanha">
              <div className="donation-panel__header">
                <span>Arrecadado até agora</span>
                <strong>{formatCurrency(campaign.valorTotalArrecadado)}</strong>
                <span>de {formatCurrency(campaign.metaFinanceira)}</span>
              </div>
              <ProgressBar campaign={campaign} />
              <dl className="transparency-list">
                <div><dt>Faltam para a meta</dt><dd>{formatCurrency(remainingAmount)}</dd></div>
                <div>
                  <dt>Tempo restante</dt>
                  <dd>{daysRemaining === null ? 'Prazo indisponível' : `${daysRemaining} ${daysRemaining === 1 ? 'dia' : 'dias'}`}</dd>
                </div>
                <div><dt>Situação</dt><dd>Recebendo apoios</dd></div>
              </dl>
              <Link className="button button--accent button--wide" to={`/campanhas/${campaign.id}/doar`}><Heart size={19} aria-hidden="true" /> Apoiar esta campanha</Link>
              <button className="button button--secondary button--wide" type="button" onClick={shareCampaign}><Share2 size={18} aria-hidden="true" /> Compartilhar</button>
              <p className="panel-note">O valor arrecadado é atualizado após o processamento da doação.</p>
            </aside>
          </div>
        </div>
      </section>
    </>
  )
}
