# Squad Technical Standards

Binding conventions, policies and definition of done for the squad that owns
`ProviderOptimizerService` and the services around it (Backend .NET, Frontend React, Docker, AWS,
GitHub Actions).

**How to read this document.** Every rule is either *enforced* (a tool, analyzer or CI gate stops
you) or *reviewed* (a reviewer must catch it). A rule with no enforcement mechanism listed is a
smoke signal, not a standard. Where the enforcement does not exist yet, it says **to be added** —
that gap is itself tracked technical debt (§9).

---

## 1. Base architecture of the squad

### 1.1 Layer model and the dependency rule

Clean Architecture. Dependencies point **inward only**. `Domain` is the centre and knows nothing
about the outside world.

```
        ┌──────────────────────────────────────────────────────────┐
        │  ProviderOptimizer.Api                                  │
        │  minimal API, DTOs, composition root, migrations         │
        └───────────────────────────┬──────────────────────────────┘
                                    │ may reference
        ┌───────────────────────────▼──────────────────────────────┐
        │  ProviderOptimizer.Infrastructure                        │
        │  EF Core, Npgsql, repositories (implements the ports)    │
        └───────────────────────────┬──────────────────────────────┘
                                    │ may reference
        ┌───────────────────────────▼──────────────────────────────┐
        │  ProviderOptimizer.Application                           │
        │  use cases (handlers) and ports                          │
        └───────────────────────────┬──────────────────────────────┘
                                    │ may reference
        ┌───────────────────────────▼──────────────────────────────┐
        │  ProviderOptimizer.Domain                                │
        │  entities, value objects, scoring and eligibility rules  │
        │  references NOTHING                                      │
        └──────────────────────────────────────────────────────────┘
```

### 1.2 Which project may reference which

| Project | May reference | Must NOT reference |
|---|---|---|
| `ProviderOptimizer.Domain` | nothing (only the BCL) | Application, Infrastructure, Api, EF Core |
| `ProviderOptimizer.Application` | `Domain` | Infrastructure, Api, EF Core |
| `ProviderOptimizer.Infrastructure` | `Domain`, `Application` | Api |
| `ProviderOptimizer.Api` | `Application`, `Infrastructure` | — |
| `*.UnitTests` | `Domain`, `Application` | Api, Infrastructure |
| `*.IntegrationTests` | everything (it hosts the real app) | — |

**Enforced by** project references in each `.csproj`. A violation is a compile error, not a review
opinion. This is deliberate: the rule does not depend on a reviewer remembering it.

### 1.3 Where each kind of rule lives

| Rule kind | Lives in | Why there |
|---|---|---|
| Business invariants (valid coordinates, valid weights, positive radius) | `Domain` value objects | Invalid objects are not constructible, so the rule cannot be bypassed |
| Selection and eligibility policy | `Domain/ProviderScoringPolicy.cs`, `Domain/ProviderEligibility.cs` | Pure and I/O-free → provable with unit tests, no database, no clock |
| Orchestration and transaction boundary | `Application` handlers | Owns the unit of work, not the transport |
| HTTP shape, status codes, DTO mapping | `Api/Endpoints/` | Transport concern, isolated from business rules |
| SQL pre-filter | `Infrastructure/Persistence/` | A **coarse pre-filter only** — never the authority (§5.3) |

---

## 2. .NET conventions

### 2.1 Shared build configuration

All projects inherit from one `Directory.Build.props` at the repo root. **Never** set these
per-project:

```xml
<TargetFramework>net8.0</TargetFramework>
<LangVersion>12.0</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<InvariantGlobalization>true</InvariantGlobalization>
```

- `<Nullable>enable</Nullable>` is mandatory. **Enforced by** the compiler: a nullable warning is
  treated as an error in CI (`-warnaserror`) — **to be added** to `ci.yml`.
- A `.csproj` contains only `RootNamespace`, `ProjectReference` and `PackageReference`. If you are
  writing anything else, it probably belongs in `Directory.Build.props`.

