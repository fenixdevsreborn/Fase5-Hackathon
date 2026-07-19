import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import type { PublicCampaign } from '../types/api'
import { CampaignCard } from './CampaignCard'

const campaign: PublicCampaign = {
  id: '8fe52b19-4b1c-48b7-9a09-bcb87e333174',
  titulo: 'Material escolar para acolhimento',
  descricao: 'Campanha usada somente para validar a apresentação do componente.',
  dataInicio: '2026-07-01T00:00:00Z',
  dataFim: '2027-12-31T00:00:00Z',
  metaFinanceira: 10000,
  valorTotalArrecadado: 4500,
}

describe('CampaignCard', () => {
  it('apresenta informações financeiras e ações acessíveis', () => {
    render(<MemoryRouter><CampaignCard campaign={campaign} /></MemoryRouter>)

    expect(screen.getByRole('heading', { name: campaign.titulo })).toBeInTheDocument()
    expect(screen.getByRole('progressbar')).toHaveAttribute('value', '45')
    expect(screen.getByRole('link', { name: /apoiar/i })).toHaveAttribute('href', `/campanhas/${campaign.id}/doar`)
    expect(screen.getByRole('img')).toHaveAccessibleName(/voluntários/i)
  })

  it('não apresenta NaN quando a data final está ausente', () => {
    render(<MemoryRouter><CampaignCard campaign={{ ...campaign, dataFim: '' }} /></MemoryRouter>)

    expect(screen.getByText('Prazo indisponível')).toBeInTheDocument()
    expect(screen.queryByText(/NaN/)).not.toBeInTheDocument()
  })
})
