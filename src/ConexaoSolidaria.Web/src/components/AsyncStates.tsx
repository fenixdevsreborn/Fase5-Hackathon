import { CircleAlert, Inbox, RefreshCw } from 'lucide-react'

export function CampaignGridSkeleton({ count = 3 }: { count?: number }) {
  return (
    <div className="campaign-grid" aria-live="polite" aria-busy="true" aria-label="Carregando campanhas">
      {Array.from({ length: count }, (_, index) => (
        <div className="campaign-card skeleton-card" key={index} aria-hidden="true">
          <div className="skeleton skeleton--image" />
          <div className="campaign-card__body">
            <div className="skeleton skeleton--eyebrow" />
            <div className="skeleton skeleton--title" />
            <div className="skeleton skeleton--text" />
            <div className="skeleton skeleton--text skeleton--short" />
            <div className="skeleton skeleton--progress" />
          </div>
        </div>
      ))}
    </div>
  )
}

interface ErrorStateProps {
  message: string
  onRetry: () => void
}

export function ErrorState({ message, onRetry }: ErrorStateProps) {
  return (
    <div className="state-block" role="alert">
      <CircleAlert size={34} aria-hidden="true" />
      <h2>Não foi possível carregar as campanhas</h2>
      <p>{message}</p>
      <button className="button button--secondary" type="button" onClick={onRetry}>
        <RefreshCw size={18} aria-hidden="true" /> Tentar novamente
      </button>
    </div>
  )
}

interface EmptyStateProps {
  title?: string
  description?: string
  children?: React.ReactNode
}

export function EmptyState({
  title = 'Nenhuma campanha disponível',
  description = 'Novas campanhas aparecerão aqui assim que forem publicadas pela organização.',
  children,
}: EmptyStateProps) {
  return (
    <div className="state-block">
      <Inbox size={34} aria-hidden="true" />
      <h2>{title}</h2>
      <p>{description}</p>
      {children}
    </div>
  )
}
