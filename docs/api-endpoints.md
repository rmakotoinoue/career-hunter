# API do Career Hunter — endpoints e cURLs

Documento gerado a partir do código e **validado por execução real** contra a API local em
05/11/2026 (cada rota abaixo foi chamada e o status de resposta conferido). Os exemplos usam
`$API_URL` e `$API_KEY`, então funcionam igual no local, em produção e por túnel SSH.

---

## 1. Como chamar

```bash
# local (docker compose publica em 127.0.0.1:5080)
export API_URL=http://127.0.0.1:5080
export API_KEY="sua-chave"          # mesma do .env (chave API_KEY)

# produção em VPS: a porta só escuta em 127.0.0.1, use túnel SSH ou reverse proxy
# ssh -L 5080:127.0.0.1:5080 usuario@seu-vps
# export API_URL=http://127.0.0.1:5080

curl -s "$API_URL/api/jobs?pageSize=2" -H "X-Api-Key: $API_KEY"
```

**Prefere Postman?** Importe [`postman/career-hunter.postman_collection.json`](../postman/career-hunter.postman_collection.json)
(File → Import): 53 requests em 7 pastas, na mesma ordem deste documento. A collection declara
`api_key` e as demais variáveis (`candidate_id` já vem preenchido com o perfil do Rafael;
`job_id`, `source_id`, `evaluation_id`, `application_id` e `digest_id` você cola conforme as
respostas). Cada request tem na aba **Testes** a asserção do código de status documentado aqui.

Regras gerais:

| Item | Comportamento |
| --- | --- |
| Autenticação | Toda rota `/api/*` exige `X-Api-Key`. Sem a chave (ou com chave errada) a resposta é `401`. |
| Rotas abertas | `/`, `/health`, `/health/database` — sem chave, para monitoração. |
| JSON | `Content-Type: application/json` no corpo de `POST`/`PUT`. |
| Enums | Enviados **como texto e exatamente como no código** (`Remote`, `Senior`, `Clt`…). Minúsculas em parâmetro de query retornam `400`. |
| Data/hora | ISO 8601 com fuso (`2026-11-04T13:54:44Z`). Datas sem hora (`startDate`) no formato `AAAA-MM-DD`. |
| Erros | RFC 7807 (`application/problem+json`) com `title`, `status`, `detail` e `traceId`. |
| Criação | `201` com corpo `{"id":"..."}`. |
| Atualização | `204` sem corpo (exceto `PUT /status` e `PUT /notification-preference`, que respondem `200` com JSON). |

### Aviso para quem testa no PowerShell (Windows)

O corpo JSON entre aspas simples (`-d '{...}'`) é repassado ao `curl.exe` com as aspas internas
alteradas pelo PowerShell 5.1 e o servidor responde `400 Malformed request` — o comando está certo,
o problema é o shell. Testei os caminhos abaixo em 05/11/2026 (mesmo corpo, mesma API):

| Como você chama | Resultado |
| --- | --- |
| Git Bash / bash / WSL com `-d '{...}'` | funciona (verificado: `404` no corpo parseado) |
| PowerShell 5.1 com `-d '{...}'` | `400` Malformed request (JSON chega quebrado) |
| PowerShell 5.1 com `--data-binary "@corpo.json"` | funciona (verificado) |
| PowerShell 5.1 com `Invoke-RestMethod -Body $json` | funciona (verificado) |
| cmd.exe direto no prompt | não testado aqui — em dúvida, use `--data-binary` |

No PowerShell, salve o corpo em arquivo e use `--data-binary`:

```powershell
@{ candidateProfileId = $CANDIDATE_ID; jobOpportunityId = $JOB_ID; preference = 'RuleBased'; force = $false } |
  ConvertTo-Json -Compress | Set-Content .\corpo.json -Encoding UTF8

curl.exe -X POST "$API_URL/api/evaluations" `
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' `
  --data-binary "@.\corpo.json"
