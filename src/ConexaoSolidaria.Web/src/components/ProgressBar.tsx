import { getCampaignProgress } from '../utils/campaign'
import type { PublicCampaign } from '../types/api'

interface ProgressBarProps {
  campaign: PublicCampaign
  compact?: boolean
}

export function ProgressBar({ campaign, compact = false }: ProgressBarProps) {
  const progress = getCampaignProgress(campaign)
  const roundedProgress = Math.round(progress)

  return (
    <div className={compact ? 'progress progress--compact' : 'progress'}>
      <div className="progress__labels">
        <span>{roundedProgress}% da meta</span>
        {campaign.valorTotalArrecadado >= campaign.metaFinanceira && <strong>Meta alcançada</strong>}
      </div>
      <progress
        className="progress__track"
        max="100"
        value={progress}
        aria-label={`Progresso da campanha ${campaign.titulo}: ${roundedProgress}% da meta`}
      >
        {roundedProgress}%
      </progress>
    </div>
  )
}
