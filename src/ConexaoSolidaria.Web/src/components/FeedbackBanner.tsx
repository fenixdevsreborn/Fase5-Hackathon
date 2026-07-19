import { CircleAlert, CircleCheck, Info, X } from 'lucide-react'

type FeedbackTone = 'success' | 'error' | 'info'

interface FeedbackBannerProps {
  tone: FeedbackTone
  title: string
  message?: string
  onDismiss?: () => void
}

const icons = {
  success: CircleCheck,
  error: CircleAlert,
  info: Info,
}

export function FeedbackBanner({ tone, title, message, onDismiss }: FeedbackBannerProps) {
  const Icon = icons[tone]
  return (
    <div className={`feedback feedback--${tone}`} role={tone === 'error' ? 'alert' : 'status'}>
      <Icon size={21} aria-hidden="true" />
      <div><strong>{title}</strong>{message && <p>{message}</p>}</div>
      {onDismiss && (
        <button className="icon-button" type="button" onClick={onDismiss} aria-label="Fechar mensagem" title="Fechar mensagem">
          <X size={18} aria-hidden="true" />
        </button>
      )}
    </div>
  )
}
