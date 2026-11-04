# Career Hunter

Plataforma pessoal de pesquisa de vagas para um candidato sênior em .NET: coleta fontes
públicas, normaliza as oportunidades, avalia a compatibilidade (motor determinístico e,
opcionalmente, IA) e monta um relatório diário por e-mail.

**Política inviolável:** o sistema nunca envia candidatura. Toda candidatura é preparada para
revisão e submissão manual pelo candidato, usando o link original da vaga.

- .NET 10 (ASP.NET Core Minimal API + Worker)
- PostgreSQL 17
- Docker Compose
- Frontend Angular: **não implementado** (ver `frontend/README.md`)

## Estrutura

| Projeto | Responsabilidade |
| --- | --- |
| `src/CareerHunter.Domain` | Agregados, value objects e regras de negócio. Não referencia nada. |
| `src/CareerHunter.Application` | Casos de uso, contratos, política de pontuação, motores de avaliação. Referencia apenas Domain. |
| `src/CareerHunter.Infrastructure` | EF Core/PostgreSQL, repositórios, queries de leitura, adaptadores (IA, SMTP, RSS). |
| `src/CareerHunter.Api` | Endpoints HTTP, Problem Details, health checks, autenticação por API key. |
| `src/CareerHunter.Worker` | Pipeline diário: coletar → normalizar → avaliar → digest → e-mail. |
| `tests/` | Domain (12), Application (10), Infrastructure (1 teste de integração ponta a ponta). |

Regra de dependência: `Domain ← Application ← Infrastructure ← {Api, Worker}`. Nenhum projeto
referencia `Api` ou `Worker`.

## Pré-requisitos

- .NET SDK 10
- Docker Desktop (ou Docker Engine + Compose) para o PostgreSQL e para o deploy completo

## 1. Subir tudo com Docker Compose

```bash
cp .env.example .env
# edite .env: POSTGRES_PASSWORD e API_KEY
docker compose up --build
```

- API: `http://127.0.0.1:5080`
- PostgreSQL: `127.0.0.1:5432` (binding local)
- Worker: sem porta, roda o pipeline a cada `WORKER_CYCLE_MINUTES`

As migrações são aplicadas automaticamente no start (API e Worker), com um advisory lock do
PostgreSQL para impedir migrações concorrentes.

### Ver o banco de dados (pgAdmin)

O pgAdmin fica em um profile opcional, então **não** sobe junto com o stack padrão:

```bash
docker compose --profile tools up -d pgadmin    # http://localhost:5050
```

- Login: `PGADMIN_DEFAULT_EMAIL` / `PGADMIN_DEFAULT_PASSWORD` do `.env`.
- O servidor **Career Hunter (local)** já vem cadastrado (`infra/pgadmin/servers.json`):
  host `postgres`, porta `5432`, banco `careerhunter`, usuário `careerhunter`.
- Ao clicar no servidor, informe a `POSTGRES_PASSWORD` (a senha não fica em arquivo).
- A porta é publicada apenas em `127.0.0.1`: a UI lê todos os dados do candidato.

Alternativa: qualquer cliente instalado no Windows (pgAdmin, DBeaver, DataGrip) conecta em
`localhost:5432` com as mesmas credenciais.

### Autenticação

Quando `API_KEY` está definido, todas as rotas `/api/*` exigem o header `X-Api-Key`. As rotas
`/`, `/health` e `/health/database` permanecem abertas para monitoração.

```bash
curl -s http://127.0.0.1:5080/api/jobs -H "X-Api-Key: $API_KEY"
```

Com `API_KEY` vazio a API fica aberta (aceitável apenas em execução local); o processo registra
um warning no start. O Compose publica a porta somente em `127.0.0.1`; em VPS use um reverse
proxy com TLS/autenticação antes de expor publicamente.

## 2. Executar API e Worker localmente

Se você só quer o PostgreSQL rodando em Docker (sem instalar nada no Windows), suba apenas o
serviço de banco:

```bash
docker compose up -d postgres
```

Usuário, banco e senha vêm de `POSTGRES_USER`, `POSTGRES_DB` e `POSTGRES_PASSWORD` no `.env`.
A connection string correspondente é
`Host=localhost;Port=5432;Database=careerhunter;Username=careerhunter;Password=<POSTGRES_PASSWORD>`.

```bash
export ConnectionStrings__CareerHunter="Host=localhost;Port=5432;Database=careerhunter;Username=careerhunter;Password=SUA_SENHA"
export Api__ApiKey="uma-chave-local"

dotnet run --project src/CareerHunter.Api
dotnet run --project src/CareerHunter.Worker   # em outro terminal
```

No PowerShell use `$env:ConnectionStrings__CareerHunter = "..."`.

A API local responde em `http://localhost:5080` (definido em
`src/CareerHunter.Api/Properties/launchSettings.json`, sem abrir o browser).