### 2.2 Naming

| Element | Convention | Example |
|---|---|---|
| Namespace | File-scoped, matches folder path | `namespace ProviderOptimizer.Domain;` |
| Interface | `I` prefix, capability noun | `IProviderRepository`, `IClock`, `IProviderScoringPolicy` |
| Async method | `Async` suffix | `HandleAsync`, `FindByServiceTypeAsync`, `SaveAsync` |
| Private field | `_camelCase` | `_providers`, `_cases`, `_scoring`, `_clock`, `_weights` |
| Public member | `PascalCase` | `SelectionResult`, `HasSelection` |
| Static factory | `Create`, `FromX`, `Parse` | `GeoPoint.Create`, `ScoringWeights.FromConfiguration`, `ServiceType.Parse` |
| Test project | `<Project>.UnitTests` / `<Project>.IntegrationTests` | `ProviderOptimizer.UnitTests` |
| Test method | Describes the scenario and the guarantee, not the method under test | see §6.3 |

**File layout.** One type per file by default. Group by *role* when the types are small and always
used together and the grouping is the documentation: `Domain/ValueObjects.cs` holds `ServiceType`,
`GeoPoint`, `CustomerLocation` and `AssistanceRequest`; `Domain/ScoringResults.cs` holds the related
scoring result records. Do not scatter five one-line records across five files.

### 2.3 Type design

**Prefer types that cannot represent an invalid state.** This is the single most important house
rule, and it is why the reviewed defective snippet's defects 4, 5 and 8 are impossible here.

```csharp
// Domain/ValueObjects.cs — a GeoPoint outside the valid range cannot exist.
private GeoPoint(double latitude, double longitude) { ... }

public static GeoPoint Create(double latitude, double longitude)
{
    if (latitude is < -90d or > 90d)
        throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90.");
    if (longitude is < -180d or > 180d)
        throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180.");
    return new GeoPoint(latitude, longitude);
}
```

| Rule | Detail |
|---|---|
| Value objects | `sealed record`, private constructor, validating static factory |
| Small scalar value objects | `readonly record struct` (`GeoPoint`) |
| Services and handlers | `sealed class` — nothing is designed for inheritance |
| Entities | `sealed class` with private setters and named factory methods (`AssistanceCase.Create`) |
| Collections | Return `IReadOnlyList<T>` / `IReadOnlyCollection<T>`, never `List<T>` or arrays |
| Mutability | No public setters on domain types. No `static` mutable state — ever (§2.5) |
| Optionality | `Provider?` with an explicit `HasSelection` flag rather than a sentinel or `null` convention |

### 2.4 Guard clauses and validation

Use the BCL guard helpers; do not hand-roll `if (x == null) throw`:

```csharp
ArgumentNullException.ThrowIfNull(candidates);
ArgumentException.ThrowIfNullOrWhiteSpace(value);
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radiusKm);
```

- Every public method validates its arguments at the top, before any work.
- **Enforced by** `dotnet format` + review. Analyzer `CA1062` **to be added**.
- Validation happens **once, at the boundary**, and the validated value travels inward. Do not
  re-validate the same rule in three layers.

### 2.5 Async, time and concurrency

| Rule | Enforcement |
|---|---|
| `async` propagates end to end; never return `void` from async | review; `CA2007` |
| **Never** `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` | analyzer `VSTHRD002` — **to be added** |
| `CancellationToken cancellationToken = default` as the **last** parameter of every async method, and forward it to every I/O call | review |
| `.ConfigureAwait(false)` in `Domain`/`Application`/`Infrastructure` (library code) | analyzer `CA2007` — **to be added** |
| Never call `DateTime.UtcNow` / `DateTime.Now` outside `SystemClock` | use the `IClock` port; analyzer `RS0030`-style ban **to be added** |
| No `static` mutable field or property anywhere | review — this is a **blocking** finding (§7) |

The `IClock` port exists so time is injected and tests are deterministic:

