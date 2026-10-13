# Architecture

## What this service is

`ProviderOptimizerService` answers one question: **given an assistance request, which provider
should serve it, and why?** It covers steps 3, 4 and 6 of the assistance flow — search available
providers, compute the optimal provider, and record the case.

It deliberately does **not** receive requests from the mobile app, notify the selected provider,
or serve real-time tracking. Those belong to other services.

## Layers

```
ProviderOptimizer.Api          minimal API, DTOs, composition root, migrations on startup
        |
ProviderOptimizer.Infrastructure   EF Core, Npgsql, repositories  (implements the ports)
        |
ProviderOptimizer.Application      use cases and ports
        |
ProviderOptimizer.Domain           entities, value objects, scoring and eligibility rules
```

The dependency rule points inward. `Domain` references nothing; `Application` references only
`Domain`; `Infrastructure` and `Api` are the outer ring. The rule is enforced by project
references, so a violation fails the build rather than relying on review.

## Where the rules live

| Rule | Location | Why there |
|---|---|---|
| Which providers are eligible (service type, radius, availability, stable order) | `Domain/ProviderEligibility` | The provider-discovery spec requires these scenarios to be automatically testable, and SQL filtering cannot be unit-tested without a database |
| Which candidate wins (ETA, distance, availability, rating; tie-break by id) | `Domain/ProviderScoringPolicy` | Deterministic and I/O-free, so scoring is provable without a database or a clock |
| That a case always carries its decision | Schema: a required `jsonb` column on the case row | A case without a decision is not representable, so the guarantee is structural, not transactional |

The repository is therefore a **coarse pre-filter only**. The integration suite proves the SQL
pre-filter agrees with the domain rule.

## Key decisions

| Decision | Choice | Rationale |
|---|---|---|
| Layering | Four projects | The dependency rule becomes a build error, not a convention |
| HTTP surface | Minimal API | Two endpoints; controllers would add ceremony without benefit |
| Scoring location | Pure domain policy with injected weights | One code path serves both endpoints and the unit tests |
| Persistence | EF Core over Npgsql | Migrations keep the schema in one place and run inside the integration test |
| Integration database | Testcontainers Postgres | Never shares or depends on a remote database |
| Time source | Injected `IClock` | Case timestamps stay assertable |

## Contracts

```
POST /optimize                                    -> 200 OptimizePayload
                                                    409 no provider available (nothing recorded)
                                                    400 invalid request
GET  /providers/available?serviceType=&latitude=&longitude=&radiusKm=
                                                  -> 200 ProviderPayload[]
                                                    400 invalid request
```

Scoring weights are configuration, not code. A missing, negative or all-zero weight set fails
startup, so the service can never score with a degenerate configuration.

## Testing

| Layer | What it proves | How |
|---|---|---|
| Unit | Scoring order, tie-break, empty set, weight validation, discovery filters, no-persist rule | xUnit with fake ports, no I/O |
| Integration | Schema, constraints, SQL pre-filter, both repositories | Real Postgres started by Testcontainers |

Every rule was additionally checked by mutation: each rule was deliberately broken and the suite
was required to fail.
