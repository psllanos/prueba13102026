# ProviderOptimizerService — ASISYA Technical Exercise

Solution for the **Líder Técnico de Desarrollo** exercise: the *Asignación Inteligente de
Proveedores* module of an assistance system (tow truck, locksmith, battery).

When a customer requests assistance, the service decides **which provider should serve it, and
why**, then records the case.

---

## Deliverables

The exercise asks for four mandatory deliverables plus CI and an optional client. Map:

| # | Deliverable | Where | Status |
|---|---|---|---|
| 1 | Diseño arquitectónico completo | [`docs/architecture.md`](docs/architecture.md), [`docs/aws-deployment.md`](docs/aws-deployment.md) | Done |
| 2 | Microservicio crítico implementado (.NET) | [`src/`](src/) | Done |
| 3 | Estándares técnicos del squad | [`docs/squad-standards.md`](docs/squad-standards.md) | Done |
| 4 | Code review de código defectuoso | [`docs/code-review/review.md`](docs/code-review/review.md) | Done |
| 5 | CI/CD (GitHub Actions) | [`.github/workflows/ci.yml`](.github/workflows/ci.yml) | Done |
| 6 | Frontend React (opcional) | [`frontend/`](frontend/) | Done |

> **Note on deliverable 4.** The exercise PDF announces *"Se entrega este snippet defectuoso"* and
> ends the section on **"Snippet (C#):"** without including the code — the snippet is not in the
> PDF's content streams, images or embedded files. The snippet was reconstructed to contain
> **exactly the eight defects the exercise enumerates**, and every one is traced to a line. See
> [`docs/code-review/review.md`](docs/code-review/review.md).

---

## What it does

```
POST /optimize                → select the optimal provider, persist the case + its decision
GET  /providers/available     → list providers eligible for a service type and location
```

Scoring is deterministic over **ETA, distance, availability and rating**. Each factor is normalised
across the candidate set and weighted, and the response carries the full **score breakdown** so the
decision is auditable rather than a bare number. Ties break by provider id.

Covers steps **3, 4 and 6** of the exercise's flow (search providers, compute the optimum, record
the case). Request intake, provider notification and real-time tracking are other services' concern.

---

## Run it

### Requirements

.NET 8 SDK · Docker Desktop · (optional) Node 22+ for the client.

### Full stack

```bash
docker compose up --build
```

Brings up Postgres (with a healthcheck) and the API, applying migrations on startup. The API waits
for Postgres to accept connections — it applies migrations at boot and does not retry.

```bash
# Seed providers so the endpoints have something to return.
# Works in cmd, PowerShell, bash and Linux alike: it feeds a FILE, so it needs no shell
# redirection, no `$(...)` command substitution and no heredoc.
docker compose exec -T postgres psql -U postgres -d provider_optimizer < scripts/seed.sql

curl -s "http://localhost:8080/providers/available?serviceType=tow-truck&latitude=4.711&longitude=-74.0721&radiusKm=10"

curl -s -X POST http://localhost:8080/optimize \
  -H "Content-Type: application/json" \
  -d '{"serviceType":"tow-truck","location":{"latitude":4.711,"longitude":-74.0721},"searchRadiusKm":10}'
```

<details>
<summary><strong>Windows: a bash heredoc will not work here</strong></summary>

An earlier version of this README used `docker exec -i $(docker compose ps -q postgres) psql ... <<'SQL'`.
Both constructs are **bash-only**, and this repository may be opened from `cmd.exe` or PowerShell:

| Construct | `cmd.exe` | PowerShell | bash |
|---|---|---|---|
| `<<'SQL'` heredoc | ❌ *"No se esperaba << en este momento"* | ❌ | ✅ |
| `$(...)` substitution | ❌ | ✅ | ✅ |

The file-based command above works everywhere. If you are already in `bash`/WSL and prefer the
shell-native form, it is equivalent:

```bash
docker compose exec -T postgres psql -U postgres -d provider_optimizer < scripts/seed.sql
```

</details>

### Without Docker

```bash
dotnet build ProviderOptimizer.sln -c Release
dotnet run --project src/ProviderOptimizer.Api
```

