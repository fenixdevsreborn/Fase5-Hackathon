# =============================================================================
# forward.ps1 - Mantem os port-forwards da stack VIVOS, religando sozinhos.
#
# Por que existe: `kubectl port-forward` prende num POD, nao no Service. Quando o
# Keel detecta uma imagem nova no Docker Hub e recria o pod (ou apos um `kubectl
# rollout restart`), o forward morre com:
#     an error occurred forwarding 18088 -> 8080: ... No such container: <id>
#     error: lost connection to pod
# e sobra um processo kubectl zumbi: a porta local para de responder ("a maquina
# de destino as recusou ativamente") ate alguem religar na mao. Como o Keel roda
# o tempo todo, isso acontece a cada push de :latest.
#
# Solucao: cada forward roda dentro de um supervisor (um pwsh escondido) com um
# laco `while ($true) { kubectl port-forward ...; sleep }`. Quando o kubectl cai
# porque o pod sumiu, o laco religa em segundos, ja no pod novo. O mesmo laco
# cobre o caso de o Service ainda nao existir (ele apenas tenta de novo).
#
# Uso:
#   pwsh infra/k8s/forward.ps1            # sobe/religa os supervisores
#   pwsh infra/k8s/forward.ps1 -Status    # mostra o que esta no ar e testa as portas
#   pwsh infra/k8s/forward.ps1 -Stop      # encerra supervisores + kubectl filhos
#
# O up.ps1 chama este script no passo 8. O down.ps1 encerra tudo pelo arquivo de
# PIDs (os supervisores sao mortos ANTES dos kubectl - se fosse ao contrario, o
# supervisor religaria o filho recem-morto).
# =============================================================================
[CmdletBinding()]
param(
    # Apenas relata o estado atual (nao sobe nada).
    [switch]$Status,
    # Encerra os supervisores e os kubectl port-forward do namespace.
    [switch]$Stop,
    # Segundos entre a queda de um forward e a nova tentativa.
    [int]$RetrySeconds = 2
)

$ErrorActionPreference = "Stop"

$ns      = "conexao-solidaria"
$pidFile = Join-Path ([System.IO.Path]::GetTempPath()) "conexao-solidaria-portforward.pids"
$logDir  = Join-Path ([System.IO.Path]::GetTempPath()) "conexao-solidaria-pf-logs"

# Marcador gravado na linha de comando de cada supervisor. A varredura de limpeza
# procura POR ELE, e nao por 'port-forward'+namespace: qualquer shell cujo comando
# apenas MENCIONE essas palavras (um `kubectl port-forward ...` digitado na mao,
# ou o proprio terminal que chamou este script) seria morto junto.
$marker = "CONEXAO_SOLIDARIA_PF_SUPERVISOR"

# Mesma tabela do up.ps1. Health e a URL usada por -Status para provar que a
# porta responde de verdade (processo vivo nao garante forward funcionando).
$forwards = @(
    @{ Svc = "web";           Map = "18088:80";    Desc = "App (Blazor)";              Health = "http://localhost:18088" }
    @{ Svc = "gateway";       Map = "18080:80";    Desc = "API via Gateway (Postman)"; Health = "http://localhost:18080/api/campanhas/search" }
    @{ Svc = "identity-api";  Map = "18081:80";    Desc = "Swagger Identity";          Health = "http://localhost:18081/swagger/index.html" }
    @{ Svc = "campaigns-api"; Map = "18082:80";    Desc = "Swagger Campaigns";         Health = "http://localhost:18082/swagger/index.html" }
    @{ Svc = "grafana";       Map = "3000:3000";   Desc = "Grafana";                   Health = "http://localhost:3000/login" }
    @{ Svc = "prometheus";    Map = "9090:9090";   Desc = "Prometheus";                Health = "http://localhost:9090/-/ready" }
    @{ Svc = "rabbitmq";      Map = "15672:15672"; Desc = "RabbitMQ Management";       Health = "http://localhost:15672" }
    @{ Svc = "zabbix-web";    Map = "8085:8080";   Desc = "Zabbix";                    Health = "http://localhost:8085" }
)

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

# PIDs que NUNCA podem ser mortos: este processo e toda a sua linha de ancestrais
# (o terminal que chamou o script). Sem isso, um shell cuja linha de comando cite o
# marcador se mata ao rodar -Stop - foi exatamente o que aconteceu no teste.
function Get-SelfAndAncestors {
    $ids = @()
    $current = $PID
    for ($i = 0; $i -lt 12 -and $current; $i++) {
        $ids += $current
        $proc = Get-CimInstance Win32_Process -Filter "ProcessId = $current" -ErrorAction SilentlyContinue
        if (-not $proc) { break }
        $current = $proc.ParentProcessId
    }
    return $ids
}

