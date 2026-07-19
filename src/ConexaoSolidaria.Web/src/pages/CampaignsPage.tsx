import { ChevronLeft, ChevronRight, Search, SlidersHorizontal, X } from 'lucide-react'
import { FormEvent, useMemo, useState } from 'react'
import { CampaignGrid } from '../components/CampaignGrid'
import { CampaignGridSkeleton, EmptyState, ErrorState } from '../components/AsyncStates'
import { useCampaigns } from '../hooks/use-campaigns'
import { getRawCampaignProgress } from '../utils/campaign'

type ProgressFilter = 'all' | 'starting' | 'advancing' | 'near' | 'reached'
type SortOption = 'progress' | 'raised' | 'goal' | 'title'

const pageSize = 6

export function CampaignsPage() {
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [progressFilter, setProgressFilter] = useState<ProgressFilter>('all')
  const [sort, setSort] = useState<SortOption>('progress')
  const [page, setPage] = useState(1)
  const { campaigns, isLoading, error, retry } = useCampaigns(search)

  const filteredCampaigns = useMemo(() => {
    const filtered = campaigns.filter((campaign) => {
      const progress = getRawCampaignProgress(campaign)
      if (progressFilter === 'starting') return progress < 50
      if (progressFilter === 'advancing') return progress >= 50 && progress < 90
      if (progressFilter === 'near') return progress >= 90 && progress < 100
      if (progressFilter === 'reached') return progress >= 100
      return true
    })

    return filtered.sort((first, second) => {
      if (sort === 'raised') return second.valorTotalArrecadado - first.valorTotalArrecadado
      if (sort === 'goal') return second.metaFinanceira - first.metaFinanceira
      if (sort === 'title') return first.titulo.localeCompare(second.titulo, 'pt-BR')
      return getRawCampaignProgress(second) - getRawCampaignProgress(first)
    })
  }, [campaigns, progressFilter, sort])

  const totalPages = Math.ceil(filteredCampaigns.length / pageSize)
  const safePage = Math.min(page, Math.max(totalPages, 1))
  const visibleCampaigns = filteredCampaigns.slice((safePage - 1) * pageSize, safePage * pageSize)

  function handleSearch(event: FormEvent) {
    event.preventDefault()
    setSearch(searchInput.trim())
    setPage(1)
  }

  function clearSearch() {
    setSearchInput('')
    setSearch('')
    setPage(1)
  }

  return (
    <>
      <header className="page-header">
        <div className="container page-header__inner">
          <span className="eyebrow">Transparência pública</span>
          <h1>Campanhas ativas</h1>
          <p>Consulte metas, valores já arrecadados e o progresso de cada mobilização.</p>
        </div>
      </header>

      <section className="section container" aria-labelledby="campaign-results-title">
        <form className="campaign-toolbar" role="search" onSubmit={handleSearch}>
          <div className="search-field">
            <label className="sr-only" htmlFor="campaign-search">Buscar campanha por título</label>
            <Search size={19} aria-hidden="true" />
            <input id="campaign-search" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder="Buscar por título" />
            {searchInput && <button className="icon-button" type="button" onClick={clearSearch} aria-label="Limpar busca" title="Limpar busca"><X size={18} aria-hidden="true" /></button>}
          </div>
          <button className="button button--primary" type="submit">Buscar</button>
          <div className="filter-field">
            <SlidersHorizontal size={18} aria-hidden="true" />
            <label htmlFor="progress-filter">Progresso</label>
            <select id="progress-filter" value={progressFilter} onChange={(event) => { setProgressFilter(event.target.value as ProgressFilter); setPage(1) }}>
              <option value="all">Todos</option>
              <option value="starting">Até 50%</option>
              <option value="advancing">De 50% a 89%</option>
              <option value="near">Próximas da meta</option>
              <option value="reached">Meta alcançada</option>
            </select>
          </div>
          <div className="filter-field">
            <label htmlFor="campaign-sort">Ordenar</label>
            <select id="campaign-sort" value={sort} onChange={(event) => { setSort(event.target.value as SortOption); setPage(1) }}>
              <option value="progress">Maior progresso</option>
              <option value="raised">Maior arrecadação</option>
              <option value="goal">Maior meta</option>
              <option value="title">Título</option>
            </select>
          </div>
        </form>

        <div className="results-summary">
          <h2 id="campaign-results-title">{search ? `Resultados para “${search}”` : 'Todas as campanhas'}</h2>
          {!isLoading && !error && <span aria-live="polite">{filteredCampaigns.length} {filteredCampaigns.length === 1 ? 'campanha encontrada' : 'campanhas encontradas'}</span>}
        </div>

        {isLoading && <CampaignGridSkeleton count={6} />}
        {error && <ErrorState message={error} onRetry={retry} />}
        {!isLoading && !error && visibleCampaigns.length > 0 && <CampaignGrid campaigns={visibleCampaigns} />}
        {!isLoading && !error && visibleCampaigns.length === 0 && (
          <EmptyState title="Nenhuma campanha encontrada" description="Tente outro termo ou ajuste o filtro de progresso.">
            {(search || progressFilter !== 'all') && <button className="button button--secondary" type="button" onClick={() => { clearSearch(); setProgressFilter('all') }}>Limpar filtros</button>}
          </EmptyState>
        )}

        {totalPages > 1 && (
          <nav className="pagination" aria-label="Paginação das campanhas">
            <button className="icon-button" type="button" disabled={safePage === 1} onClick={() => setPage((current) => current - 1)} aria-label="Página anterior" title="Página anterior"><ChevronLeft aria-hidden="true" /></button>
            <span>Página <strong>{safePage}</strong> de {totalPages}</span>
            <button className="icon-button" type="button" disabled={safePage === totalPages} onClick={() => setPage((current) => current + 1)} aria-label="Próxima página" title="Próxima página"><ChevronRight aria-hidden="true" /></button>
          </nav>
        )}
      </section>
    </>
  )
}