```

### Códigos de status

Os três primeiros e o `409` foram reproduzidos; `422` e `503` vêm do handler de exceções.

| Código | Quando |
| --- | --- |
| `400` | JSON malformado, parâmetro que não converte (`modality=remote`), enum inválido no corpo. |
| `401` | Falta ou não confere `X-Api-Key`. |
| `404` | Id inexistente (`title: "Resource not found"`). |
| `409` | Regra de negócio violada — ex.: `Applied` sem `userConfirmed`, transição de status inválida, notificação desligada. |
| `422` | Resposta da IA fora do contrato de avaliação (`AiEvaluationValidationException`; caminho de código, não reproduzido manualmente). |
| `503` | `/health/database` sem conexão com o PostgreSQL (código do handler; o caso `200` foi testado). |

---

## 2. Visão geral

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/` | Identificação da API e política de candidatura manual |
| GET | `/health` | Liveness |
| GET | `/health/database` | Readiness do PostgreSQL |
| GET | `/api/candidates` | Lista perfis |
| GET | `/api/candidates/{id}` | Detalhe do perfil (com skills, experiências, alvos) |
| POST | `/api/candidates` | Cria perfil |
| PUT | `/api/candidates/{id}` | Atualiza dados do perfil |
| PUT | `/api/candidates/{id}/preferences` | Preferências de trabalho + expectativa salarial |
| POST | `/api/candidates/{id}/experiences` | Adiciona experiência |
| POST | `/api/candidates/{id}/skills` | Adiciona skill |
| DELETE | `/api/candidates/{id}/skills/{skillId}` | Remove skill |
| POST | `/api/candidates/{id}/educations` | Adiciona formação |
| POST | `/api/candidates/{id}/certifications` | Adiciona certificação |
| POST | `/api/candidates/{id}/career-targets` | Adiciona cargo-alvo |
| PUT | `/api/candidates/{id}/career-targets/{targetId}/primary` | Marca alvo como primário |
| PUT | `/api/candidates/{id}/notification-preference` | E-mail/hora/fuso/score mínimo do digest |
| GET | `/api/job-sources` | Lista fontes de coleta |
| POST | `/api/job-sources` | Registra fonte |
| PUT | `/api/job-sources/{id}/enabled` | Pausa/reanuda fonte |
| POST | `/api/job-sources/collect` | Executa coleta (`sourceId`, `candidateProfileId`, `force`) |
| GET | `/api/job-sources/runs` | Histórico de execuções (`sourceId`, `take`) |
| GET | `/api/job-sources/records` | Registros brutos (`sourceId`, `status`, `take`) |
| POST | `/api/job-sources/{id}/records` | Envia registro manual de vaga |
| POST | `/api/catalog/normalize` | Normaliza registros brutos em vagas (`take`) |
| GET | `/api/jobs` | Catálogo (`query`, `modality`, `contractType`, `seniority`, `onlyActive`, `page`, `pageSize`) |
| GET | `/api/jobs/{id}` | Detalhe da vaga |
| GET | `/api/jobs/{id}/evaluations` | Avaliações da vaga |
| POST | `/api/evaluations` | Avalia vaga para um candidato |
| GET | `/api/evaluations/{id}` | Detalhe da avaliação |
| GET | `/api/applications` | Lista candidaturas (`candidateProfileId`, `status`) |
| GET | `/api/applications/{id}` | Detalhe da candidatura |
| POST | `/api/applications` | Abre candidatura |
| POST | `/api/applications/{id}/materials` | Adiciona material (currículo, mensagem…) |
| POST | `/api/applications/{id}/notes` | Adiciona nota |
| PUT | `/api/applications/{id}/salary-expectation` | Registra expectativa salarial |
| PUT | `/api/applications/{id}/status` | Muda status (exige `userConfirmed` para `Applied`) |
| POST | `/api/applications/{id}/prepare-package` | Gera pacote de candidatura |
| GET | `/api/digests` | Lista digests (`candidateProfileId`, `take`) |
| GET | `/api/digests/{id}` | Detalhe do digest com itens |
| GET | `/api/digests/{id}/items` | Itens do digest **com o detalhe da vaga** (descrição, modalidade, salário, requisitos, fonte) |
| POST | `/api/digests` | Gera o digest do dia (idempotente) |
| POST | `/api/digests/{id}/send` | **Envia o e-mail de verdade** |
| GET | `/api/digests/{id}/preview` | Prévia do e-mail (`format=json\|text\|html`) |