### Migrações

```bash
dotnet ef migrations add NomeDaMigracao \
  --project src/CareerHunter.Infrastructure \
  --startup-project src/CareerHunter.Api \
  --output-dir Persistence/Migrations
```

## 4. Fontes de vagas

O sistema só coleta o que a fonte permite: **API pública documentada**, **GET anônimo em feed
público (RSS/Atom)** ou **registro manual**. Não há login automatizado, scraping de páginas
protegidas, contorno de CAPTCHA ou rate-limit.

| Provedor | `kind` | Credencial | Escopo |
| --- | --- | --- | --- |
| Adzuna | `Adzuna` | `JobProviders__Adzuna__AppId` + `__AppKey` | Busca por país (`br`), termo e cidade. |
| Jooble | `Jooble` | `JobProviders__Jooble__ApiKey` | Busca por palavra-chave e local; o host vem da URL da fonte (`br.jooble.org` ou `jooble.org`). |
| Greenhouse | `Greenhouse` | nenhuma | Board público de uma empresa (`boards-api.greenhouse.io`). |
| Lever | `Lever` | nenhuma | Board público de uma empresa (`api.lever.co`). |
| LinkedIn | `Manual` | — | Sem API pública de vagas; os termos proíbem coleta automatizada. |
| Indeed | `Manual` | — | API de vagas descontinuada/restrita a parceiros; scraping proibido. |
| Glassdoor, InfoJobs, Gupy | `Manual` | — | Sem API pública; registre a vaga pelo link original. |
| Feeds RSS/Atom | `Rss` | nenhuma | Somente feeds públicos. |

As chaves ficam **apenas em configuração** (`JobProviders__...`), nunca no banco: a URL da fonte é
guardada e a credencial é anexada somente na requisição de saída. No Jooble a chave viaja no path;
o adapter monta `{scheme}://{host}/api/{key}` a partir do host já validado da fonte, então
`https://br.jooble.org/api/?keywords=...&location=...` usa o front brasileiro e
`https://jooble.org/api/?...` usa o global.

Board de empresa (Greenhouse/Lever) devolve **todas** as vagas abertas. O filtro de ingestão
(`appsettings.json` → `JobProviders:Filter`) descarta o que não interessa antes de gravar:
`TitleKeywords` vale para todos os provedores; `LocationKeywords` só para boards de empresa (as
buscas de Adzuna e Jooble já são restritas pelo provedor). Lista vazia desliga o filtro.
Esse filtro é o **padrão para fontes sem candidato**.

### Busca dinâmica por candidato

Cada fonte pode ser vinculada a um candidato (`candidateProfileId` no cadastro). Nesse caso a
busca é montada **a cada coleta** a partir do perfil, não de uma URL fixa:

| Campo do perfil | Uso |
| --- | --- |
| `careerTargets` (primário primeiro) | termo principal enviado ao provedor + palavras do título |
| `skills` técnicas/domínio (top 6) | termos extras da busca + filtro de título |
| `headline` | vocabulário adicional (cobre o título em inglês, ex. "Software Engineer") |
| `location` + `workPreferences.acceptsRemote` | país quando aceita remoto; cidade/UF quando não |

- **Adzuna/Jooble** (busca no servidor): `what`/`keywords` e `where`/`location` vêm do plano; o
  `what` fixo da URL deixa de valer. No Adzuna o cargo vai em `what` e as skills em `what_or`,
  porque a API aplica AND entre as palavras de um mesmo parâmetro.
- **Greenhouse/Lever/RSS** (devolvem o catálogo inteiro): o filtro de título usa as palavras do
  plano; `LocationKeywords` global continua valendo como escopo de país.
- Fonte **sem** candidato segue compartilhada e usa `JobProviders:Filter`.

Alterar o perfil (cargo-alvo, skill, cidade, aceita remoto) muda a próxima coleta sem editar a
fonte. Coletar apenas um candidato:

```bash
curl -X POST "http://localhost:5080/api/job-sources/collect?candidateProfileId=<CANDIDATE_ID>&force=true" \
  -H "X-Api-Key: $API_KEY"
```

Registrar e coletar um board público (sem chave):

```bash
curl -X POST http://localhost:5080/api/job-sources -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" -d '{
    "name": "Greenhouse - C6 Bank",
    "baseUrl": "https://boards-api.greenhouse.io/v1/boards/c6bank/jobs?content=true",
    "kind": "Greenhouse",
    "accessPolicyNotes": "API publica de job board; sem chave.",
    "minimumIntervalMinutes": 720,
    "candidateProfileId": "<CANDIDATE_ID>" }'

curl -X POST "http://localhost:5080/api/job-sources/collect?sourceId=<SOURCE_ID>&force=true" \
  -H "X-Api-Key: $API_KEY"
curl -X POST "http://localhost:5080/api/catalog/normalize?take=500" -H "X-Api-Key: $API_KEY"
```