```csharp
// Domain types never read the clock themselves.
var assistanceCase = AssistanceCase.Create(
    Guid.NewGuid(), request.ServiceType, request.Location,
    provider.Id, breakdown, _clock.UtcNow);
```

### 2.6 Comments and documentation

XML doc comments (`/// <summary>`) on every public type and member. Comments explain **why**, never
**what** — the code already says what.

```csharp
// Good: explains a non-obvious decision a reader would otherwise "fix" by mistake.
// Startup fails on a missing, negative or all-zero weight set: that is a misconfiguration, not a default.
```

```csharp
// Bad: restates the code, carries no information.
// Sets the weights.
```

Non-obvious decisions **must** carry a comment or a `docs/` entry. The comment above
`depends_on: condition: service_healthy` in `docker-compose.yml` and the note in `OptimizeEndpoint`
about migrations are the reference examples.

---

## 3. React conventions

> The React client is currently **out of scope** for this change (see
> `openspec/changes/provider-optimizer-service/proposal.md`). These conventions are the squad
> standard to be applied when the optional client lands — they are written now so it does not start
> divergent.

### 3.1 Structure and naming

| Element | Convention | Example |
|---|---|---|
| Component file | `PascalCase.tsx`, one component per file | `ProviderTable.tsx` |
| Component | Function component, arrow or `function`, typed props | `function ProviderTable({ rows }: ProviderTableProps)` |
| Props type | `<Component>Props`, not exported unless reused | `ProviderTableProps` |
| Hook file | `use<Name>.ts` | `useProviderStatus.ts` |
| Non-component file | `camelCase.ts` | `apiClient.ts` |
| Folder | `features/<domain>/` for feature code, `shared/` for cross-cutting | `features/providers/` |

### 3.2 Rules

| Rule | Enforcement |
|---|---|
| **TypeScript strict mode on** (`strict: true`, no `any`) | `tsconfig.json` + ESLint `@typescript-eslint/no-explicit-any` |
| No business logic in components — it belongs in a hook or a plain module | review |
| Server state via a query library (TanStack Query); no hand-rolled `useEffect` fetch loops | review; ESLint `react-hooks/exhaustive-deps` |
| Every list render has a stable `key` that is not the array index | ESLint `react/jsx-key` |
| Never store the JWT in `localStorage`; use an httpOnly cookie or in-memory + refresh | review — **security blocking** |
| No secret, API base URL or bucket name hardcoded; use `import.meta.env.VITE_*` | review |
| Formatting and lint are automated, not argued | Prettier + ESLint via `npm run lint` in CI (**to be added**) |
| Accessibility: every input has a label, every interactive element is keyboard reachable | ESLint `jsx-a11y` |

---

## 4. Docker conventions

### 4.1 Dockerfile

| Rule | Detail |
|---|---|
| Multi-stage | `mcr.microsoft.com/dotnet/sdk:8.0` as `build`, `mcr.microsoft.com/dotnet/aspnet:8.0` as `runtime` |
| Syntax directive | `# syntax=docker/dockerfile:1` on line 1 |
| **Restore before copy** | Copy `Directory.Build.props` + every `.csproj` first, `dotnet restore`, *then* copy source. Editing source must not invalidate the NuGet cache layer |
| Release only | `dotnet publish --configuration Release --no-restore` |
| Non-root | `USER $APP_UID`. Running as root is a **blocking** review finding |
| Port | `EXPOSE 8080` and `ASPNETCORE_URLS=http://+:8080` |
| Diagnostics off in prod | `DOTNET_EnableDiagnostics=0` |
| No secrets in the image | Never `ENV`/`ARG` a connection string or password. **Blocking** |
| `.dockerignore` present | Must exclude `**/bin`, `**/obj`, `**/node_modules`, `**/.git`, plus non-runtime assets (`openspec`, `tests`, `docs`, `*.md`) |

### 4.2 docker-compose (local development)

