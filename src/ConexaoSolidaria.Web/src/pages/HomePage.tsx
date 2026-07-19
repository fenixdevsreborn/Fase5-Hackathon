import { ArrowRight, BarChart3, Heart, HeartHandshake, ShieldCheck } from 'lucide-react'
import { Link } from 'react-router-dom'
import { CampaignGrid } from '../components/CampaignGrid'
import { CampaignGridSkeleton, EmptyState, ErrorState } from '../components/AsyncStates'
import { ProgressBar } from '../components/ProgressBar'
import { useCampaigns } from '../hooks/use-campaigns'
import { sortByClosestToGoal } from '../utils/campaign'
import { formatCurrency } from '../utils/format'

export function HomePage() {
  const { campaigns, isLoading, error, retry } = useCampaigns()
  const orderedCampaigns = sortByClosestToGoal(campaigns)
  const featuredCampaign = orderedCampaigns[0]
  const nearGoalCampaigns = orderedCampaigns.slice(0, 3)
  const totalRaised = campaigns.reduce((total, campaign) => total + campaign.valorTotalArrecadado, 0)

  return (
    <>
      <section className="home-hero" aria-labelledby="home-title">
        <div className="home-hero__overlay" aria-hidden="true" />
        <div className="container home-hero__content">
          <span className="eyebrow eyebrow--light">Solidariedade que se transforma em cuidado</span>
          <h1 id="home-title">Conexão Solidária</h1>
          <p>Acompanhe campanhas da Esperança Solidária e ajude a ampliar o acolhimento de crianças em situação de vulnerabilidade.</p>
          <div className="button-row">
            <Link className="button button--accent" to="/campanhas"><Heart size={19} aria-hidden="true" /> Conhecer campanhas</Link>
            <Link className="button button--light" to="/cadastro">Quero apoiar <ArrowRight size={19} aria-hidden="true" /></Link>
          </div>
        </div>
      </section>

      <section className="trust-band" aria-label="Compromissos da Conexão Solidária">
        <div className="container trust-band__grid">
          <div><HeartHandshake aria-hidden="true" /><span><strong>Mais de 10 anos</strong> acolhendo crianças</span></div>
          <div><BarChart3 aria-hidden="true" /><span><strong>Transparência</strong> em cada campanha</span></div>
          <div><ShieldCheck aria-hidden="true" /><span><strong>Processamento rastreável</strong> das doações</span></div>
        </div>
      </section>

      {isLoading && <section className="section container"><div className="section-heading"><span className="eyebrow">Campanhas ativas</span><h2>Onde sua ajuda pode chegar</h2></div><CampaignGridSkeleton /></section>}
      {error && <section className="section container"><ErrorState message={error} onRetry={retry} /></section>}
      {!isLoading && !error && campaigns.length === 0 && (
        <section className="section container"><EmptyState><Link className="button button--secondary" to="/entrar">Acesso da organização</Link></EmptyState></section>
      )}

      {!isLoading && !error && featuredCampaign && (
        <>
          <section className="section section--feature" aria-labelledby="featured-title">
            <div className="container feature-campaign">
              <div className="feature-campaign__image-wrap">
                <img src="/images/hero-solidariedade.webp" alt="Voluntários separando alimentos e livros para doação" />
              </div>
              <div className="feature-campaign__content">
                <span className="eyebrow">Mais perto da meta</span>
                <h2 id="featured-title">{featuredCampaign.titulo}</h2>
                <p>{featuredCampaign.descricao}</p>
                <ProgressBar campaign={featuredCampaign} />
                <div className="feature-campaign__numbers">
                  <span><strong>{formatCurrency(featuredCampaign.valorTotalArrecadado)}</strong> arrecadados</span>
                  <span><strong>{formatCurrency(featuredCampaign.metaFinanceira)}</strong> de meta</span>
                </div>
                <div className="button-row">
                  <Link className="button button--primary" to={`/campanhas/${featuredCampaign.id}/doar`}><Heart size={18} aria-hidden="true" /> Apoiar agora</Link>
                  <Link className="button button--secondary" to={`/campanhas/${featuredCampaign.id}`}>Ver campanha <ArrowRight size={18} aria-hidden="true" /></Link>
                </div>
              </div>
            </div>
          </section>

          <section className="section container" aria-labelledby="near-goal-title">
            <div className="section-heading section-heading--split">
              <div><span className="eyebrow">Mobilização em andamento</span><h2 id="near-goal-title">Campanhas próximas da meta</h2></div>
              <Link className="text-link" to="/campanhas">Ver todas <ArrowRight size={18} aria-hidden="true" /></Link>
            </div>
            <CampaignGrid campaigns={nearGoalCampaigns} />
          </section>

          <section className="impact-band" aria-label="Resumo das campanhas ativas">
            <div className="container impact-band__inner">
              <div><strong>{campaigns.length}</strong><span>{campaigns.length === 1 ? 'campanha ativa' : 'campanhas ativas'}</span></div>
              <div><strong>{formatCurrency(totalRaised)}</strong><span>arrecadados nas campanhas atuais</span></div>
              <div><strong>100%</strong><span>dos valores exibidos vêm da API pública</span></div>
            </div>
          </section>
        </>
      )}
    </>
  )
}
