# Proposal: provider-optimizer-service

## Intent

Deliver one microservice that decides *which* assistance provider (tow truck, locksmith, battery)
should serve a request, and why. No reproducible demonstration of that decision exists today.
`ProviderOptimizerService` makes provider selection explicit, testable, and auditable, covering
scenario steps **3-4 and 6**.

## Scope

### In Scope
- `POST /optimize` - return the selected provider with a score breakdown.
- `GET /providers/available` - list providers eligible for a service type and location.
- Deterministic scoring over ETA, distance, availability, and rating.
- PostgreSQL persistence of the assistance case and its decision record.
- Clean Architecture layout: Domain, Application, Infrastructure, Api.
- Unit tests plus at least one integration test (`WebApplicationFactory` + real Postgres).
- `Dockerfile`, `docker-compose.yml`, GitHub Actions CI, and an AWS topology design.

### Out of Scope
- Mobile app, React client, provider notification, real-time tracking, auth.
- Live provider telemetry - providers are seeded and simulated.
- Real AWS resource creation.

## Capabilities

### New Capabilities
- `provider-discovery`: which providers are eligible for a service type and location.
- `provider-scoring`: how a candidate set is ranked into one optimal provider.
- `assistance-case-lifecycle`: case creation persisted with its decision record.

### Modified Capabilities
None.

## Approach

Dependency rule enforced inward: `Domain` (entities, value objects, `ProviderScoringPolicy`)
<- `Application` (use cases, ports) <- `Infrastructure` (EF Core + Npgsql) and `Api` (minimal API).
Scoring is a pure domain policy, testable without a database; the ports (`IProviderRepository`,
`IClock`, `IScorePolicy`) are injected so integration tests use disposable Postgres.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/ProviderOptimizer.{Domain,Application,Infrastructure,Api}/` | New | Clean Architecture projects |
| `tests/ProviderOptimizer.{UnitTests,IntegrationTests}/` | New | Scoring; API + Postgres container |
| `Dockerfile`, `docker-compose.yml`, `.github/workflows/ci.yml` | New | Service, Postgres, CI |
| `docs/` | New | AWS topology, decisions |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Neon TLS/`channel_binding` incompatible with Npgsql | Med | `SslMode=Require`; tests use a local container |
| Credentialed connection string leaks | High | Env vars only; `.env` gitignored |
| Scoring weights arbitrary | Med | Weights in config; tests pin expected ranking |
| No test runner yet; Strict TDD off | Med | First task creates test projects; re-run `sdd-init` |

## Rollback Plan

Nothing is deployed and the repo has no commits. Delete
`openspec/changes/provider-optimizer-service/`, `src/`, `tests/`, `Dockerfile`,
`docker-compose.yml`, and `.github/` - all new, nothing to restore. After the first
commit, `git revert` the work-unit commits.

## Dependencies

- .NET 8 SDK, Docker Desktop, PostgreSQL.
- Neon connection string - environment variable only.

## Success Criteria

- [ ] `POST /optimize` returns exactly one provider with a score breakdown.
- [ ] `GET /providers/available` returns only providers matching type and radius.
- [ ] Unit suite and one Postgres integration test green.
- [ ] `docker compose up` serves the API against Postgres from a clean clone.
- [ ] CI passes build and tests on push.
