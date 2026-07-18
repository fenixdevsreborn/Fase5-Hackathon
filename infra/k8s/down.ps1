# =============================================================================
# down.ps1 - Derruba a stack do Conexao Solidaria no Kubernetes local.
#
# Uso:
#   pwsh infra/k8s/down.ps1              # remove os recursos do overlay local
#   pwsh infra/k8s/down.ps1 -PurgeData   # tambem apaga PVCs e o namespace (perde dados)
#
# Por padrao, os PVCs de Postgres/RabbitMQ (volumeClaimTemplates) NAO sao
# removidos - proposital, para nao perder dados por engano.
# =============================================================================
[CmdletBinding()]
param(
    [switch]$PurgeData
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$overlay   = Join-Path $scriptDir "overlays/local"
$ns        = "conexao-solidaria"

# Encerra os port-forwards que o up.ps1 deixou em segundo plano (PIDs salvos +
# varredura por seguranca de kubectl port-forward deste namespace).
Write-Host "==> Encerrando port-forwards em segundo plano..." -ForegroundColor Cyan
$pidFile = Join-Path ([System.IO.Path]::GetTempPath()) "conexao-solidaria-portforward.pids"
if (Test-Path $pidFile) {
    Get-Content $pidFile | Where-Object { $_ } | ForEach-Object {
        Stop-Process -Id ([int]$_) -Force -ErrorAction SilentlyContinue
    }
    Remove-Item $pidFile -ErrorAction SilentlyContinue
}
Get-CimInstance Win32_Process -Filter "Name = 'kubectl.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -match 'port-forward' -and $_.CommandLine -match [regex]::Escape($ns) } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Write-Host "==> Removendo a stack (kubectl delete -k)..." -ForegroundColor Cyan
kubectl delete -k $overlay --ignore-not-found

if ($PurgeData) {
    Write-Host "==> Apagando PVCs e o namespace (perde dados)..." -ForegroundColor Yellow
    kubectl delete pvc -n $ns --all --ignore-not-found
    kubectl delete namespace $ns --ignore-not-found
}
else {
    Write-Host "PVCs de Postgres/RabbitMQ preservados. Para apagar tudo:" -ForegroundColor DarkGray
    Write-Host "  pwsh infra/k8s/down.ps1 -PurgeData" -ForegroundColor DarkGray
}