---

## 3. Saúde e segurança

```bash
curl -s "$API_URL/"
curl -s "$API_URL/health"
curl -s "$API_URL/health/database"

# sem a chave -> 401
curl -i -s "$API_URL/api/jobs" 
# {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.2","title":"Unauthorized",
#  "status":401,"detail":"A valid X-Api-Key header is required.","traceId":"..."}

# com a chave -> 200
curl -s "$API_URL/api/jobs?pageSize=1" -H "X-Api-Key: $API_KEY"
```

---

## 4. Candidato

### Criar perfil

```bash
curl -s -X POST "$API_URL/api/candidates" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{
    "fullName": "Rafael Makoto Inoue",
    "email": "rmakotoinoue@gmail.com",
    "headline": "Tech Lead | Engenheiro de Software Sênior | .NET, Azure e AWS",
    "summary": "19 anos de tecnologia, liderança técnica e modernização de legado.",
    "city": "Mongaguá", "state": "SP", "country": "Brazil",
    "yearsOfExperience": 19,
    "linkedinUrl": "https://linkedin.com/in/rafael-makoto-inoue",
    "acceptsRemote": true, "acceptsHybrid": true, "acceptsOnsite": false,
    "acceptsClt": true, "acceptsPj": true,
    "expectedMinAmount": 9000, "expectedMaxAmount": 12000,
    "expectedPeriod": "Monthly", "expectedCurrency": "BRL"
  }'
# 201 {"id":"<CANDIDATE_ID>"}
```

`expectedPeriod` aceita `Monthly`, `Yearly`, `Daily`, `Hourly`.

### Ler e atualizar

```bash
curl -s "$API_URL/api/candidates" -H "X-Api-Key: $API_KEY"
curl -s "$API_URL/api/candidates/$CANDIDATE_ID" -H "X-Api-Key: $API_KEY"

curl -s -X PUT "$API_URL/api/candidates/$CANDIDATE_ID" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{
    "fullName": "Rafael Makoto Inoue",
    "headline": "Tech Lead | Engenheiro de Software Sênior | .NET, Azure e AWS",
    "summary": null, "city": "Mongaguá", "state": "SP", "country": "Brazil",
    "yearsOfExperience": 19, "linkedinUrl": null
  }'                                  # 204
```

### Preferências de trabalho e expectativa

```bash
curl -s -X PUT "$API_URL/api/candidates/$CANDIDATE_ID/preferences" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{
    "candidateProfileId": "'"$CANDIDATE_ID"'",
    "acceptsRemote": true, "acceptsHybrid": true, "acceptsOnsite": false,
    "acceptsClt": true, "acceptsPj": true,
    "expectedMinAmount": 9000, "expectedMaxAmount": 12000,
    "expectedPeriod": "Monthly", "expectedCurrency": "BRL",
    "clearCompensationExpectation": false
  }'                                  # 204
```

### Experiências, skills, formação, certificações, cargos-alvo

