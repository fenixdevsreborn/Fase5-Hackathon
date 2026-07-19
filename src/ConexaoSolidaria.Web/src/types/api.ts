export type UserRole = 'GestorONG' | 'Doador'

export type CampaignStatus = 'Ativa' | 'Concluida' | 'Cancelada'

export interface AuthSession {
  usuarioId: string
  nomeCompleto: string
  email: string
  role: UserRole
  accessToken: string
  expiraEm: string
}

export interface LoginInput {
  email: string
  senha: string
}

export interface RegisterInput extends LoginInput {
  nomeCompleto: string
  cpf: string
}

export interface PublicCampaign {
  id: string
  titulo: string
  descricao: string
  dataInicio: string
  dataFim: string
  metaFinanceira: number
  valorTotalArrecadado: number
}

export interface ManagedCampaign extends PublicCampaign {
  status: CampaignStatus
}

export interface SaveCampaignInput {
  titulo: string
  descricao: string
  dataInicio: string
  dataFim: string
  metaFinanceira: number
  status: CampaignStatus
}

export interface DonationAccepted {
  doacaoId: string
  campanhaId: string
  valorDoacao: number
  status: string
  mensagem: string
}

export interface PaginatedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export interface ApiProblem {
  mensagem?: string
  title?: string
  errors?: Record<string, string[]>
}