- Postgres is pinned (`postgres:16-alpine`) with a `pg_isready` healthcheck.
- The API declares `depends_on: postgres: condition: service_healthy`. This is required because the
  API applies migrations on startup and does not retry — starting before Postgres accepts
  connections is a guaranteed crash loop.
- Configuration is passed as **environment variables** using the ASP.NET Core double-underscore
  convention:

```yaml
ConnectionStrings__OptimizerDb: "Host=postgres;Port=5432;Database=provider_optimizer;Username=postgres;Password=postgres"
Scoring__Eta: "0.5"
Search__DefaultRadiusKm: "10"
```

- `docker compose up --build` from a clean clone must serve the API against Postgres. That is a
  success criterion (§5 of the exercise), not a nice-to-have.

---

## 5. Architecture policies

### 5.1 The dependency rule

Dependencies point inward. `Domain` references nothing. `Application` knows ports, not
implementations. `Infrastructure` implements ports. `Api` is a thin transport shell. Enforced by
project references (§1.2).

### 5.2 Ports and adapters

Anything the domain needs from the outside world is expressed as a **port** — an interface owned by
the inner layer — and implemented by an adapter in the outer layer:

```csharp
// Application owns the interface...
namespace ProviderOptimizer.Application.Ports;

public interface IClock { DateTimeOffset UtcNow { get; } }

// ...Infrastructure owns the implementation.
namespace ProviderOptimizer.Infrastructure;

public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
```

Ports live in `Application/Ports/`. The inner layer defines the contract; the outer layer obeys it.
Never the reverse.

### 5.3 The repository is a coarse pre-filter, not the authority

The SQL query in `IProviderRepository.FindByServiceTypeAsync` filters by service type **only**. Every
*authoritative* eligibility rule — availability, radius, stable candidate ordering — lives in
`Domain/ProviderEligibility.cs`, which is pure and unit-testable without a database.

Two reasons, both deliberate:

1. **Testability.** SQL filtering cannot be unit-tested; domain rules can.
2. **One place for the rule.** Splitting eligibility between SQL and C# means two places to change
   and one place to forget.

Because the repository is only a pre-filter, a **contract test** must prove the SQL pre-filter agrees
with the domain rule. That test exists: `tests/ProviderOptimizer.IntegrationTests/ProviderRepositoryTests.cs`.
Any change to either side must keep them in agreement — **blocking** in review.

### 5.4 Structural guarantees over transactional intent

Where a rule must always hold, prefer a schema or type that makes violation *unrepresentable* over a
runtime check that must be remembered. A case carries a required `jsonb` decision column, so a case
without its decision cannot be written. `tests/.../DecisionRecordTests.cs` proves no case survives a
decision-record write failure.

### 5.5 Every decision must be explainable

The scoring policy returns a `ScoreBreakdown` listing each factor, its raw value, its weight and its
contribution — not a bare number. A score without its factors is not auditable, and this domain
(dispatching a tow truck to a stranded customer) requires that we can answer *why this provider*.

Ties are broken deterministically by provider id, so the same request always yields the same
selection.

---

## 6. Testing standards

### 6.1 Frameworks

| Concern | Choice |
|---|---|
| Framework | xUnit (`xunit` 2.9.2) |
| Runner | `Microsoft.NET.Test.Sdk` 17.11.1 |
| API hosting | `Microsoft.AspNetCore.Mvc.Testing` 8.0.32 |
| Real Postgres in tests | `Testcontainers.PostgreSql` 3.10.0 |

### 6.2 The two tiers

| Tier | Project | Scope | Speed |
|---|---|---|---|
| Unit | `tests/ProviderOptimizer.UnitTests` | Domain policy, eligibility, handlers with fake ports. **No database, no clock, no network.** | Milliseconds |
| Integration | `tests/ProviderOptimizer.IntegrationTests` | Real HTTP through `WebApplicationFactory` + **real Postgres** via Testcontainers | Seconds |

**Both tiers are mandatory for any change that touches persistence or an endpoint.** A unit test that
mocks the database does not prove the SQL is right.

