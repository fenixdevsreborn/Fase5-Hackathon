# =============================================================================
# push-dockerhub.ps1 - Build + push das 5 imagens da aplicacao para o Docker Hub
# (junonn5/conexao-solidaria-<svc>:latest). E a forma canonica de "soltar uma
# imagem nova": ao rodar este script, a nova tag :latest sobe para o Docker Hub e
# o Keel (rodando no cluster) detecta o novo digest e recria os pods sozinho.
#
# Uso:
#   $env:DOCKERHUB_TOKEN = "dckr_pat_..."   # PAT do Docker Hub (NUNCA versionar)
#   pwsh infra/k8s/push-dockerhub.ps1
#
#   # variacoes:
#   pwsh infra/k8s/push-dockerhub.ps1 -User junonn5            # troca o usuario
#   pwsh infra/k8s/push-dockerhub.ps1 -ExtraTag v2025-07-17    # publica :latest E :v...
#   pwsh infra/k8s/push-dockerhub.ps1 -SkipLogin              # ja fez docker login antes
#
# Seguranca: o token e lido de $env:DOCKERHUB_TOKEN e passado ao docker via
# --password-stdin; nunca e gravado em disco. O Docker guarda a credencial no
# credential store, entao chamadas seguintes de push nao precisam do token.
#
# Pre-requisitos:
#   - Docker em execucao (Docker Desktop).
#   - Reposit0rios publicos em hub.docker.com/u/<User> (o Keel puxa sem credencial).
# =============================================================================
[CmdletBinding()]
param(
    [string]$User = "junonn5",
    # Prefixo dos repositorios no Docker Hub. Nome final: <User>/<Prefix><svc>.
    [string]$Prefix = "conexao-solidaria-",
    # Tag adicional (alem de :latest) para rastreabilidade. Opcional.
    [string]$ExtraTag,
    # Pula o docker login (util quando a credencial ja esta no credential store).
    [switch]$SkipLogin
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path   # ...\infra\k8s
$repoRoot  = Split-Path -Parent (Split-Path -Parent $scriptDir) # raiz do repo

# Servico logico -> Dockerfile (relativo a raiz do repo). Mesmo mapa de up.ps1.
$services = [ordered]@{
    "identity-api"     = "src/ConexaoSolidaria.Identity.Api/Dockerfile"
    "campaigns-api"    = "src/ConexaoSolidaria.Campaigns.Api/Dockerfile"
    "donations-worker" = "src/ConexaoSolidaria.Donations.Worker/Dockerfile"
    "gateway"          = "src/ConexaoSolidaria.Gateway/Dockerfile"
    "web"              = "src/ConexaoSolidaria.Web/Dockerfile"
}

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

# --- 1) Login no Docker Hub (token via stdin, nunca em disco) ----------------
if (-not $SkipLogin) {
    if ([string]::IsNullOrWhiteSpace($env:DOCKERHUB_TOKEN)) {
        throw "Defina o token antes de rodar: `$env:DOCKERHUB_TOKEN = '<PAT>'  (ou use -SkipLogin se ja fez docker login)."
    }
    Write-Step "docker login -u $User (token via --password-stdin)..."
    $env:DOCKERHUB_TOKEN | docker login -u $User --password-stdin
    if ($LASTEXITCODE -ne 0) { throw "docker login falhou para o usuario '$User'." }
}
else {
    Write-Step "Pulando login (-SkipLogin); usando a credencial ja salva."
}

# --- 2) Build + push das 5 imagens (contexto = raiz do repo) -----------------
Push-Location $repoRoot
try {
    foreach ($svc in $services.Keys) {
        $dockerfile = $services[$svc]
        $repo       = "$User/$Prefix$svc"
        $latest     = "$repo`:latest"

        Write-Step "Build $latest  ($dockerfile)"
        # Tagueia :latest sempre; adiciona -t <ExtraTag> no mesmo build se pedido.
        $buildArgs = @("build", "-f", $dockerfile, "-t", $latest)
        if ($ExtraTag) { $buildArgs += @("-t", "$repo`:$ExtraTag") }
        $buildArgs += "."
        docker @buildArgs
        if ($LASTEXITCODE -ne 0) { throw "docker build falhou para $svc" }

        Write-Step "Push $latest"
        docker push $latest
        if ($LASTEXITCODE -ne 0) { throw "docker push falhou para $latest" }

        if ($ExtraTag) {
            docker push "$repo`:$ExtraTag"
            if ($LASTEXITCODE -ne 0) { throw "docker push falhou para $repo`:$ExtraTag" }
        }
    }
}
finally { Pop-Location }

Write-Host ""
Write-Host "Publicado no Docker Hub ($User):" -ForegroundColor Green
foreach ($svc in $services.Keys) {
    Write-Host "  $User/$Prefix$svc`:latest" -ForegroundColor Green
}
Write-Host ""
Write-Host "Com o Keel rodando no cluster, os pods sao atualizados em ate ~1 min (pollSchedule)." -ForegroundColor DarkGray
Write-Host "Acompanhe: kubectl get pods -n conexao-solidaria -w   |   kubectl logs -n keel deploy/keel -f" -ForegroundColor DarkGray
