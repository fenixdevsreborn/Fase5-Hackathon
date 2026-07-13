# Smoke test #K8S-011: aplica o overlay local, aguarda o rollout dos Deployments
# e dos StatefulSets, e imprime o estado dos pods.
#
# Uso:
#   pwsh infra/k8s/smoke.ps1
#
# Pre-requisitos:
#   1. Kubernetes habilitado no Docker Desktop  (kubectl config use-context docker-desktop)
#   2. Imagens locais construidas (ver README.md, secao "Build das imagens")
#   3. Secret aplicado:  kubectl apply -f infra/k8s/secret.yaml

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$overlay = Join-Path $scriptDir "overlays/local"
$ns = "conexao-solidaria"

Write-Host "==> Validando o overlay (kubectl kustomize)..." -ForegroundColor Cyan
kubectl kustomize $overlay | Out-Null

Write-Host "==> Verificando o Secret '$ns/conexao-solidaria-secret'..." -ForegroundColor Cyan
try {
    kubectl get secret conexao-solidaria-secret -n $ns -o name | Out-Null
}
catch {
    Write-Warning "Secret 'conexao-solidaria-secret' nao encontrado no namespace '$ns'."
    Write-Warning "Crie-o antes de aplicar:  kubectl apply -f infra/k8s/secret.yaml"
    throw
}

Write-Host "==> Aplicando o overlay local (kubectl apply -k)..." -ForegroundColor Cyan
kubectl apply -k $overlay

$deployments = @(
    "gateway", "web", "identity-api", "campaigns-api", "donations-worker",
    "elasticsearch", "prometheus", "grafana", "zabbix-server", "zabbix-web"
)
$statefulsets = @("postgres", "rabbitmq")

Write-Host "==> Aguardando rollout dos StatefulSets..." -ForegroundColor Cyan
foreach ($s in $statefulsets) {
    kubectl rollout status "statefulset/$s" -n $ns --timeout=300s
}

Write-Host "==> Aguardando rollout dos Deployments..." -ForegroundColor Cyan
foreach ($d in $deployments) {
    kubectl rollout status "deployment/$d" -n $ns --timeout=300s
}

Write-Host "==> Estado final dos pods:" -ForegroundColor Cyan
kubectl get pods -n $ns -o wide

Write-Host "==> Services:" -ForegroundColor Cyan
kubectl get svc -n $ns

Write-Host "==> Smoke test concluido." -ForegroundColor Green