### 6.3 Conventions

- Test names describe the **scenario and the guarantee**, not the method:

```csharp
// Good
public void LowerEtaWinsWhenAllOtherFactorsAreEqual() { }
public void NoCaseIsPersistedWhenTheDecisionRecordFails() { }
```

- One logical assertion per test; a test proves one guarantee.
- Arrange / Act / Assert, visually separated.
- Shared fixtures live in a `*Fixture.cs` (`PostgresFixture.cs`) — one container per collection, not
  per test.
- `public partial class Program { }` at the end of `Program.cs` exists so the integration suite can
  host the **real** application. Do not delete it, and do not replace it with a hand-built `Host`.
- Never assert on log text or on private internals. Assert on observable behaviour.

**Enforced by** the `Test` step in `ci.yml`; a red suite fails the pipeline.

---

## 7. Branch strategy, PRs and code review

### 7.1 Branch strategy — trunk-based with short-lived branches

`main` is always deployable and always green. Nobody pushes to `main` directly.

| Branch prefix | Use |
|---|---|
| `feat/` | New capability |
| `fix/` | Defect correction |
| `chore/` | Tooling, dependencies, CI |
| `docs/` | Documentation only |
| `refactor/` | Behaviour-preserving restructure |

- Branches live **days, not weeks**. If a branch outlives its review, it is too big.
- Branch name: `feat/provider-optimizer-endpoints`, lowercase, hyphen-separated.
- Rebase on `main` before requesting review; keep history linear.
- No direct commits to `main`. **Enforced by** branch protection — **to be added**.

### 7.2 PR size and chained PRs

**Target: ≤ 400 changed lines per PR.** Past that, review quality collapses — reviewers skim, and
skimming is how defects 1–8 of the code review get merged.

| Size | Action |
|---|---|
| ≤ 400 lines | One PR |
| 400–800 lines | Split, or justify in the PR description and ask the reviewer to accept the size |
| > 800 lines | **Must** be split into stacked PRs |

When the change is genuinely indivisible (a migration, a rename across the tree), say so explicitly
in the description and flag the review cost. Do not make the reviewer discover it.

### 7.3 Code review policy

| Rule | Detail |
|---|---|
| Minimum approvals | 1 (the owner of the affected area) |
| **No self-merge** | The author never approves their own PR. Even a solo squad needs a second pair of eyes; if truly nobody is available, the PR is left open for the next working day |
| Review SLA | First response within 1 business day |
| Author responsibility | Write a description stating **what**, **why**, **how it was tested**, and **what is not covered** |
| Reviewer responsibility | Review the change, not the author. Ask questions; do not rewrite in comments |

### 7.4 What blocks a PR (correctness and safety)

These are **blocking** — the PR cannot merge:

- Incorrect or missing business logic; a wrong result for a valid input.
- **Security**: hardcoded secrets, credentials in logs, SQL string concatenation, missing authz,
  PII exposure, non-parameterised queries.
- **Data integrity**: a multi-write operation without a transaction; a missing constraint.
- **Concurrency**: any shared mutable state (`static`), race conditions, unguarded read-modify-write.
- **Async correctness**: `.Result` / `.Wait()` / `async void`.
- Missing or failing tests where the change touches persistence, an endpoint or a domain rule.
- A red build or a red pipeline.
- Silently breaking an API contract.

### 7.5 What is a follow-up (not blocking)

Do not hold a PR hostage to these:

- Pure style nitpicks that a formatter should have caught — fix the formatter instead.
- **Pre-existing** problems on lines the PR did not touch.
- Subjective naming preferences with no effect on clarity.
- Optimisations with no measured impact.

**Rule:** a pre-existing problem found while reviewing is never silently fixed inside an unrelated PR.
File it (§9) or hand it back as a separate change. Mixing unrelated fixes into a PR is how an
unreviewable 900-line diff is born.

### 7.6 Reviewer checklist