```bash
# experiences -> 201 {"id":"..."}
curl -s -X POST "$API_URL/api/candidates/$CANDIDATE_ID/experiences" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{
    "candidateProfileId": "'"$CANDIDATE_ID"'",
    "employer": "Ília", "title": "Tech Lead",
    "startDate": "2025-12-01", "endDate": null,
    "description": "Modernização de legado para Azure.",
    "technologies": ["C#",".NET","Azure"]
  }'

# skills -> 201 {"id":"<SKILL_ID>"}   category: Technical|Behavioral|Language|Tool|Domain
curl -s -X POST "$API_URL/api/candidates/$CANDIDATE_ID/skills" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{"candidateProfileId":"'"$CANDIDATE_ID"'","name":"C#","category":"Technical","proficiency":5,"yearsOfExperience":15}'

curl -s -X DELETE "$API_URL/api/candidates/$CANDIDATE_ID/skills/$SKILL_ID" \
  -H "X-Api-Key: $API_KEY"                                # 204

# educations -> 201
curl -s -X POST "$API_URL/api/candidates/$CANDIDATE_ID/educations" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{"candidateProfileId":"'"$CANDIDATE_ID"'","institution":"FIAP","degree":"Pós-graduação","fieldOfStudy":"Data Analytics","startDate":"2023-01-01","endDate":"2024-12-31"}'

# certifications -> 201
curl -s -X POST "$API_URL/api/candidates/$CANDIDATE_ID/certifications" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{"candidateProfileId":"'"$CANDIDATE_ID"'","name":"Azure Fundamentals","issuer":"Microsoft","issuedOn":"2024-01-15","expiresOn":null,"credentialUrl":null}'

# career-targets -> 201   seniority: Intern|Junior|MidLevel|Senior|Lead|Principal|Architect
curl -s -X POST "$API_URL/api/candidates/$CANDIDATE_ID/career-targets" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{"candidateProfileId":"'"$CANDIDATE_ID"'","jobTitle":"Tech Lead","seniority":"Lead","isPrimary":true,"notes":null}'

curl -s -X PUT "$API_URL/api/candidates/$CANDIDATE_ID/career-targets/$TARGET_ID/primary" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' -d '{}'   # 204
```

### Notificação do digest

```bash
curl -s -X PUT "$API_URL/api/candidates/$CANDIDATE_ID/notification-preference" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{
    "candidateProfileId": "'"$CANDIDATE_ID"'",
    "email": "rmakotoinoue@gmail.com",
    "isEnabled": true,
    "sendHourLocal": 8,
    "timeZoneId": "America/Sao_Paulo",
    "minimumScore": 55
  }'
# 200 {"id":"<PREFERENCE_ID>"}
```

---

## 5. Descoberta (fontes, coleta, normalização)

```bash
# lista as fontes com status da última coleta
curl -s "$API_URL/api/job-sources" -H "X-Api-Key: $API_KEY"

# registra uma fonte -> 201 {"id":"<SOURCE_ID>"}
# kind: Manual|Rss|Api|HtmlPage|Adzuna|Jooble|Greenhouse|Lever
curl -s -X POST "$API_URL/api/job-sources" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{
    "name": "Greenhouse - Minha Empresa",
    "baseUrl": "https://boards-api.greenhouse.io/v1/boards/minhaempresa/jobs?content=true",
    "kind": "Greenhouse",
    "accessPolicyNotes": "API pública do board, sem chave e sem scraping.",
    "minimumIntervalMinutes": 720,
    "candidateProfileId": "'"$CANDIDATE_ID"'"
  }'
```

`candidateProfileId: null` cria a fonte compartilhada (usa o filtro global `JobProviders:Filter`).
Com candidato, a busca é **remontada do perfil a cada coleta**.

```bash
# pausa / reanuda -> 204
curl -s -X PUT "$API_URL/api/job-sources/$SOURCE_ID/enabled" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' -d '{"enabled":false}'

# coleta: tudo do candidato, uma fonte, ou forçando ignorar o intervalo -> 200
curl -s -X POST "$API_URL/api/job-sources/collect?candidateProfileId=$CANDIDATE_ID&force=true" -H "X-Api-Key: $API_KEY"
curl -s -X POST "$API_URL/api/job-sources/collect?sourceId=$SOURCE_ID&force=true" -H "X-Api-Key: $API_KEY"
# 200 [{"collectionRunId":"...","jobSourceId":"...","jobSourceName":"...","status":"Succeeded",
#       "recordsCollected":20,"recordsSkipped":0,"recordsRejected":0}]

# histórico de execuções e registros brutos
curl -s "$API_URL/api/job-sources/runs?take=5" -H "X-Api-Key: $API_KEY"
curl -s "$API_URL/api/job-sources/records?take=10&status=Collected" -H "X-Api-Key: $API_KEY"
# status: Collected|Normalized|Rejected

# registro manual (portais sem API) -> 201 {"id":"..."}
curl -s -X POST "$API_URL/api/job-sources/$SOURCE_ID/records" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{
    "jobSourceId": "'"$SOURCE_ID"'",
    "externalId": null,
    "sourceUrl": "https://empresa.gupy.io/job/12345",
    "title": "Tech Lead .NET",
    "company": "Empresa Ltda",
    "location": "São Paulo/SP",
    "description": "Cole aqui o texto original da vaga.",
    "publishedAt": "2026-11-04T09:00:00Z"
  }'

# normaliza registros brutos -> 200 {"processed":20,"normalized":17,"rejected":3,
#                                    "rejectionReasons":["Employer could not be identified from the source content."]}
curl -s -X POST "$API_URL/api/catalog/normalize?take=100" -H "X-Api-Key: $API_KEY"
```

