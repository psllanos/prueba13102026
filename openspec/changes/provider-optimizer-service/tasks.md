# Tasks: provider-optimizer-service

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 1400-1800 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 -> PR 2 -> PR 3 -> PR 4 |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

```text
Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High
```

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Solution skeleton and Domain scoring policy | PR 1 | `dotnet test tests/ProviderOptimizer.UnitTests` | N/A - pure in-memory policy, no runtime boundary | `src/ProviderOptimizer.Domain/`, `tests/ProviderOptimizer.UnitTests/` |
| 2 | Application use cases and ports | PR 2 | `dotnet test tests/ProviderOptimizer.UnitTests` | N/A - handlers exercised with fake ports | `src/ProviderOptimizer.Application/` |
| 3 | EF Core persistence and migrations | PR 3 | `dotnet test tests/ProviderOptimizer.IntegrationTests` | `docker compose up -d postgres`, then the integration suite | `src/ProviderOptimizer.Infrastructure/`, `tests/ProviderOptimizer.IntegrationTests/` |
| 4 | API surface, packaging, CI and docs | PR 4 | `dotnet test` | `docker compose up --build`, then a real `POST /optimize` | `src/ProviderOptimizer.Api/`, `Dockerfile`, `docker-compose.yml`, `.github/`, `docs/` |

## Phase 1: Solution Skeleton and Domain

- [x] 1.1 Create `ProviderOptimizer.sln` and `Directory.Build.props` targeting `net8.0` with nullable enabled
- [x] 1.2 Create `src/ProviderOptimizer.Domain/ProviderOptimizer.Domain.csproj` with no external references
- [x] 1.3 Add `ServiceType`, `GeoPoint`, `CustomerLocation` and `AssistanceRequest` in `src/ProviderOptimizer.Domain/ValueObjects.cs`
- [x] 1.4 Add the `Provider` entity and weight validation in `src/ProviderOptimizer.Domain/ScoringWeights.cs`
- [x] 1.5 Implement `src/ProviderOptimizer.Domain/ProviderScoringPolicy.cs`: composite score, `ScoreBreakdown`, deterministic tie-break, single selection

## Phase 2: Application Use Cases

- [x] 2.1 Create `src/ProviderOptimizer.Application/Ports/` with `IProviderRepository`, `IAssistanceCaseRepository` and `IClock`
- [x] 2.2 Implement `src/ProviderOptimizer.Application/OptimizeProviderHandler.cs`: candidates, scoring, case plus decision in one transaction
- [x] 2.3 Implement `src/ProviderOptimizer.Application/ListAvailableProvidersHandler.cs` reusing candidate lookup only

## Phase 3: Infrastructure and API

- [ ] 3.1 Map providers, cases and decisions in `src/ProviderOptimizer.Infrastructure/Persistence/OptimizerDbContext.cs`
- [ ] 3.2 Implement `src/ProviderOptimizer.Infrastructure/Persistence/ProviderRepository.cs` for both repository ports
- [ ] 3.3 Generate the initial schema migration in `src/ProviderOptimizer.Infrastructure/Migrations/`
- [ ] 3.4 Build `src/ProviderOptimizer.Api/Program.cs` as composition root, applying migrations on startup
- [ ] 3.5 Add request and response DTOs in `src/ProviderOptimizer.Api/Contracts/`
- [ ] 3.6 Add `src/ProviderOptimizer.Api/Endpoints/OptimizeEndpoint.cs` for `POST /optimize` returning 200, 400 and 409
- [ ] 3.7 Add `src/ProviderOptimizer.Api/Endpoints/AvailableProvidersEndpoint.cs` for `GET /providers/available`

## Phase 4: Tests

- [x] 4.1 Unit tests in `tests/ProviderOptimizer.UnitTests/ProviderScoringPolicyTests.cs` covering every provider-scoring scenario
- [x] 4.2 Unit tests in `tests/ProviderOptimizer.UnitTests/ProviderDiscoveryTests.cs` covering every provider-discovery scenario
- [ ] 4.3 Add `tests/ProviderOptimizer.IntegrationTests/OptimizerApiFactory.cs` with `WebApplicationFactory` and a Testcontainers Postgres
- [ ] 4.4 Integration test `tests/ProviderOptimizer.IntegrationTests/OptimizeEndpointTests.cs`: persistence, the 409 path and the 400 path
- [ ] 4.5 Integration test `tests/ProviderOptimizer.IntegrationTests/AvailableProvidersEndpointTests.cs`: filtering and stable order
- [ ] 4.6 Integration test proving no case survives a decision-record write failure

## Phase 5: Packaging and CI

- [ ] 5.1 Add `Dockerfile` and `.dockerignore` for the Api
- [ ] 5.2 Add `docker-compose.yml` with the Api and Postgres
- [ ] 5.3 Add `.github/workflows/ci.yml` running restore, build and test
- [x] 5.4 Extend `.gitignore` for .NET, Node and environment files
- [ ] 5.5 Document the AWS topology and architecture decisions in `docs/`