- [ ] Does the change do what the description says, and only that?
- [ ] Is there a test that **fails without this change**?
- [ ] Any secret, credential or PII in the diff?
- [ ] Every SQL statement parameterised?
- [ ] Any `static` mutable state, `.Result`, or `.Wait()`?
- [ ] Multi-write operations inside one transaction?
- [ ] Do the layers still point inward (no `Domain` → `Infrastructure`)?
- [ ] Are failures explicit (correct status code, no swallowed exception)?
- [ ] Is anything observability-relevant now blind (metric, log, trace)?
- [ ] Does the size respect §7.2, or is the exception justified?

---

## 8. CI/CD policy

### 8.1 Required pipeline steps

Every push to `main` and every PR runs:

| # | Step | Command | Status |
|---|---|---|---|
| 1 | Restore | `dotnet restore ProviderOptimizer.sln` | present |
| 2 | **Lint** | `dotnet format --verify-no-changes` | **to be added** |
| 3 | Build | `dotnet build --configuration Release --no-restore` | present |
| 4 | Tests | `dotnet test --configuration Release --no-build` | present |
| 5 | **Docker build** | `docker build -t provider-optimizer:${{ github.sha }} .` | **to be added** |
| 6 | (optional) Push to ECR | `aws-actions/amazon-ecr-login` + `docker push` | optional per the exercise |

Rules:

- **A red pipeline blocks the merge. No exceptions.** "It passes locally" is not evidence.
- The lint step is `--verify-no-changes`, so formatting is a decision made once by the formatter, not
  re-litigated in every review.
- Test results are always uploaded (`if: always()`), including on failure — a failed run with no
  artifacts is undebuggable.
- The integration suite needs Docker; `ubuntu-latest` provides it.
- `workflow_dispatch` is enabled so any step can be re-run manually without an empty commit.
- Deployment to AWS is **not** in this pipeline. Real resource creation is out of scope; the topology
  is documented in `docs/aws-deployment.md`.

### 8.2 Pipelines are code

Changes to `.github/workflows/` are reviewed like any other change: same PR, same approval, same
blocking rules. A pipeline that can be weakened by an unreviewed commit is not a gate.

---

## 9. Secrets management

| Rule | Enforcement |
|---|---|
| **No secret in the repository, ever** — not in source, not in `appsettings.json`, not in a workflow file, not in a test | Blocking in review; **secret scanning to be added** (gitleaks / GitHub secret scanning with push protection) |
| Non-secret defaults only in `appsettings.json` (weights, radius, log levels) | review |
| Secrets arrive as **environment variables**; ASP.NET Core binds them via `__` | `docker-compose.yml` for local, Secrets Manager for AWS |
| The app **fails fast** when a required secret is absent | `Program.cs` throws on a missing connection string — no insecure default, ever |
| `.env`, `.env.*`, `appsettings.*.local.json`, `*.pfx` are gitignored | `.gitignore` |
| A leaked secret is **rotated immediately**, not just deleted | Incident runbook. Deleting the line does not remove it from Git history |
| Least privilege: never a `sa`/root account for the application | dedicated user, minimum grants |
| Secrets are never logged, echoed in CI, or included in an error response | review |

**The counter-example lives in this repo.** `docs/code-review/defective-snippet.cs` contains a
production `sa` password in source. That is exactly the finding that stops a merge and opens a
security incident — see `docs/code-review/review.md` §9.

---

## 10. Definition of Done

A work unit is done when **all** of the following hold. This is a checklist, not a vibe.

- [ ] The behaviour required by the spec is implemented, and **only** that behaviour.
- [ ] **Unit tests** cover the new/changed rules, including the edge cases (empty set, ties,
      boundary values, degenerate input).
- [ ] **At least one integration test** when the change touches persistence, an endpoint or a
      migration. It runs against real Postgres.
- [ ] The test suite **fails before the change and passes after** — no test written to match the
      implementation.
