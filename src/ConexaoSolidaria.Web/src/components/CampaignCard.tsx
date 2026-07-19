import { ArrowRight, Heart, Timer } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { PublicCampaign } from '../types/api'
import { getDaysRemaining } from '../utils/campaign'
import { formatCurrency } from '../utils/format'
import { ProgressBar } from './ProgressBar'

interface CampaignCardProps {
  campaign: PublicCampaign
}

export function CampaignCard({ campaign }: CampaignCardProps) {
  const daysRemaining = getDaysRemaining(campaign.dataFim)

  return (
    <article className="campaign-card">
      <Link className="campaign-card__image-link" to={`/campanhas/${campaign.id}`} aria-label={`Ver ${campaign.titulo}`}>
        <img
          className="campaign-card__image"
          src="/images/hero-solidariedade.webp"
          alt="Voluntários da Esperança Solidária preparando doações"
        />
      </Link>
      <div className="campaign-card__body">
        <div className="campaign-card__meta">
          <span className="status-label">Campanha ativa</span>
          <span className="deadline">
            <Timer size={15} aria-hidden="true" />
            {daysRemaining === null ? 'Prazo indisponível' : `${daysRemaining} ${daysRemaining === 1 ? 'dia' : 'dias'}`}
          </span>
        </div>
        <h3><Link to={`/campanhas/${campaign.id}`}>{campaign.titulo}</Link></h3>
        <p className="campaign-card__description">{campaign.descricao}</p>
        <ProgressBar campaign={campaign} compact />
        <div className="campaign-card__amounts">
          <span><strong>{formatCurrency(campaign.valorTotalArrecadado)}</strong> arrecadados</span>
          <span>Meta {formatCurrency(campaign.metaFinanceira)}</span>
        </div>
        <div className="campaign-card__actions">
          <Link className="button button--secondary button--small" to={`/campanhas/${campaign.id}`}>
            Ver detalhes <ArrowRight size={17} aria-hidden="true" />
          </Link>
          <Link className="button button--primary button--small" to={`/campanhas/${campaign.id}/doar`}>
            <Heart size={17} aria-hidden="true" /> Apoiar
          </Link>
        </div>
      </div>
    </article>
  )
}
