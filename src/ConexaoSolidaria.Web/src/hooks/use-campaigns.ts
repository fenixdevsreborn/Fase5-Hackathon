import { useCallback, useEffect, useState } from 'react'
import { getPublicCampaigns } from '../services/campaigns-api'
import type { PublicCampaign } from '../types/api'

export function useCampaigns(search = '') {
  const [campaigns, setCampaigns] = useState<PublicCampaign[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [requestVersion, setRequestVersion] = useState(0)

  const retry = useCallback(() => setRequestVersion((version) => version + 1), [])

  useEffect(() => {
    const controller = new AbortController()
    queueMicrotask(() => {
      if (!controller.signal.aborted) {
        setIsLoading(true)
        setError(null)
      }
    })

    getPublicCampaigns(search, controller.signal)
      .then(setCampaigns)
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof Error ? requestError.message : 'Não foi possível carregar as campanhas.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setIsLoading(false)
      })

    return () => controller.abort()
  }, [search, requestVersion])

  return { campaigns, isLoading, error, retry }
}