---

## 6. Catálogo de vagas

```bash
curl -s "$API_URL/api/jobs?pageSize=10" -H "X-Api-Key: $API_KEY"
# 200 {"items":[{"id":"...","title":"Tech Lead .NET","employerName":"...","location":"...",
#       "modality":"Hybrid","contractType":"Pj","seniority":"Lead","minAmount":null,
#       "maxAmount":null,"currency":null,"status":"Active",
#       "publishedAt":"2026-11-03T22:44:38+00:00","lastSeenAt":"..."}],
#     "page":1,"pageSize":10,"totalCount":213,"totalPages":22}   # contagem em 05/11/2026

# filtros combináveis (a resposta acima vira {"items":[...],"totalCount":0,...} quando nada casa)
curl -s "$API_URL/api/jobs?query=tech+lead&modality=Remote&contractType=Clt&seniority=Senior&page=1&pageSize=10" \
  -H "X-Api-Key: $API_KEY"
```

- `modality`: `Remote|Hybrid|Onsite`
- `contractType`: `Clt|Pj`
- `seniority`: `Intern|Junior|MidLevel|Senior|Lead|Principal|Architect`
- `onlyActive` padrão `true`
- **os valores são case-sensitive**: `modality=remote` responde `400`

```bash
curl -s "$API_URL/api/jobs/$JOB_ID" -H "X-Api-Key: $API_KEY"
curl -s "$API_URL/api/jobs/$JOB_ID/evaluations" -H "X-Api-Key: $API_KEY"
curl -i -s "$API_URL/api/jobs/00000000-0000-0000-0000-000000000009" -H "X-Api-Key: $API_KEY"
# 404 {"title":"Resource not found","detail":"JobOpportunity was not found.", ...}
```

---

## 7. Avaliação

```bash
# preference: Auto|RuleBased|Ai   (Auto usa IA quando configurada, senão regras)
curl -s -X POST "$API_URL/api/evaluations" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{
    "candidateProfileId": "'"$CANDIDATE_ID"'",
    "jobOpportunityId": "'"$JOB_ID"'",
    "preference": "RuleBased",
    "force": false
  }'                                     # 201 {"id":"<EVALUATION_ID>"}

curl -s "$API_URL/api/evaluations/$EVALUATION_ID" -H "X-Api-Key: $API_KEY"
# 200 com overallScore, recommendation, gaps, strengths, dimensions,
#     engine, modelProvider/ModelName/PromptVersion (quando IA) e inputFingerprint
```

Com `preference` inválido a resposta é `400`.

---

## 8. Candidatura (sempre manual)

> A API **nunca** envia candidatura. O status `Applied` apenas registra algo que o candidato
> fez à mão.

### Máquina de estados

```
Discovered -> Analyzed -> ReadyForReview -> Prepared -> Applied -> Rejected
                    \          \               \          ^
                     \          \               +---------+  exige userConfirmed=true
                      \          \                            E salary-expectation definido
                       \          +-> Expired
                        +-> Expired
```

Toda transição fora dessa lista responde `409`. `Analyzed` exige candidatura vinculada a uma
avaliação.

