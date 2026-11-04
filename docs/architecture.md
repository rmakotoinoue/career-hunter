# Arquitetura

Documento curto. O detalhamento completo está em `docs/delivery-report.md`; o diagnóstico inicial
está em `docs/architecture-assessment.md`.

## Estilo

Monólito modular com cinco projetos e uma única base PostgreSQL:

```
Domain ← Application ← Infrastructure ← Api
                                      ← Worker
```

- `Domain`: agregados, value objects, invariantes, política de pontuação. Não referencia nada.
- `Application`: casos de uso explícitos, contratos, motores de avaliação. Referencia apenas Domain.
- `Infrastructure`: EF Core/Npgsql, repositórios, queries de leitura, adaptadores externos.
- `Api`: endpoints, Problem Details, health, autenticação por API key. Sem regra de negócio.
- `Worker`: pipeline diário (coletar → normalizar → avaliar → digest → e-mail).

## Decisões

- Monólito modular: menor custo operacional que microserviços para um único usuário.
- API e Worker separados para isolar tráfego HTTP de processamento em segundo plano.
- Referências entre contextos são apenas ids, sem navegações EF; leitura cruzada fica nas queries.
- Sem MediatR, CQRS, event sourcing, barramento de eventos ou repositório genérico.
- PostgreSQL como persistência; migrações aplicadas no start com advisory lock.
- IA é opcional e consultiva: motor determinístico é o padrão e o fallback; o agregado nunca aceita
  a pontuação geral vinda do modelo.
- Candidaturas permanecem sob revisão e envio manual; nenhuma ação externa é executada.

## Contextos

1. CandidateProfile — perfil, competências, experiências, formação, certificações, alvos e
   preferências.
2. JobDiscovery — fontes (`manual`, RSS/Atom público), execuções de coleta e registros brutos.
3. JobCatalog — empregadores, vagas normalizadas, requisitos e proveniência.
4. JobAssessment — avaliações versionadas com dimensões, requisitos, evidências e histórico.
5. ApplicationTracking — candidaturas, histórico de status, materiais e notas.
6. Notification — preferências, digest diário e tentativas de entrega.
