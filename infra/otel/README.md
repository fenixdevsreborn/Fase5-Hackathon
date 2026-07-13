# OpenTelemetry Collector - Conexao Solidaria

Ponto central de coleta de telemetria (OTLP) dos servicos .NET. O
`ServiceDefaults` (`src/ConexaoSolidaria.ServiceDefaults/Extensions.cs`) ja
configura tracing/metrics/logs via OpenTelemetry e liga o exporter OTLP **quando
a env `OTEL_EXPORTER_OTLP_ENDPOINT` esta definida**. Hoje, sem collector, apenas
o Aspire Dashboard local consome esses sinais. Este collector fecha essa lacuna
em compose/k8s e entrega as metricas ao Prometheus.

## Fluxo

```
Identity/Campaigns/Worker  --OTLP(4317/4318)-->  otel-collector  --/metrics(8889)-->  Prometheus
                                                       |
                                                       +-- debug (stdout)
```

- Metricas customizadas de negocio (`conexao_donations_processed_total`, etc.)
  continuam expostas via prometheus-net em `/metrics` de cada servico (nao
  migradas para OTel). O collector agrega as metricas OTel de instrumentacao
  (ASP.NET Core, HttpClient, runtime) e as traces.

## Portas

| Porta | Uso                                             |
|-------|-------------------------------------------------|
| 4317  | Receiver OTLP gRPC (endpoint dos servicos)      |
| 4318  | Receiver OTLP HTTP                              |
| 8889  | Exporter Prometheus (`/metrics` para scrape)    |
| 13133 | Health check do proprio collector               |

## Como plugar no docker-compose

Adicione o servico (outro agente aplica a mudanca no `docker-compose.yml`):

```yaml
  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    command: ["--config=/etc/otel/otel-collector-config.yaml"]
    volumes:
      - ./infra/otel/otel-collector-config.yaml:/etc/otel/otel-collector-config.yaml:ro
    ports:
      - "4317:4317"
      - "4318:4318"
      - "8889:8889"
```

E em cada servico da aplicacao (identity-api, campaigns-api, donations-worker),
acrescente a env:

```yaml
    environment:
      OTEL_EXPORTER_OTLP_ENDPOINT: http://otel-collector:4317
```

## Como plugar no Kubernetes

Crie um Deployment + Service `otel-collector` (imagem
`otel/opentelemetry-collector-contrib`) montando este arquivo via ConfigMap em
`/etc/otel/otel-collector-config.yaml`, e exponha as portas 4317/4318/8889.
Adicione a env `OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317` aos
deployments `identity-api`, `campaigns-api` e `donations-worker`.

## Job Prometheus sugerido (NAO editar aqui)

O `prometheus.yml` e responsabilidade de outro agente. Sugestao de job para
raspar as metricas OTel agregadas pelo collector:

```yaml
  - job_name: otel-collector
    metrics_path: /metrics
    static_configs:
      - targets: ["otel-collector:8889"]
```

## Validacao

- Health do collector: `curl http://localhost:13133` (deve responder 200).
- Metricas agregadas: `curl http://localhost:8889/metrics`.
- Logs do container mostram os batches (exporter `debug`).
