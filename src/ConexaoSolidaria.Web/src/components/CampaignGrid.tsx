import type { PublicCampaign } from '../types/api'
import { CampaignCard } from './CampaignCard'

interface CampaignGridProps {
  campaigns: PublicCampaign[]
}

export function CampaignGrid({ campaigns }: CampaignGridProps) {
  return (
    <div className="campaign-grid">
      {campaigns.map((campaign) => <CampaignCard key={campaign.id} campaign={campaign} />)}
    </div>
  )
}
