import { ArrowRight, BarChart3, CircleDollarSign, Edit3, Plus, Target } from 'lucide-react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { CampaignGridSkeleton, EmptyState, ErrorState } from '../components/AsyncStates'
import { FeedbackBanner } from '../components/FeedbackBanner'
import { useCampaigns } from '../hooks/use-campaigns'
import { formatCurrency, formatDate } from '../utils/format'

export function ManagementPage() {
  const { campaigns, isLoading, error, retry } = useCampaigns()
  const location = useLocation()
  const navigate = useNavigate()
  const feedback = (location.state as { message?: string } | null)?.message
  const totalGoal = campaigns.reduce((total, campaign) => total + campaign.metaFinanceira, 0)
  const totalRaised = campaigns.reduce((total, campaign) => total + campaign.valorTotalArrecadado, 0)

  return (
    <>
      <header className="management-header">
        <div className="container management-header__inner">
          <div><span className="eyebrow eyebrow--light">Área da organização</span><h1>Gestão de campanhas</h1><p>Acompanhe as campanhas ativas e publique novas mobilizações.</p></div>
          <Link className="button button--light" to="/gestao/campanhas/nova"><Plus size={19} aria-hidden="true" /> Nova campanha</Link>
        </div>
      </header>

      <section className="section container management-content">
        {feedback && <FeedbackBanner tone="success" title={feedback} onDismiss={() => navigate('/gestao', { replace: true })} />}
        <div className="metric-grid" aria-label="Resumo das campanhas ativas">
          <div className="metric"><BarChart3 aria-hidden="true" /><span>Campanhas ativas</span><strong>{campaigns.length}</strong></div>
          <div className="metric"><CircleDollarSign aria-hidden="true" /><span>Total arrecadado</span><strong>{formatCurrency(totalRaised)}</strong></div>
          <div className="metric"><Target aria-hidden="true" /><span>Metas ativas</span><strong>{formatCurrency(totalGoal)}</strong></div>
        </div>

        <div className="section-heading section-heading--split"><div><span className="eyebrow">Visão operacional</span><h2>Campanhas publicadas</h2></div><Link className="text-link" to="/campanhas">Ver página pública <ArrowRight size={18} aria-hidden="true" /></Link></div>
        {isLoading && <CampaignGridSkeleton count={3} />}
        {error && <ErrorState message={error} onRetry={retry} />}
        {!isLoading && !error && campaigns.length === 0 && <EmptyState title="Nenhuma campanha ativa" description="Crie a primeira campanha para começar a mobilização."><Link className="button button--primary" to="/gestao/campanhas/nova"><Plus size={18} aria-hidden="true" /> Criar campanha</Link></EmptyState>}
        {!isLoading && !error && campaigns.length > 0 && (
          <div className="management-table-wrap">
            <table className="management-table">
              <caption className="sr-only">Campanhas ativas disponíveis para gestão</caption>
              <thead><tr><th scope="col">Campanha</th><th scope="col">Encerramento</th><th scope="col">Arrecadado</th><th scope="col">Meta</th><th scope="col"><span className="sr-only">Ações</span></th></tr></thead>
              <tbody>{campaigns.map((campaign) => <tr key={campaign.id}><td><strong>{campaign.titulo}</strong><span>Ativa</span></td><td data-label="Encerramento">{formatDate(campaign.dataFim)}</td><td data-label="Arrecadado">{formatCurrency(campaign.valorTotalArrecadado)}</td><td data-label="Meta">{formatCurrency(campaign.metaFinanceira)}</td><td><Link className="button button--secondary button--small" to={`/gestao/campanhas/${campaign.id}/editar`}><Edit3 size={16} aria-hidden="true" /> Editar</Link></td></tr>)}</tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}
