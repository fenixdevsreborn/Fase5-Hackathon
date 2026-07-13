# Pull Request — Conexao Solidaria (Spec-Driven)

## Spec relacionada
<!-- Link para a spec/issue/ADR que motiva esta mudanca. -->
- Spec/Issue:

## O que mudou
<!-- Resumo objetivo da alteracao e do porque. -->
-

## Criterios de aceite
<!-- Como saber que esta pronto? Marque o que se aplica. -->
- [ ] Atende aos criterios de aceite da spec
- [ ] Sem breaking changes (ou documentados abaixo)

## Testes
- [ ] Testes unitarios (`tests/ConexaoSolidaria.Tests`)
- [ ] Testes de integracao (`tests/ConexaoSolidaria.IntegrationTests` — Testcontainers)
- [ ] Smoke test manual / end-to-end
- [ ] N/A (justifique):

## Observabilidade
- [ ] Metricas expostas/atualizadas quando relevante
- [ ] Logs estruturados adequados (sem dados sensiveis)
- [ ] Traces/spans (OpenTelemetry) cobrindo o novo fluxo
- [ ] N/A

## Seguranca
- [ ] Nenhum segredo/credencial commitado
- [ ] Autorizacao/RBAC verificada nos endpoints afetados
- [ ] Dados sensiveis tratados/mascarados corretamente
- [ ] N/A

## Evidencias
<!-- Prints, logs, saidas de teste, dashboards, etc. -->
-

## Checklist final
- [ ] `dotnet build -c Release` passa localmente
- [ ] `dotnet test` passa localmente
- [ ] CI verde (quality / tests / containers / kubernetes-validation)
