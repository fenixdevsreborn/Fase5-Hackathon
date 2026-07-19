import type {
  DonationAccepted,
  ManagedCampaign,
  PaginatedResponse,
  PublicCampaign,
  SaveCampaignInput,
} from '../types/api'
import { ApiError, apiRequest } from './http'

const campaignsApiUrl = import.meta.env.VITE_CAMPAIGNS_API_URL ?? '/campaigns-api'
const maxPageSize = 100

async function fetchAllPages(
  path: string,
  searchParams: URLSearchParams,
  signal?: AbortSignal,
): Promise<PublicCampaign[]> {
  const campaigns: PublicCampaign[] = []
  let page = 1
  let hasNextPage = true

  while (hasNextPage) {
    searchParams.set('page', String(page))
    searchParams.set('pageSize', String(maxPageSize))

    const response = await apiRequest<PaginatedResponse<PublicCampaign>>(
      `${campaignsApiUrl}${path}?${searchParams.toString()}`,
      { signal },
    )

    campaigns.push(...response.items)
    hasNextPage = response.hasNextPage
    page += 1
  }

  return campaigns
}

export async function getPublicCampaigns(search = '', signal?: AbortSignal) {
  const title = search.trim()

  if (!title) {
    return fetchAllPages('/api/campanhas/transparencia', new URLSearchParams(), signal)
  }

  try {
    return await fetchAllPages(
      '/api/campanhas/transparencia-search',
      new URLSearchParams({ titulo: title }),
      signal,
    )
  } catch (error) {
    if (!(error instanceof ApiError) || error.status !== 503) {
      throw error
    }

    return fetchAllPages(
      '/api/campanhas/transparencia',
      new URLSearchParams({ titulo: title }),
      signal,
    )
  }
}

export async function getPublicCampaign(id: string, signal?: AbortSignal) {
  const campaigns = await getPublicCampaigns('', signal)
  return campaigns.find((campaign) => campaign.id === id) ?? null
}

export function getManagedCampaign(id: string, token: string, signal?: AbortSignal) {
  return apiRequest<ManagedCampaign>(`${campaignsApiUrl}/api/campanhas/${id}`, {
    token,
    signal,
  })
}

export function createCampaign(input: SaveCampaignInput, token: string) {
  return apiRequest<ManagedCampaign>(`${campaignsApiUrl}/api/campanhas`, {
    method: 'POST',
    token,
    body: JSON.stringify(input),
  })
}

export function updateCampaign(id: string, input: SaveCampaignInput, token: string) {
  return apiRequest<ManagedCampaign>(`${campaignsApiUrl}/api/campanhas/${id}`, {
    method: 'PUT',
    token,
    body: JSON.stringify(input),
  })
}

export function createDonation(campaignId: string, amount: number, token: string) {
  return apiRequest<DonationAccepted>(`${campaignsApiUrl}/api/doacoes`, {
    method: 'POST',
    token,
    body: JSON.stringify({ idCampanha: campaignId, valorDoacao: amount }),
  })
}