```bash
# abrir candidatura -> 201 {"id":"<APP_ID>"}
curl -s -X POST "$API_URL/api/applications" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{"candidateProfileId":"'"$CANDIDATE_ID"'","jobOpportunityId":"'"$JOB_ID"'","jobEvaluationId":"'"$EVALUATION_ID"'"}'

curl -s "$API_URL/api/applications?candidateProfileId=$CANDIDATE_ID" -H "X-Api-Key: $API_KEY"
curl -s "$API_URL/api/applications/$APP_ID" -H "X-Api-Key: $API_KEY"
curl -s "$API_URL/api/applications?status=Applied" -H "X-Api-Key: $API_KEY"

# material -> 201   type: Resume|RecruiterMessage|QuestionAnswer|SalaryExpectation|CoverLetter
curl -s -X POST "$API_URL/api/applications/$APP_ID/materials" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{"applicationId":"'"$APP_ID"'","type":"RecruiterMessage","title":"Mensagem para o recrutador","content":"Olá! ...","preparedBy":"candidate","modelName":null}'

# nota -> 201
curl -s -X POST "$API_URL/api/applications/$APP_ID/notes" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{"applicationId":"'"$APP_ID"'","text":"Entrevista marcada.","author":"candidate"}'

# expectativa salarial -> 204 (obrigatória antes de Applied)
curl -s -X PUT "$API_URL/api/applications/$APP_ID/salary-expectation" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{"applicationId":"'"$APP_ID"'","amount":11000,"currency":"BRL"}'

# pacote de candidatura (resumo de currículo + mensagem) -> 200 {"materialIds":["...","..."]}
curl -s -X POST "$API_URL/api/applications/$APP_ID/prepare-package" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' -d '{}'
```

### Mudança de status

```bash
# 200 {"status":"Analyzed"}
curl -s -X PUT "$API_URL/api/applications/$APP_ID/status" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{"applicationId":"'"$APP_ID"'","targetStatus":"Analyzed","changedBy":"rafael","reason":null,"userConfirmed":false}'

# sem confirmação -> 409 {"title":"Business rule violation",
#                        "detail":"Marking an application as Applied requires explicit confirmation by the candidate."}
curl -s -X PUT "$API_URL/api/applications/$APP_ID/status" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{"applicationId":"'"$APP_ID"'","targetStatus":"Applied","changedBy":"rafael","reason":null,"userConfirmed":false}'

# com confirmação -> 200 {"status":"Applied"}
curl -s -X PUT "$API_URL/api/applications/$APP_ID/status" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{"applicationId":"'"$APP_ID"'","targetStatus":"Applied","changedBy":"rafael","reason":"candidate enviou manualmente","userConfirmed":true}'
```

---

## 9. Digest diário

```bash
# gera (ou devolve) o digest do dia -> 200
curl -s -X POST "$API_URL/api/digests" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' \
  -d '{"candidateProfileId":"'"$CANDIDATE_ID"'"}'
# {"digestId":"<DIGEST_ID>","digestDate":"2026-11-05","status":"Pending","itemCount":0,"alreadyExisted":false}

curl -s "$API_URL/api/digests?candidateProfileId=$CANDIDATE_ID&take=10" -H "X-Api-Key: $API_KEY"
# 200 [{"id":"...","digestDate":"2026-11-04","status":"Pending","itemCount":20,"attemptCount":0,"sentAt":null}]

curl -s "$API_URL/api/digests/$DIGEST_ID" -H "X-Api-Key: $API_KEY"
# 200 itens com rank, score, jobTitle, employerName, jobUrl, recommendation

# itens com o detalhe da vaga (é o que o dashboard consome, em uma chamada só)
curl -s "$API_URL/api/digests/$DIGEST_ID/items" -H "X-Api-Key: $API_KEY"
# 200 [{...,"location":"São Paulo, SP","modality":"Remote","contractType":"Clt",
#       "seniority":"Senior","compensation":{"minAmount":10000,...,"isEstimated":false},
#       "description":"...","publishedAt":"...","sourceName":"Adzuna BR - .NET Sao Paulo",
#       "requirements":[{"name":".NET","category":"Technology","isRequired":true}]}]
# Campos que a fonte não trouxe vêm null (modality, compensation, publishedAt...).
# Digest inexistente -> 404; digest sem itens -> 200 [].
```

### Prévia do e-mail (não envia)

