# =============================================================================
# down.ps1 - Derruba a stack do Conexao Solidaria no Kubernetes local.
#
# Uso:
#   pwsh infra/k8s/down.ps1              # remove os recursos do overlay local
#   pwsh infra/k8s/down.ps1 -PurgeData   # tambem apaga PVCs e o namespace (perde dados)
#
# Por padrao, os PVCs de Postgres/RabbitMQ (volumeClaimTemplates) NAO sao
# removidos - proposital, para nao perder dados por engano. O mesmo vale para o
# PVC 'web-dataprotection-keys': ele fica fora do overlay (ver
# base/web-dataprotection-pvc.yaml), entao o `delete -k` abaixo nao o alcanca e
# os JWTs no localStorage dos browsers continuam validos apos o proximo up.
# Só o -PurgeData apaga todos eles.
# =============================================================================
[CmdletBinding()]
param(
    [switch]$PurgeData
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$overlay   = Join-Path $scriptDir "overlays/local"
$ns        = "conexao-solidaria"

# Encerra os port-forwards que o up.ps1 deixou em segundo plano. Delegado ao
# forward.ps1 -Stop porque agora cada forward tem um supervisor que o religa: matar
# so o kubectl.exe (como era antes) fazia o supervisor recria-lo em seguida. O
# -Stop mata os supervisores primeiro e so depois os kubectl filhos.
Write-Host "==> Encerrando port-forwards em segundo plano..." -ForegroundColor Cyan
& (Join-Path $scriptDir "forward.ps1") -Stop

Write-Host "==> Removendo a stack (kubectl delete -k)..." -ForegroundColor Cyan
kubectl delete -k $overlay --ignore-not-found

if ($PurgeData) {
    Write-Host "==> Apagando PVCs e o namespace (perde dados)..." -ForegroundColor Yellow
    kubectl delete pvc -n $ns --all --ignore-not-found
    kubectl delete namespace $ns --ignore-not-found
}
else {
    Write-Host "PVCs preservados (Postgres/RabbitMQ + Data Protection keys do Web)." -ForegroundColor DarkGray
    Write-Host "Para apagar tudo:" -ForegroundColor DarkGray
    Write-Host "  pwsh infra/k8s/down.ps1 -PurgeData" -ForegroundColor DarkGray
}