# Mata primeiro os supervisores, depois os kubectl. Nessa ordem: se o kubectl
# morresse antes, seu supervisor o religaria no mesmo instante.
function Stop-Forwards {
    $protected = Get-SelfAndAncestors

    if (Test-Path $pidFile) {
        Get-Content $pidFile | Where-Object { $_ } | ForEach-Object {
            $target = [int]$_
            if ($protected -notcontains $target) {
                Stop-Process -Id $target -Force -ErrorAction SilentlyContinue
            }
        }
        Remove-Item $pidFile -ErrorAction SilentlyContinue
    }

    # Varredura de seguranca: supervisores de execucoes antigas cujo PID se perdeu.
    # Casa pelo $marker, presente so na linha de comando dos supervisores.
    Get-CimInstance Win32_Process -Filter "Name = 'pwsh.exe' OR Name = 'powershell.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -match $marker -and $protected -notcontains $_.ProcessId } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

    # Os kubectl filhos ficam orfaos quando o supervisor morre; aqui eles caem.
    Get-CimInstance Win32_Process -Filter "Name = 'kubectl.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -match 'port-forward' -and $_.CommandLine -match [regex]::Escape($ns) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

if ($Stop) {
    Write-Step "Encerrando supervisores e port-forwards..."
    Stop-Forwards
    Write-Host "Port-forwards encerrados." -ForegroundColor Green
    return
}

if ($Status) {
    Write-Step "Estado dos port-forwards (namespace $ns):"
    foreach ($f in $forwards) {
        $alive = @(Get-CimInstance Win32_Process -Filter "Name = 'kubectl.exe'" -ErrorAction SilentlyContinue |
            Where-Object { $_.CommandLine -match 'port-forward' -and $_.CommandLine -match "svc/$($f.Svc) " }).Count -gt 0
        try {
            $code = (Invoke-WebRequest -Uri $f.Health -UseBasicParsing -TimeoutSec 10).StatusCode
            $http = "HTTP $code"; $color = "Green"
        }
        catch {
            $http = "sem resposta"; $color = "Red"
        }
        Write-Host ("    {0,-14} {1,-28} kubectl: {2,-6} {3}" -f `
            $f.Svc, $f.Desc, $(if ($alive) { "sim" } else { "nao" }), $http) -ForegroundColor $color
    }
    Write-Host ""
    Write-Host "Logs: $logDir   PIDs dos supervisores: $pidFile" -ForegroundColor DarkGray
    return
}

# --- Sobe os supervisores ----------------------------------------------------
Write-Step "Encerrando port-forwards anteriores (evita 'address already in use')..."
Stop-Forwards

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

# pwsh 7 e o padrao do repo; cai para o Windows PowerShell se nao estiver no PATH.
$shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { "pwsh" } else { "powershell" }

Write-Step "Subindo os port-forwards com religamento automatico..."
$pfPids = @()
foreach ($f in $forwards) {
    $logPath = Join-Path $logDir "$($f.Svc).log"

    # Laco do supervisor. *>> junta stdout e stderr no mesmo log (o motivo da queda
    # - 'No such container', 'connection refused' - fica registrado antes do retry).
    # A atribuicao morta com o $marker existe para aparecer na linha de comando do
    # processo: e por ela que Stop-Forwards identifica os supervisores.
    $loop = @"
`$supervisor = '$marker/$($f.Svc)'
`$ErrorActionPreference = 'SilentlyContinue'
while (`$true) {
    kubectl port-forward -n $ns svc/$($f.Svc) $($f.Map) *>> '$logPath'
    Start-Sleep -Seconds $RetrySeconds
}
"@

    $p = Start-Process -FilePath $shell `
             -ArgumentList @("-NoProfile", "-NoLogo", "-Command", $loop) `
             -WindowStyle Hidden -PassThru
    $pfPids += $p.Id
    Write-Host ("    {0,-28} {1,-14} {2}" -f $f.Desc, "svc/$($f.Svc)", $f.Map) -ForegroundColor Green
}

$pfPids | Set-Content -Path $pidFile

Write-Host ""
Write-Host "Supervisores no ar: um pod recriado pelo Keel volta sozinho em ~$RetrySeconds s." -ForegroundColor Green
Write-Host "  Conferir:  pwsh infra/k8s/forward.ps1 -Status" -ForegroundColor DarkGray
Write-Host "  Encerrar:  pwsh infra/k8s/forward.ps1 -Stop   (ou infra/k8s/down.ps1)" -ForegroundColor DarkGray
Write-Host "  Logs:      $logDir" -ForegroundColor DarkGray
