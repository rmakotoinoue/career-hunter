# Career Hunter — Architecture Assessment

Status: Phase 1 deliverable (baseline inspection + target design).
Date: 2026-11-02.

## 1. Baseline findings

### 1.1 What exists

| Artifact | State |
| --- | --- |
| `src/CareerHunter.Domain` | Empty class library (README only) |
| `src/CareerHunter.Application` | Empty class library, references Domain |
| `src/CareerHunter.Infrastructure` | Empty class library, references Domain + Application, `Npgsql` only |
| `src/CareerHunter.Api` | Minimal API with `/`, `/health`, `/health/database`. Direct `NpgsqlDataSource` usage, no EF Core, no domain |
| `src/CareerHunter.Worker` | `BackgroundService` logging every 60s, no business pipeline, no Infrastructure reference |
| `CareerHunter.Api.csproj` / `Worker.csproj` | No project references at all — layers are disconnected from the host |
| `frontend/` | README only, Angular app not created (Angular CLI not installed in the environment) |
| Tests | None |
| Solution file | None (projects built individually) |
| Docker Compose | PostgreSQL 17-alpine, health check, named volume, private network, API + Worker services |
| Migrations | None |

### 1.2 Defects and architectural problems found

1. **Hosts bypass the architecture.** `Program.cs` opens `NpgsqlDataSource` directly in the API; the Worker has no reference to Application/Infrastructure. All business logic would end up in endpoints if this pattern continued.
2. **No persistence model.** No `DbContext`, no provider configuration, no migrations. The Docker PostgreSQL container has no schema.
3. **Dockerfiles copy only their own project**, so the first project reference added to API/Worker breaks `docker build` (`restore` cannot see referenced projects). No `.dockerignore`.
4. **No solution file**, so `dotnet build` at the repository root is not possible and CI cannot build the whole graph.
5. **Worker compose service has no connection string** even though it must access the same database.
6. **Vulnerable transitive package**: `Microsoft.AspNetCore.OpenApi 10.0.0` → `Microsoft.OpenApi 2.0.0` (high severity advisory GHSA-v5pm-xwqc-g5wc). Also `Npgsql` requested directly where EF Core is the intended data access path.
7. **Health endpoint leaks infrastructure details** (`exception.Message` from Npgsql to the client).
8. **Encoding corruption in source files** (`JobSearchWorker.cs`, READMEs, `Program.cs` error string): UTF-8 was written as ANSI, producing `Pr��xima`, `nǜo`. Must be fixed when touched.
9. **No credential policy in appsettings.Development.json.** A real password literal is committed. Local dev must use `.env`/user-secrets; documented default must be clearly local-only.
10. **No idempotency, no duplicate detection, no domain invariants** anywhere — nothing to preserve yet, but the target design must include them.
11. **API has no auth, no validation, no Problem Details, no request limits.** Acceptable only for a local skeleton; must be addressed before VPS deployment.

### 1.3 Verified tooling

* .NET SDK 11.0.100-rc.3 with .NET 10.0.14 targeting/runtime packs — `net10.0` builds succeed.
* `dotnet-ef` 10.0.13 installed globally.
* Node 24 / npm 11 present; Angular CLI absent (frontend generation deferred).
* Docker CLI 29.8.2 / Compose v5.6.0 installed, **daemon not running** in this environment, so container runtime checks are configuration-level only.

## 2. Target architecture

Modular monolith, one deployable API process, one Worker process, one PostgreSQL database, one Angular app.

```
CareerHunter.Api ──────┐
CareerHunter.Worker ───┼──► CareerHunter.Infrastructure ──► CareerHunter.Application ──► CareerHunter.Domain
                       └───────────────────────────────────────────────────────────────┘
```

* `Domain` — no framework references beyond the BCL (no EF, no ASP.NET Core, no AI SDK).
* `Application` — use cases, ports, contracts, validation; depends only on Domain + `Microsoft.Extensions.*` abstractions.
* `Infrastructure` — EF Core/Npgsql, HTTP AI provider, SMTP, source adapters, query services.
* `Api` — endpoint mapping, HTTP contracts validation, Problem Details, DI composition only.
* `Worker` — scheduled orchestration of the **same** application use cases.

Dependency rule enforced in review: no project may reference `Api` or `Worker`; `Domain` references nothing.

### 2.1 Bounded contexts

