import { describe, expect, it } from 'vitest'
import type { PublicCampaign } from '../types/api'
import { getCampaignProgress, getDaysRemaining, getRemainingAmount, sortByClosestToGoal } from './campaign'

const campaign: PublicCampaign = {
  id: '8fe52b19-4b1c-48b7-9a09-bcb87e333174',
  titulo: 'Campanha de teste',
  descricao: 'Descrição usada somente no teste automatizado.',
  dataInicio: '2026-07-01T00:00:00Z',
  dataFim: '2026-07-20T00:00:00Z',
  metaFinanceira: 1000,
  valorTotalArrecadado: 750,
}

describe('campaign helpers', () => {
  it('calcula e limita o progresso visual em 100%', () => {
    expect(getCampaignProgress(campaign)).toBe(75)
    expect(getCampaignProgress({ ...campaign, valorTotalArrecadado: 1250 })).toBe(100)
  })

  it('nunca retorna valor restante negativo', () => {
    expect(getRemainingAmount(campaign)).toBe(250)
    expect(getRemainingAmount({ ...campaign, valorTotalArrecadado: 1250 })).toBe(0)
  })

  it('calcula dias restantes sem retornar números negativos', () => {
    expect(getDaysRemaining('2026-07-20T00:00:00Z', new Date('2026-07-17T00:00:00Z'))).toBe(3)
    expect(getDaysRemaining('2026-07-10T00:00:00Z', new Date('2026-07-17T00:00:00Z'))).toBe(0)
  })

  it('retorna null quando a data final está ausente ou é inválida', () => {
    expect(getDaysRemaining('')).toBeNull()
    expect(getDaysRemaining('data inválida')).toBeNull()
  })

  it('ordena campanhas pelo percentual real da meta', () => {
    const ordered = sortByClosestToGoal([
      campaign,
      { ...campaign, id: 'second', titulo: 'Segunda', valorTotalArrecadado: 950 },
    ])
    expect(ordered[0].id).toBe('second')
  })
})