```bash
curl -s "$API_URL/api/digests/$DIGEST_ID/preview?format=json" -H "X-Api-Key: $API_KEY"   # subject, to, htmlBody, textBody
curl -s "$API_URL/api/digests/$DIGEST_ID/preview?format=text"  -H "X-Api-Key: $API_KEY"   # text/plain
curl -s "$API_URL/api/digests/$DIGEST_ID/preview"              -H "X-Api-Key: $API_KEY"   # HTML no navegador
```

### Envio (tem efeito real)

```bash
curl -s -X POST "$API_URL/api/digests/$DIGEST_ID/send" \
  -H "X-Api-Key: $API_KEY" -H 'Content-Type: application/json' -d '{}'
```

Comportamento verificado:

| Situação | Resposta |
| --- | --- |
| Preferência com `isEnabled=false` | `409` — falha **antes** de tocar no SMTP |
| Digest já `Sent` | `200` `{"delivered":true,...}` — idempotente, não reenvia |
| Id inexistente | `404` |
| SMTP configurado e preferência ativa | `200` `{"delivered":true,"attemptCount":...}` e **o e-mail sai** |

---

## 10. Cenário de verificação em produção (copie e cole)

> Script em **bash** (Git Bash, WSL ou o terminal do servidor). No PowerShell, troque os
> `-d '{...}'` por `--data-binary "@corpo.json"` — ver aviso da seção 1.

```bash
export API_URL="https://seu-host"        # ou http://127.0.0.1:5080 via túnel SSH
export API_KEY="sua-chave"
export H="X-Api-Key: $API_KEY"

# 1. liveness + banco
curl -s "$API_URL/health" && echo
curl -s "$API_URL/health/database" && echo

# 2. autenticação (espera 401)
curl -s -o /dev/null -w "%{http_code}\n" "$API_URL/api/jobs"

# 3. dados
curl -s "$API_URL/api/candidates" -H "$H"
CANDIDATE_ID=$(curl -s "$API_URL/api/candidates" -H "$H" | jq -r '.[0].id')
curl -s "$API_URL/api/jobs?pageSize=3&query=tech+lead" -H "$H"
curl -s "$API_URL/api/job-sources" -H "$H"

# 4. pipeline
curl -s -X POST "$API_URL/api/job-sources/collect?candidateProfileId=$CANDIDATE_ID&force=true" -H "$H"
curl -s -X POST "$API_URL/api/catalog/normalize?take=50" -H "$H"

# 5. digest de hoje
DIGEST_ID=$(curl -s -X POST "$API_URL/api/digests" -H "$H" -H 'Content-Type: application/json' \
  -d '{"candidateProfileId":"'"$CANDIDATE_ID"'"}' | jq -r '.digestId')
curl -s "$API_URL/api/digests/$DIGEST_ID" -H "$H"
curl -s "$API_URL/api/digests/$DIGEST_ID/preview?format=text" -H "$H"
# somente depois de revisar a prévia:
# curl -s -X POST "$API_URL/api/digests/$DIGEST_ID/send" -H "$H" -H 'Content-Type: application/json' -d '{}'
```

Sem `jq`, troque por `grep -o` ou leia a saída na mão.

---

## 11. Notas de operação

- **OpenAPI** só é publicado quando `ASPNETCORE_ENVIRONMENT=Development`: a rota é
  `/openapi/v1.json` (checado em 05/11/2026 — `200`; em qualquer outro ambiente dá `404`). Em
  produção a documentação é este arquivo.
- `GET /api/jobs` filtra por `onlyActive=true` por padrão; passe `onlyActive=false` para ver
  vagas expiradas.
- `POST /api/digests` é idempotente por candidato e por dia: repetir devolve
  `alreadyExisted:true` com o mesmo `digestId`.
- `POST /api/evaluations` com `force:false` reaproveita a avaliação existente para o mesmo
  par candidato/vaga.
- Endpoints sem `DELETE`: candidato, fonte, vaga e candidatura. Pausar uma fonte é preferível
  a apagá-la (mantém a auditoria das coletas).
- Remoção manual de um candidato **direto no banco** não apaga avaliações, candidaturas,
  fontes, preferência de notificação e digests: essas tabelas não têm FK para
  `candidate_profiles` (ver `docs/delivery-report.md`, débito 10).