Fonte que ainda depende de credencial pode ficar registrada e **pausada**:

```bash
curl -X PUT http://localhost:5080/api/job-sources/<SOURCE_ID>/enabled \
  -H "X-Api-Key: $API_KEY" -H "Content-Type: application/json" -d '{ "enabled": false }'
```

O Worker ignora fontes desabilitadas. Para Adzuna e Jooble, preencha as chaves no `.env`
(`ADZUNA_APP_ID`, `ADZUNA_APP_KEY`, `JOOBLE_API_KEY`), recrie os containers e reative a fonte.

Registro manual (LinkedIn, Indeed, Gupy e afins):

```bash
curl -X POST http://localhost:5080/api/job-sources -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" -d '{
    "name": "LinkedIn", "baseUrl": "https://www.linkedin.com/jobs/", "kind": "Manual",
    "accessPolicyNotes": "Registro manual a partir do anúncio público.",
    "minimumIntervalMinutes": 1440 }'

curl -X POST http://localhost:5080/api/job-sources/<SOURCE_ID>/records -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" -d '{
    "jobSourceId": "<SOURCE_ID>", "externalId": "linkedin-123",
    "sourceUrl": "https://www.linkedin.com/jobs/view/123",
    "title": "Engenheiro .NET Sênior", "company": "Empresa X", "location": "Remoto",
    "description": "Descrição original completa da vaga..." }'
```

Fontes `Manual` são um no-op na coleta automática do Worker: elas nunca falham nem geram
requisições para o portal.

## 5. Configuração de IA e e-mail (ambas opcionais)

Sem `AI_API_KEY` (ou `Ai__ApiKey`), a avaliação usa somente o motor determinístico.
Sem `SMTP_HOST`, o envio de e-mail usa um sender de log (nada sai do processo).

| Variável | Efeito |
| --- | --- |
| `AI_API_KEY`, `AI_BASE_URL`, `AI_MODEL` | Provider compatível com OpenAI chat completions. |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USE_STARTTLS`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM` | Envio do digest. `SMTP_USE_STARTTLS=true` exige TLS. |
| `Worker__UseAi` | `true` permite IA no pipeline; o fallback determinístico continua ativo. |
| `Worker__CycleIntervalMinutes` | Intervalo do pipeline (mínimo 1). |

## 6. Testes

```bash
dotnet build CareerHunter.sln
dotnet test CareerHunter.sln
```

Status atual: build 0 warnings / 0 erros; 24 testes aprovados (12 Domain, 10 Application,
2 de integração com repositórios, queries, EF Core e pipeline completo em SQLite).

## 7. Endpoints (resumo)

`docs/delivery-report.md` traz a lista completa. Principais grupos:

- `GET/POST /api/candidates`, `.../skills`, `.../experiences`, `.../educations`,
  `.../certifications`, `.../career-targets`, `.../preferences`
- `GET/POST /api/job-sources`, `POST /api/job-sources/collect`, `POST /api/job-sources/{id}/records`
- `POST /api/catalog/normalize`
- `GET /api/jobs`, `GET /api/jobs/{id}`, `GET /api/jobs/{id}/evaluations`
- `POST /api/evaluations`, `GET /api/evaluations/{id}`
- `GET/POST /api/applications`, `PUT /api/applications/{id}/status`,
  `POST /api/applications/{id}/prepare-package`
- `PUT /api/candidates/{id}/notification-preference`
- `GET/POST /api/digests`, `POST /api/digests/{id}/send`, `GET /api/digests/{id}/preview`

A prévia renderiza o e-mail exatamente como ele sairia, sem enviar nada:
`GET /api/digests/{id}/preview` (HTML), `?format=text` ou `?format=json`.

Marcar uma candidatura como `Applied` exige `userConfirmed=true` e não dispara nenhuma ação
externa; o status apenas registra algo que o candidato já fez manualmente.

## 8. Limitações conhecidas

- Frontend Angular ainda não foi gerado (Angular CLI indisponível no ambiente de build).
- O daemon Docker foi usado para validar o PostgreSQL e o pgAdmin; as imagens de `api` e `worker`
  ainda não foram construídas nem executadas em container.
- A coleta suporta apenas `manual` e RSS/Atom públicos; portais com autenticação, CAPTCHA ou
  scraping protegido estão fora da política (LinkedIn, Gupy, Indeed, Glassdoor e InfoJobs entram
  como `Manual`).
- Heurísticas de normalização (senioridade, modalidade, localização) podem errar; campos
  inferidos nunca são apresentados como dados confirmados da vaga.
- Sem versionamento de concorrência otimista: escritas simultâneas no mesmo agregado seguem
  last-write-wins.