| # | Context | Aggregate roots | Owns data |
| --- | --- | --- | --- |
| 1 | CandidateProfile | `CandidateProfile` | experiences, skills, education, certifications, career targets, work preferences, compensation expectation |
| 2 | JobDiscovery | `JobSource`, `SourceJobRecord`, `CollectionRun` | raw source content, collection errors, collection timestamps |
| 3 | JobCatalog | `JobOpportunity`, `Employer` | normalized job data, requirements, location, compensation, provenance |
| 4 | JobAssessment | `JobEvaluation`, `EvaluationPromptVersion` | scores, requirement matches, evidence, model/prompt audit data |
| 5 | ApplicationTracking | `Application` | status history, prepared materials, notes, applied salary expectation |
| 6 | Notification | `NotificationPreference`, `DailyDigest` | digest items, delivery attempts |

Contexts are separated by folders in Domain/Application and by schema conventions in PostgreSQL. **Cross-context references are identifiers only** (no EF navigations between contexts); cross-context reads use query services implemented in Infrastructure.

### 2.2 Context map and relationships

```
CandidateProfile ◄──(candidateId)── JobAssessment ──(jobId)──► JobCatalog
        ▲                                │                        ▲
        │                                │                        │ provenance
        │                                ▼                        │
        └────────(candidateId)──── ApplicationTracking      JobDiscovery
                                         │                   (raw records)
                                         │ evaluation ids
                                         ▼
                                    Notification
```

* JobDiscovery → JobCatalog: a `SourceJobRecord` is normalized into exactly one `JobOpportunity`; the opportunity keeps a list of `JobSourceReference` (provenance). Relationship is *discovery produces raw data; catalog owns normalized data*.
* JobCatalog → JobAssessment: evaluation references `JobOpportunityId`; the evaluation stores its own snapshot of the input, so re-normalization never rewrites history.
* JobAssessment → ApplicationTracking: an application is created from an evaluation (score + gaps copied as a snapshot at creation time).
* ApplicationTracking → Notification: digest items reference evaluations (id, score, recommendation, URL snapshot).
* Notification never mutates evaluations: a delivery failure is recorded on the digest only.

### 2.3 Aggregate boundaries and why

* **CandidateProfile is one aggregate.** Skills/experiences/targets/preferences have no independent lifecycle and every change must keep uniqueness and preference invariants consistent. Loading is bounded (< 200 rows in practice).
* **SourceJobRecord is its own aggregate** (not part of `CollectionRun`). A collection run can contain thousands of records; loading a run must never load raw job descriptions. `CollectionError` is inside `CollectionRun` because errors are meaningless without their run.
* **CollectionRun** is an aggregate root so a run can be completed/failed atomically while records are inserted in batches.
* **JobOpportunity own its requirements and provenance**; `JobSourceReference` is a child because provenance must stay consistent with the opportunity and is queried with it.
* **Employer is separate** and referenced by id; employer identity is shared by many opportunities and has its own dedupe rule (normalized name uniqueness).
* **JobEvaluation owns matches/evidence/dimensions** — they are never queried without the evaluation, and the score invariant spans all of them.
* **Application owns status history, materials, notes** — the transition invariant must be transactional with the history row.
* **DailyDigest owns items and delivery attempts** — duplicate-send prevention must be atomic with the delivery record.
* Deliberately **no** giant `Candidate → Jobs → Evaluations → Applications` aggregate: that would load unrelated data on every write.

### 2.4 Key invariants

CandidateProfile:
* at most one primary career target; demoting/adding a primary target is an atomic operation;
* skill names unique case-insensitively; proficiency 1–5; years 0–60;
* experience end date not before start date, current position has no end date;
* work preferences accept ≥ 1 modality and ≥ 1 contract type (CLT and/or PJ);
* compensation expectation min ≤ max, never negative, never invented (unknown stays `null`).

JobDiscovery:
* source name unique; URL absolute http(s);
* a `(sourceId, contentHash)` pair is stored once (idempotent collection);
* raw content and original URL are immutable once collected;
* collection errors recorded on the run, never swallowed.

JobCatalog:
* a normalized opportunity always has ≥ 1 provenance record and ≥ 1 employer;
* missing salary stays `null` — never `0`;
* unknown modality/contract/seniority stay `null`, never guessed;
* requirements unique by normalized name;
* dedupe key unique at database level; merging adds provenance, never removes the original description.

