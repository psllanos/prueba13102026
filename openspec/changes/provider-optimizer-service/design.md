# Design: provider-optimizer-service

## Technical Approach

Clean Architecture in four projects, with the dependency rule enforced by project references:
`Domain` holds entities, value objects and the scoring policy; `Application` holds the two use
cases and the ports they need; `Infrastructure` implements those ports with EF Core over Npgsql;
`Api` exposes both endpoints as a minimal API. Scoring is a pure function of a candidate set plus
injected weights, so it is testable without I/O and shared by both endpoints.

Target is **.NET 8**: this machine has only SDK `8.0.425`. Docker 29.7.2 and Node 24.19.0 are present.

## Architecture Decisions

| # | Decision | Alternatives | Choice | Rationale |
|---|----------|--------------|--------|-----------|
| D1 | Layering | Clean Architecture (4 projects) / ports and adapters in 2 / one project with folders | 4 projects | The dependency rule becomes a build error, not a convention |
| D2 | Where scoring lives | Domain policy / application service / SQL | Pure domain policy, injected weights | Deterministic, I/O-free, unit-testable; one code path for both endpoints |
| D3 | HTTP surface | Controllers / minimal API | Minimal API | Two endpoints; controllers add ceremony without benefit |
| D4 | Persistence | EF Core + Npgsql / Dapper / raw Npgsql | EF Core + Npgsql | Migrations keep the schema in one place and can run inside the integration test |
| D5 | Integration-test database | Neon / Testcontainers / in-memory provider | Testcontainers Postgres | Neon is remote, shared and TLS-constrained; in-memory providers never exercise real SQL or migrations |
| D6 | Time source | `DateTime.UtcNow` / injected clock | Injected `IClock` | Makes `decidedAt` and case timestamps assertable |

## Data Flow

```
POST /optimize
  Client        -> Api            : {serviceType, location, radiusKm}
  Api           -> Application    : OptimizeCommand
  Application   -> Infrastructure : IProviderRepository.FindCandidates(...)
  Infrastructure-> Postgres       : SELECT providers WHERE type, distance, available
  Application   -> Domain         : ProviderScoringPolicy.Select(candidates, weights)
  Domain        -> Application    : (provider, ScoreBreakdown)
  Application   -> Infrastructure : IAssistanceCaseRepository.Save(case, decision)  [one transaction]
  Api           -> Client         : 200 {caseId, selectedProvider, totalScore, breakdown, decidedAt}
```

When scoring yields no selection, the Api responds `409` and nothing is persisted. `GET
/providers/available` reuses `FindCandidates` and returns the filtered set without scoring.

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `ProviderOptimizer.sln`, `Directory.Build.props` | Create | Solution and shared build settings |
| `src/ProviderOptimizer.Domain/ProviderOptimizer.Domain.csproj` | Create | Domain project; no external references |
| `src/ProviderOptimizer.Domain/{Provider,ScoringWeights}.cs` | Create | Provider entity; validated weight set |
| `src/ProviderOptimizer.Domain/ValueObjects.cs` | Create | `ServiceType`, `GeoPoint`, `CustomerLocation`, `AssistanceRequest` |
| `src/ProviderOptimizer.Domain/ProviderScoringPolicy.cs` | Create | `Select` -> provider plus `ScoreBreakdown` |
| `src/ProviderOptimizer.Application/Ports/*.cs` | Create | `IProviderRepository`, `IAssistanceCaseRepository`, `IClock` |
| `src/ProviderOptimizer.Application/OptimizeProviderHandler.cs` | Create | Candidate lookup, scoring, case persistence |
| `src/ProviderOptimizer.Application/ListAvailableProvidersHandler.cs` | Create | Filtering for the availability endpoint |
| `src/ProviderOptimizer.Infrastructure/Persistence/OptimizerDbContext.cs` | Create | EF Core model: providers, cases, decisions |
| `src/ProviderOptimizer.Infrastructure/Persistence/ProviderRepository.cs` | Create | Implements both repository ports |
| `src/ProviderOptimizer.Infrastructure/Migrations/` | Create | Initial schema migration |
| `src/ProviderOptimizer.Api/Program.cs` | Create | Composition root; migrations applied on start |
| `src/ProviderOptimizer.Api/Endpoints/*.cs` | Create | `POST /optimize`, `GET /providers/available` |
| `src/ProviderOptimizer.Api/Contracts/*.cs` | Create | Request and response DTOs |
| `tests/ProviderOptimizer.UnitTests/*.cs` | Create | Scoring, filtering, weight validation |
| `tests/ProviderOptimizer.IntegrationTests/*.cs` | Create | `WebApplicationFactory` + Testcontainers |
| `Dockerfile`, `docker-compose.yml`, `.dockerignore` | Create | Service and Postgres |
| `.github/workflows/ci.yml` | Create | Restore, build, test |
| `.gitignore` | Modify | Add .NET, Node and env entries |

## Interfaces / Contracts

```
POST /optimize
  { "serviceType": "locksmith",
    "location": { "latitude": -34.6037, "longitude": -58.3816 },
    "searchRadiusKm": 10 }
  -> 200 { "caseId": "...", "selectedProvider": { "id": "...", "name": "..." },
           "totalScore": 0.87,
           "breakdown": [ { "factor": "eta", "weight": 0.4, "contribution": 0.35 } ],
           "decidedAt": "2026-10-13T12:00:00Z" }
  -> 409 no provider available (nothing persisted)
  -> 400 customer location cannot be resolved

GET /providers/available?serviceType=locksmith&latitude=..&longitude=..&radiusKm=10
  -> 200 [ { "id": "...", "name": "...", "rating": 4.6, "available": true } ]
```

The scoring seam (non-obvious, so shown explicitly):

```csharp
public interface IProviderScoringPolicy
{
    SelectionResult Select(IReadOnlyCollection<Provider> candidates, ScoringWeights weights);
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|--------------|----------|
| Unit | Scoring order, tie-break, single selection, empty set, radius and service-type filtering, weight validation | xUnit, in-memory, no I/O |
| Integration | Both endpoints over real HTTP against a real Postgres, including case + decision persistence and transactional rollback | `WebApplicationFactory` + Testcontainers, migrations applied on startup |
| E2E | None | N/A - no UI or browser surface in this change |

## Threat Matrix

N/A - this design adds no routing, shell-command, subprocess, version-control automation, PR
automation, executable-file classification, or process-integration boundary. It exposes HTTP
endpoints and a database adapter only.

## Migration / Rollout

No migration required - greenfield repository. The initial EF Core migration creates the schema and
`docker-compose.yml` provisions an empty Postgres for local runs. The Neon connection string is
supplied through the `ConnectionStrings__OptimizerDb` environment variable only.

## Open Questions

- [ ] Is `searchRadiusKm` per-request or configuration-only? Assumed per-request with a configured default.
- [ ] Confirm .NET 8 as the target: only SDK 8.0.425 is installed, so .NET 7 is not available locally.
