import { ArrowLeft, RefreshCw, Save } from 'lucide-react'
import { type FormEvent, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { FeedbackBanner } from '../components/FeedbackBanner'
import { useAuth } from '../context/auth-context'
import { createCampaign, getManagedCampaign, updateCampaign } from '../services/campaigns-api'
import { ApiError } from '../services/http'
import type { CampaignStatus, SaveCampaignInput } from '../types/api'
import { toDateTimeLocal, toIsoDate } from '../utils/format'

interface CampaignFormState {
  titulo: string
  descricao: string
  dataInicio: string
  dataFim: string
  metaFinanceira: string
  status: CampaignStatus
}

const initialForm: CampaignFormState = {
  titulo: '',
  descricao: '',
  dataInicio: '',
  dataFim: '',
  metaFinanceira: '',
  status: 'Ativa',
}

export function CampaignFormPage() {
  const { id } = useParams()
  const isEditing = Boolean(id)
  const { session } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState(initialForm)
  const [isLoading, setIsLoading] = useState(isEditing)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id || !session) return
    const controller = new AbortController()
    getManagedCampaign(id, session.accessToken, controller.signal)
      .then((campaign) => setForm({
        titulo: campaign.titulo,
        descricao: campaign.descricao,
        dataInicio: toDateTimeLocal(campaign.dataInicio),
        dataFim: toDateTimeLocal(campaign.dataFim),
        metaFinanceira: String(campaign.metaFinanceira),
        status: campaign.status,
      }))
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof ApiError ? requestError.message : 'Não foi possível carregar a campanha.')
      })
      .finally(() => { if (!controller.signal.aborted) setIsLoading(false) })
    return () => controller.abort()
  }, [id, session])

  function updateField<Key extends keyof CampaignFormState>(key: Key, value: CampaignFormState[Key]) {
    setForm((current) => ({ ...current, [key]: value }))
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!session) return

    const financialGoal = Number(form.metaFinanceira.replace(',', '.'))
    if (!form.titulo.trim() || !form.descricao.trim() || !form.dataInicio || !form.dataFim) {
      setError('Preencha todos os campos obrigatórios.')
      return
    }
    if (!Number.isFinite(financialGoal) || financialGoal <= 0) {
      setError('A meta financeira deve ser maior que zero.')
      return
    }
    if (new Date(form.dataFim) < new Date()) {
      setError('A data de encerramento não pode estar no passado.')
      return
    }
    if (new Date(form.dataFim) < new Date(form.dataInicio)) {
      setError('A data de encerramento deve ser posterior à data de início.')
      return
    }

    const input: SaveCampaignInput = {
      titulo: form.titulo.trim(),
      descricao: form.descricao.trim(),
      dataInicio: toIsoDate(form.dataInicio),
      dataFim: toIsoDate(form.dataFim),
      metaFinanceira: financialGoal,
      status: form.status,
    }

    setIsSubmitting(true)
    setError(null)
    try {
      if (id) await updateCampaign(id, input, session.accessToken)
      else await createCampaign(input, session.accessToken)
      navigate('/gestao', { replace: true, state: { message: isEditing ? 'Campanha atualizada com sucesso.' : 'Campanha publicada com sucesso.' } })
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Não foi possível salvar a campanha.')
    } finally {
      setIsSubmitting(false)
    }
  }

  if (isLoading) return <div className="page-loading" aria-live="polite"><RefreshCw className="spin" aria-hidden="true" /> Carregando campanha...</div>

  return (
    <section className="section form-page">
      <div className="container form-page__container">
        <Link className="back-link" to="/gestao"><ArrowLeft size={17} aria-hidden="true" /> Voltar para gestão</Link>
        <div className="form-page__heading"><span className="eyebrow">Área da organização</span><h1>{isEditing ? 'Editar campanha' : 'Nova campanha'}</h1><p>As informações publicadas serão exibidas no painel público de transparência.</p></div>
        <form className="campaign-form" onSubmit={handleSubmit} noValidate>
          {error && <FeedbackBanner tone="error" title="Não foi possível salvar" message={error} onDismiss={() => setError(null)} />}
          <div className="form-field form-field--full"><label htmlFor="campaign-title">Título</label><input id="campaign-title" maxLength={160} value={form.titulo} onChange={(event) => updateField('titulo', event.target.value)} required /></div>
          <div className="form-field form-field--full"><label htmlFor="campaign-description">Descrição</label><textarea id="campaign-description" rows={7} value={form.descricao} onChange={(event) => updateField('descricao', event.target.value)} required /><small>Explique de forma clara o objetivo e como os recursos serão utilizados.</small></div>
          <div className="form-field"><label htmlFor="campaign-start">Data de início</label><input id="campaign-start" type="datetime-local" value={form.dataInicio} onChange={(event) => updateField('dataInicio', event.target.value)} required /></div>
          <div className="form-field"><label htmlFor="campaign-end">Data de encerramento</label><input id="campaign-end" type="datetime-local" value={form.dataFim} onChange={(event) => updateField('dataFim', event.target.value)} required /></div>
          <div className="form-field"><label htmlFor="campaign-goal">Meta financeira</label><div className="currency-input"><span>R$</span><input id="campaign-goal" type="number" min="0.01" step="0.01" inputMode="decimal" value={form.metaFinanceira} onChange={(event) => updateField('metaFinanceira', event.target.value)} required /></div></div>
          <div className="form-field"><label htmlFor="campaign-status">Status</label><select id="campaign-status" value={form.status} onChange={(event) => updateField('status', event.target.value as CampaignStatus)}><option value="Ativa">Ativa</option><option value="Concluida">Concluída</option><option value="Cancelada">Cancelada</option></select></div>
          <div className="campaign-form__actions"><Link className="button button--ghost" to="/gestao">Cancelar</Link><button className="button button--primary" type="submit" disabled={isSubmitting}>{isSubmitting ? <><RefreshCw className="spin" size={18} aria-hidden="true" /> Salvando...</> : <><Save size={18} aria-hidden="true" /> {isEditing ? 'Salvar alterações' : 'Publicar campanha'}</>}</button></div>
        </form>
      </div>
    </section>
  )
}
