# AWS deployment topology (design only)

> **Nothing in this document is provisioned.** No AWS resource has been created, and the
> repository contains no credentials, no account identifiers and no deployment automation.
> This is the target shape for a later delivery.

## Target topology

```
                    Route 53 (api.example.com)
                              |
                        CloudFront                 optional: cache GET /providers/available
                              |
                    Application Load Balancer      public subnets, TLS via ACM
                              |
        +---------------------+---------------------+
        |                                           |
   ECS Fargate task                              ECS Fargate task
   ProviderOptimizer.Api                        ProviderOptimizer.Api
   (private subnet, awslogs)                    (private subnet, awslogs)
        |                                           |
        +---------------------+---------------------+
                              |
                    RDS for PostgreSQL / Aurora Serverless v2
                    (private subnets, Multi-AZ, encrypted at rest)
```

## Component mapping

| Concern | AWS service | Notes |
|---|---|---|
| Image registry | ECR | One repository per service; images are built by CI, never by hand |
| Runtime | ECS Fargate | No host to patch; task role instead of instance role |
| Load balancing | Application Load Balancer | Health check on `/providers/available` with a known seed row |
| Database | RDS for PostgreSQL or Aurora Serverless v2 | The Neon connection string used in development maps to a managed endpoint here |
| Secrets | Secrets Manager | Referenced by the task definition as `ConnectionStrings__OptimizerDb`; never baked into the image |
| Networking | VPC, private subnets, security groups | Tasks are unreachable from the internet except through the ALB |
| DNS and TLS | Route 53 + ACM | Certificate attached to the ALB listener |
| Logs and metrics | CloudWatch Logs, Container Insights | Structured logs; alarms on 5xx rate and task restarts |
| Scaling | Application Auto Scaling | Target tracking on average CPU, plus ALB request count per target |

## Configuration and secrets

The service reads three things from the environment and nothing from files in the image:

| Variable | Source |
|---|---|
| `ConnectionStrings__OptimizerDb` | Secrets Manager |
| `Scoring__Eta`, `Scoring__Distance`, `Scoring__Availability`, `Scoring__Rating` | Task definition environment |
| `Search__DefaultRadiusKm` | Task definition environment |

A missing or all-zero weight set fails startup, which surfaces as a task that never becomes
healthy rather than a service that silently scores everything identically.

## Deployment sequence (when it is eventually built)

1. CI builds and pushes an image tagged with the commit SHA to ECR.
2. A migration task runs `dotnet ef database update` once, before the new service revision.
3. The ECS service is updated with the new task definition revision.
4. The ALB drains the old tasks once the new ones pass the health check.

Migrations run on API startup today, which is fine for a single replica. Before running more than
one replica, move migration to the dedicated task in step 2 so replicas do not race.

## Explicit non-goals

- No multi-region active-active topology.
- No real-time tracking pipeline, notification fan-out or mobile gateway.
- No WAF, no Shield Advanced, no custom threat model.
- No cost model; sizing is left to the delivery that builds it.
