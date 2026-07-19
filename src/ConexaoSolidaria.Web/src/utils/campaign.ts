import type { PublicCampaign } from '../types/api'

export function getCampaignProgress(campaign: PublicCampaign) {
  if (campaign.metaFinanceira <= 0) return 0
  return Math.max(0, Math.min(100, (campaign.valorTotalArrecadado / campaign.metaFinanceira) * 100))
}

export function getRawCampaignProgress(campaign: PublicCampaign) {
  if (campaign.metaFinanceira <= 0) return 0
  return Math.max(0, (campaign.valorTotalArrecadado / campaign.metaFinanceira) * 100)
}

export function getRemainingAmount(campaign: PublicCampaign) {
  return Math.max(0, campaign.metaFinanceira - campaign.valorTotalArrecadado)
}

export function getDaysRemaining(endDate: string, now = new Date()) {
  const endTime = new Date(endDate).getTime()
  const nowTime = now.getTime()

  if (!Number.isFinite(endTime) || !Number.isFinite(nowTime)) return null

  const difference = endTime - nowTime
  return Math.max(0, Math.ceil(difference / 86_400_000))
}

export function sortByClosestToGoal(campaigns: PublicCampaign[]) {
  return [...campaigns].sort((first, second) => {
    const progressDifference = getRawCampaignProgress(second) - getRawCampaignProgress(first)
    return progressDifference || first.titulo.localeCompare(second.titulo, 'pt-BR')
  })
}
