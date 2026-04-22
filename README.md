# FocusTrack

Backend platform for logging deep-work and learning sessions, earning daily badges, sharing with colleagues, and giving admins aggregated insights.

Built on .NET 8 with Clean Architecture, CQRS-style read models, MassTransit over RabbitMQ for async events, SignalR for realtime notifications, and Auth0 via OIDC + PKCE.

![Architecture](docs/architecture.png)

## One-command startup

```bash
docker compose up --build
```

The first build takes 5–10 minutes. After that the stack runs on:

| Component            | URL                                       |
|----------------------|-------------------------------------------|
| Gateway (BFF)        | http://localhost:5000                     |
| Session Service      | http://localhost:5101 (direct)            |
| Notification Service | http://localhost:5102 (direct)            |
| RabbitMQ UI          | http://localhost:15672 (guest / guest)    |
| Jaeger UI            | http://localhost:16686                    |
| Mailhog UI           | http://localhost:8025                     |
| SQL Server           | `localhost,1433` (sa / FocusTrack!2026)   |

### Auth0 config

Create a `.env` next to `docker-compose.yml`:

```env
AUTH_AUTHORITY=https://your-tenant.us.auth0.com/
AUTH_DOMAIN=your-tenant.us.auth0.com
AUTH_CLIENT_ID=...
AUTH_CLIENT_SECRET=...
AUTH_AUDIENCE=https://focustrack-api
```

Register `http://localhost:5000/signin-oidc` in the Auth0 app's **Allowed Callback URLs** and `http://localhost:5000` in **Allowed Logout URLs** and **Allowed Web Origins**.

## Architecture

FocusTrack is a **modular monolith at the repo level, split into independent runtime services**. Each service has its own database and its own MassTransit consumer group; the only cross-service dependency is through asynchronous events on RabbitMQ.

- **FocusTrack.Gateway** — YARP reverse proxy and BFF. Handles Auth0 cookie-based OIDC login (Authorization Code + PKCE), forwards the bearer token to downstream services, and refreshes the access token before it expires. Also consumes `UserStatusChangedEvent` to maintain an in-memory suspended-user cache for fast deny-lookups.
- **Session Service** — the write side. Session is the aggregate root, `DurationMin` is a value object, and every state change raises a domain event. EF Core + SQL Server for persistence. Admin endpoints (session filtering, monthly stats, user status) live here too.
- **Notification Service** — consumes `SessionSharedEvent` and `DailyGoalAchievedEvent`. Pushes realtime messages to online users via SignalR, falls back to email for offline users. Has its own SQL Server database tracking notification preferences.
- **Reward Worker** — consumes `SessionCreatedEvent`, evaluates the user's running total for the day against the 120-minute goal, marks the triggering session, and publishes `DailyGoalAchievedEvent`.

### Event flow

```
POST /api/sessions
   └─> Session.Presentation
         └─> MediatR → CreateSessionHandler
               ├─> EF Core: INSERT Session
               └─> MassTransit.Publish(SessionCreatedEvent)
                     ├─> RewardWorker.SessionCreatedConsumer
                     │     └─> if daily goal reached, Publish(DailyGoalAchievedEvent)
                     │           └─> Notification.DailyGoalAchievedConsumer
                     └─> (any future projection handler)
```

### Clean Architecture layering (per service)

```
Service.Domain          ← entities, value objects, domain events. No external deps.
   ↑
Service.Application     ← MediatR commands/queries, FluentValidation, MassTransit contracts
   ↑
Service.Infrastructure  ← EF Core DbContext, repositories, migrations
   ↑
Service.Presentation    ← ASP.NET controllers, DI wiring, middleware
```

## Testing

3 test projects with ~55 tests total:

| Project | What it covers |
|---|---|
| `Session.UnitTests` | Domain invariants, `DurationMin`, validators, command/query handlers over EF InMemory |
| `RewardWorker.UnitTests` | The brief-mandated **119.99 / 120.00 / 120.01** boundary cases + consumer idempotence |
| `Notification.UnitTests` | Entity, `OnlineTracker`, `SessionShared`/`DailyGoalAchieved` consumers |

Run:

```bash
dotnet test
```

Coverage:

```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator "-reports:**/coverage.cobertura.xml" "-targetdir:coverage-report" "-reporttypes:HtmlInline;TextSummary"
cat coverage-report/Summary.txt
```

## Key design decisions

| Decision | Why |
|---|---|
| Modular monolith in one solution, independent runtime services in compose | Keeps the interview-sized scope tractable while proving the boundary design. Splitting into separate repos is a mechanical refactor. |
| CQRS-style `MonthlyFocusProjection` | Monthly stats need a shape the write model doesn't; projecting keeps the admin query fast and the write model clean. |
| BFF pattern at the Gateway | Tokens never touch the browser — Auth0 refresh tokens stay server-side. The Gateway exchanges them for bearer tokens it forwards downstream. |
| Structured JSON logging with CorrelationId | Every request gets a correlation id (inherited from `X-Correlation-ID` header or generated). It propagates to every service and every log line. Ready for ingestion into any log aggregator. |
| MassTransit over raw RabbitMQ client | Built-in retry, DLQ, consumer contracts, test harness. |
| In-memory `OnlineTracker` in Notification | Fine for single-instance; production would swap for Redis-backed state. |

## Known trade-offs

1. **`CreateSessionHandler` publishes after SaveChanges, outside a transaction.** Classic dual-write problem — if the app crashes between `SaveChangesAsync` and `Publish`, the DB row exists but no event is emitted. Production fix: MassTransit's EF outbox (`x.AddEntityFrameworkOutbox<AppDbContext>(...)`).
2. **SignalR connection state is in a static dictionary.** Breaks horizontal scaling of the notification service. Production fix: SignalR Redis backplane + `IOnlineTracker` reading from Redis.
3. **Observability stack (Jaeger, Prometheus) is in compose but services don't emit yet.** Adding OpenTelemetry is a per-service change (`AddOpenTelemetry().WithTracing(...).WithMetrics(...)`).

## Production deployment & scaling

- Deploy each service as its own container image with a Helm chart or Kustomize overlay per environment. Dockerfiles are already multi-stage and run as non-root `app` user.
- Scaling:
  - **Gateway** — stateless; scale horizontally behind a load balancer with sticky sessions for SignalR.
  - **Session Service** — stateless; scale on HTTP RPS.
  - **Notification Service** — needs Redis backplane before scaling (see trade-off #2).
  - **Reward Worker** — stateless; MassTransit's competing-consumer semantics handle concurrent consumers safely.
- Run migrations as a dedicated job in the Helm release rather than on service startup, to avoid startup races between replicas.
- Replace Mailhog with a real SMTP provider (SendGrid, SES, Postmark).

## Repository layout

```
FocusTrack/
├── FocusTrack.sln
├── docker-compose.yml
├── .env                          # (gitignored — Auth0 credentials)
├── Contracts/                    # Shared integration event records
├── Infrastructure/               # CorrelationIdMiddleware, shared middleware
├── Session.Domain/               # Aggregates, value objects, domain events
├── Session.Application/          # MediatR handlers, validators
├── Session.Infrastructure/       # EF Core, repositories, migrations
├── Session.Presentation/         # ASP.NET controllers, Program.cs, Dockerfile
├── Notification.* /              # Same layering for the notification service
├── RewardWorker/                 # BackgroundService + consumers
├── FocusTrack.Gateway/           # YARP + OIDC + middleware
├── tests/                        # Unit + integration tests
├── ops/prometheus/               # Scrape config + alert rules
└── docs/architecture.png
```