Requires `ConnectionStrings__OptimizerDb`; the app **fails fast** if it is absent rather than
falling back to an insecure default.

### Frontend (optional)

```bash
cd frontend
npm install
npm run dev     # http://localhost:5173, proxies /api to the API on :8080
```

Simulated JWT sign-in (any non-empty credentials), provider status table with simulated ETA, and the
score breakdown for the selected provider.

---

## Verify

Every claim below was executed; nothing is asserted from intent.

| Check | Command | Result |
|---|---|---|
| Build (warnings are errors) | `dotnet build ProviderOptimizer.sln -c Release -warnaserror` | 0 warnings, 0 errors |
| Lint | `dotnet format ProviderOptimizer.sln --verify-no-changes` | clean |
| Unit tests | `dotnet test tests/ProviderOptimizer.UnitTests` | **21 passed** |
| Integration tests (real Postgres) | `dotnet test tests/ProviderOptimizer.IntegrationTests` | **19 passed** |
| Frontend typecheck | `npm run typecheck` | clean |
| Frontend lint | `npm run lint` | clean |
| Frontend build | `npm run build` | built |
| End-to-end | `docker compose up` + real `POST /optimize` | 200, case persisted |

The integration suite hosts the real application (`WebApplicationFactory`) against a **real Postgres
container** (Testcontainers) — it does not mock the database.

---

## Architecture

```
ProviderOptimizer.Api          minimal API, DTOs, composition root, migrations on startup
        │
ProviderOptimizer.Infrastructure   EF Core, Npgsql, repositories  (implements the ports)
        │
ProviderOptimizer.Application      use cases and ports
        │
ProviderOptimizer.Domain           entities, value objects, scoring and eligibility rules
```

**The dependency rule points inward.** `Domain` references nothing; the rule is enforced by project
references, so a violation fails the build instead of relying on a reviewer's memory.

Three decisions carry the design:

| Decision | Why |
|---|---|
| Eligibility and scoring are **pure domain code**, not SQL | Every rule is provable with unit tests, with no database and no clock |
| The repository is a **coarse pre-filter only** | SQL filtering cannot be unit-tested; a contract test proves the pre-filter agrees with the domain rule |
| A case **cannot be represented without its decision** (`breakdown jsonb NOT NULL`) | The guarantee is structural, not a runtime check someone must remember |

Full detail: [`docs/architecture.md`](docs/architecture.md) · AWS topology:
[`docs/aws-deployment.md`](docs/aws-deployment.md) · conventions:
[`docs/squad-standards.md`](docs/squad-standards.md).

---

## Layout

```
src/            Clean Architecture projects (Domain, Application, Infrastructure, Api)
tests/          Unit and integration suites
frontend/       Optional React client (TypeScript, Vite)
scripts/        Development seed data
docs/           Architecture, AWS topology, squad standards
docs/code-review/   Defective snippet and its review
openspec/       Spec-driven change: proposal, design, specs, tasks
.github/        CI pipeline
```

---

## Known limitations

Stated openly rather than left for the reviewer to discover. Each is tracked in
[`docs/squad-standards.md`](docs/squad-standards.md) §11.2.

- **The API has no authentication or authorization.** It must not be exposed publicly until it does.
  The frontend's JWT is a *simulated* placeholder that the API does not verify.
- **No rate limiting** on `POST /optimize`, which runs an outbound query per call.
- **Providers are seeded and simulated** — ETA and distance are not real telemetry.
- **Scoring runs in-process on a single instance**; no distributed cache yet (Redis is in the target
  architecture but not wired).
- **No structured logging, metrics or tracing** yet.
- `dotnet format` is in CI, but the analyzers that enforce the async rules (`VSTHRD002`, `CA2007`)
  are not enabled yet.

---

## Exercise

The original brief (recruitment material, not published with this repository) asked for four
mandatory deliverables — architecture, one implemented microservice, squad standards and a code
review — plus a GitHub Actions pipeline and an optional React client. How each one was addressed is
mapped in the **Deliverables** table at the top of this document.