JobAssessment:
* overall score is **computed**, not trusted from input: `round(Σ dimension.score × weight)`;
* weights sum to 1.0 (±0.001), each score 0–100, each dimension unique;
* every requirement match has a status and evidence; low scores require a rationale;
* one missing secondary technology cannot zero an evaluation (scoring policy + validator);
* AI evaluations always store provider, model, prompt version and timestamp;
* previous evaluations are never deleted.

ApplicationTracking:
* allowed transition map; terminal states are final;
* `Applied` requires explicit user confirmation, an applied timestamp and a salary expectation;
* status history is append-only;
* `ReadyForReview` requires at least one prepared material.

Notification:
* one digest per candidate per date (unique index);
* an already-sent digest cannot be sent again (idempotency);
* every delivery attempt is persisted with provider, timestamp and error;
* delivery failure never deletes evaluations or digest items.

### 2.5 Value objects

`EmailAddress`, `Money`, `SalaryRange` (+ `SalaryPeriod`), `CandidateLocation`, `JobLocation`, `WorkPreferences`, `CompensationExpectation`, `DeduplicationKey` (string produced by a domain normalizer).
Shared kernel: `WorkModality`, `EmploymentContractType`, `SeniorityLevel` — shared language between CandidateProfile, JobCatalog and JobAssessment; duplicating the enums per context would create translation debt with no benefit.

### 2.6 Domain events

No domain event bus is introduced. The only asynchronous work (collection, digest) is time-triggered and already goes through explicit application use cases. Domain events would be speculative infrastructure with no consumer. Recorded as deliberate non-decision; revisit if notifications must react to status changes.

### 2.7 Explicitly rejected for now

MediatR, CQRS command bus, event sourcing, generic repositories, multi-tenant abstractions, microservices, Azure/AWS specific services.
Justification: one candidate, one database, one deployment target; the added indirection would obscure the business rules without solving a concrete problem.

## 3. Persistence model (logical)

```
candidate_profiles 1─* candidate_experiences
                   1─* candidate_skills
                   1─* candidate_educations
                   1─* candidate_certifications
                   1─* candidate_career_targets
                   owned: candidate_location, work_preferences, compensation_expectation

job_sources 1─* source_job_records *─1 collection_runs
source_job_records 0..1─1 job_opportunities (normalized_job_opportunity_id)

employers 1─* job_opportunities 1─* job_requirements
                                1─* job_source_references
                                1─* job_evaluations *─1 evaluation_prompt_versions

job_evaluations 1─* evaluation_dimension_scores
                1─* requirement_matches 1─* evaluation_evidences

applications 1─* application_status_changes
             1─* prepared_application_materials
             1─* application_notes
             owned: salary_expectation

notification_preferences (unique candidate)
daily_digests 1─* daily_digest_items
              1─* notification_deliveries
```

Indexes: `(job_source_id, content_hash)` unique, `(job_source_id, external_id)` unique filtered, `deduplication_key` unique, `(candidate_id, job_opportunity_id)` unique on applications, `(candidate_profile_id, digest_date)` unique, `(job_opportunity_id, evaluated_at desc)` on evaluations.

## 4. Implementation order

1. Solution/scaffolding, package management, Docker correctness, migrations baseline. *(Phase 2)*
2. CandidateProfile (aggregate + EF + use cases + endpoints + tests). *(Phase 3)*
3. JobDiscovery raw collection, then JobCatalog normalization/dedupe. *(Phase 4)*
4. JobAssessment: deterministic engine first, AI provider behind ports, validator, prompt versioning. *(Phase 5)*
5. ApplicationTracking with manual-approval enforcement. *(Phase 6)*
6. Notification digest + delivery audit + retry. *(Phase 7)*
7. Independent review, build/test evidence, technical debt. *(Phase 8)*

## 5. Assumptions

1. Single candidate profile in practice; the model still carries `CandidateProfileId` on evaluations, applications and digests so multi-profile support does not require a schema rewrite.
2. Job source adapters in this iteration: `manual` (deterministic test/seed input) and `rss` (public feeds); scraping protected platforms is out of scope and forbidden.
3. AI provider is OpenAI-compatible chat completions; when not configured, the deterministic rule engine is used and the evaluation records `RuleBased`.
4. Email delivery uses SMTP (MailKit). A logging sender is used when SMTP is not configured.
5. Local dev credentials come from `.env`; no production secret is committed.