- [ ] `dotnet build` clean, **zero warnings** (warnings are errors in CI).
- [ ] `dotnet format --verify-no-changes` clean.
- [ ] CI is green: lint, build, tests, docker build.
- [ ] `docker compose up --build` serves the API from a clean clone.
- [ ] No secret, no credential, no PII in the diff.
- [ ] Layer references still point inward.
- [ ] Public behaviour documented (`docs/`, XML docs, spec) and every non-obvious decision commented.
- [ ] New configuration is documented **and** has a safe default or a fail-fast check.
- [ ] Review approved by someone other than the author; all blocking comments resolved.
- [ ] The change is **observable**: a failure can be diagnosed from logs/metrics/traces without
      attaching a debugger.
- [ ] Any shortcut taken has been filed as debt (§11).

---

## 11. Technical debt control

### 11.1 Policy

| Rule | Detail |
|---|---|
| Debt is **recorded, not remembered** | Every shortcut becomes a GitHub issue labelled `tech-debt` with: what was skipped, the risk if not paid, the trigger to pay it |
| Every PR that takes a shortcut **must** file it | The reviewer checks this in the same PR — a shortcut with no issue is a blocked PR |
| Debt has an owner | An unowned issue is not tracked debt, it is a wish |
| Bounded budget | The `tech-debt` list stays under a set ceiling (proposal: **15 open items**). Past it, the squad stops taking on features and pays down |
| Periodic triage | Reviewed monthly: pay, schedule, or explicitly **accept and close** with a rationale |
| Accepted debt is documented | If the squad decides to live with it, it is written down — including in `openspec` for the change. Unknown debt is the dangerous kind |

### 11.2 Debt this project knowingly carries

| # | Item | Risk if unpaid | Trigger to pay |
|---|---|---|---|
| 1 | **No authentication or authorization** — the API is open | Cannot be exposed publicly | Before the first public deployment |
| 2 | **No rate limiting** on `POST /optimize` | Abuse degrades the service for everyone; the endpoint does an outbound query per call | Before production traffic |
| 3 | **Providers are seeded and simulated** — no real telemetry | ETA/distance are not real | When a provider integration exists |
| 4 | **Scoring is computed in-process, single instance** | Does not scale horizontally with consistent low latency under load | When read volume justifies a cache |
| 5 | **No distributed cache** — Redis is in the architecture but not wired | Database load grows with traffic | Same as #4 |
| 6 | `lint` and `docker build` **absent from `ci.yml`** | Formatting drifts; the image is never validated before deploy | Immediately — required by §8.1 |
| 7 | **No secret scanning** in CI | A secret can land in the repo undetected | Immediately |
| 8 | No `.editorconfig` / analyzers (`CA2007`, `VSTHRD002`) yet | Rules in §2.5 are honoured by discipline, not tooling | Next tooling iteration |
| 9 | No structured logging / metrics / tracing | Failures are diagnosed by reading console output | Before production |
| 10 | React client not started (optional deliverable) | No user-facing surface | If the optional deliverable is picked up |

---

## 12. Enforcement summary

| Rule | Mechanism | Status |
|---|---|---|
| Layer dependency direction | Project references (compile error) | active |
| Nullable reference types | Compiler | active |
| Formatting | `dotnet format --verify-no-changes` | to be added |
| No `.Result`/`.Wait()` | Analyzer `VSTHRD002` | to be added |
| `ConfigureAwait(false)` in library code | Analyzer `CA2007` | to be added |
| No nullable warnings | `-warnaserror` in CI | to be added |
| No secret in the repo | Secret scanning + push protection | to be added |
| Red pipeline blocks merge | Branch protection | to be added |
| Tests must pass | CI `Test` step | active |
| Non-root container | `USER $APP_UID` in `Dockerfile` | active |
| No secret in the image | Review + `.dockerignore` | active |
| Debt is filed | Review checklist (§7.6) | active |

Every "to be added" above is a tracked item in §11.2. A rule that is neither enforced nor tracked is
an aspiration — and aspirations do not survive a deadline.
