# HubSpot CRM Integration & Sales Automation Platform

Status: **Phase 8 — Security, JWT Authentication & RBAC** (foundation, core CRM domain, a HubSpot
API client, bidirectional sync, durable webhook ingestion/processing, Deal/Contact stage-change
automation with Closed-Won onboarding, a read-only Sales Operations reporting/analytics API, and
JWT authentication with role-based access control across every internal endpoint are all in
place). The full README (architecture diagrams, demo script, API docs) will be written in Phase 12
once the rest of the platform is implemented — this is a placeholder covering what exists today.

**Every internal API endpoint now requires a valid JWT and role-based authorization** — see
[docs/SECURITY.md](docs/SECURITY.md) for the full authentication/RBAC design, or jump to
"Authentication & authorization" below for the quickstart.

## Authentication & authorization

JWT bearer authentication + role-based access control (Admin, Operations, Sales, ReadOnly) protect
every internal endpoint from Phases 1–7. See [docs/SECURITY.md](docs/SECURITY.md) for the full
design (password hashing, JWT claims, refresh-token rotation/replay protection, the complete RBAC
matrix, and honest limitations — e.g. access tokens can't be revoked before they expire).

```
POST /api/auth/login       # { email, password } -> access + refresh tokens
POST /api/auth/refresh     # { refreshToken } -> rotated access + refresh tokens (anonymous)
POST /api/auth/logout      # { refreshToken } -> revokes it (requires a valid access token)
GET  /api/auth/me          # current user's id/email/role (any authenticated role)

GET  /api/users            # Admin only — list/create/change-role/activate-deactivate/reset-password
```

**Getting a token locally**: set `BootstrapAdmin__Email`/`BootstrapAdmin__Password` in `.env`
before the first run to auto-create an initial Admin (see `docs/SECURITY.md` "Admin bootstrap"),
then `POST /api/auth/login`, copy `accessToken`, and click **Authorize** in Swagger UI
(`http://localhost:5291/swagger`) to call any protected endpoint from there.

```
dotnet test tests/CrmIntegration.UnitTests --filter FullyQualifiedName~Security
dotnet test tests/CrmIntegration.IntegrationTests --filter FullyQualifiedName~Security
```
The integration tests exercise the real ASP.NET Core JWT validation pipeline (not just token
generation), the real RBAC matrix through real HTTP, and genuine concurrent-race scenarios
(refresh-token replay, last-Admin protection) against real PostgreSQL.

## Synchronization engine

Contacts, Companies, and Deals can be synchronized in both directions between PostgreSQL and
HubSpot. See:
- [docs/FIELD_MAPPING.md](docs/FIELD_MAPPING.md) — exactly which internal fields map to which
  HubSpot properties, and why some don't map at all.
- [docs/SYNC_POLICY.md](docs/SYNC_POLICY.md) — conflict handling, duplicate detection, retry/
  backoff, dead-lettering, and an honest statement of what consistency guarantees this system
  does (and does not) provide, with sequence diagrams for both sync directions.

### Development sync endpoints

**Security:** requires a JWT with the `CanManageIntegrations` policy (Admin or Operations role) —
see [docs/SECURITY.md](docs/SECURITY.md).

```
POST /api/sync/contacts/{id}/to-hubspot        # sync an internal Contact -> HubSpot
POST /api/sync/companies/{id}/to-hubspot
POST /api/sync/deals/{id}/to-hubspot
POST /api/sync/hubspot/contacts/{hubSpotId}    # import/update from a HubSpot Contact
POST /api/sync/hubspot/companies/{hubSpotId}
POST /api/sync/hubspot/deals/{hubSpotId}
GET  /api/sync/jobs/{id}                       # inspect one sync job's outcome
GET  /api/sync/jobs?limit=50                   # recent sync jobs
POST /api/sync/jobs/{id}/retry                 # manually retry a DeadLettered job
```

Example (after starting the API and creating a Company via `POST /api/companies`):
```
curl -X POST http://localhost:5291/api/sync/companies/{id}/to-hubspot
```
The response is a `SyncJobResponse` — check its `status` (`Succeeded`/`Failed`/`DeadLettered`)
and `failureCategory`/`errorMessage` if it didn't succeed.

### Running sync engine tests

```
dotnet test tests/CrmIntegration.UnitTests --filter FullyQualifiedName~Sync
dotnet test tests/CrmIntegration.IntegrationTests --filter FullyQualifiedName~Sync
```
The integration tests use a fake `IHubSpotClient` against the real Dockerized PostgreSQL — no
real HubSpot account is needed for the deterministic suite. A separate opt-in test
(`LiveSyncVerificationTests`) exercises the real HubSpot API end-to-end; see its class-level
comment for how to run it, and the Phase 4 completion report for what was verified.

## Webhooks & event processing

HubSpot webhook deliveries (Contact/Company/Deal creation and property-change events) are
persisted durably and processed asynchronously via the same Phase 4 sync services above — see
[docs/WEBHOOKS.md](docs/WEBHOOKS.md) for the full architecture, the exact HubSpot signature
algorithm implemented (v3) and why, idempotency/batch/retry/dead-letter behavior, and the current
live-verification limitations (no real HubSpot delivery has been received — see that document for
why and what to do about it in a real deployment).

```
POST /api/webhooks/hubspot                     # HubSpot webhook target (signature-verified)
GET  /api/webhooks/events?limit=50             # recent IntegrationEvents
GET  /api/webhooks/events/{id}
POST /api/webhooks/events/{id}/retry           # manually retry a DeadLettered event
```

**Security:** `POST /api/webhooks/hubspot` (ingestion) is intentionally anonymous — HubSpot itself
calls it and cannot present a JWT; it remains protected by its own v3 signature validation only.
The inspection/retry endpoints (`GET`/`POST .../events*`) require `CanManageIntegrations`
(Admin/Operations) — see [docs/SECURITY.md](docs/SECURITY.md) "HubSpot webhook authentication
exception".

```
dotnet test tests/CrmIntegration.UnitTests --filter FullyQualifiedName~Webhooks
dotnet test tests/CrmIntegration.IntegrationTests --filter FullyQualifiedName~Webhooks
```

## Sales workflow automation

Deal stage changes and Contact lifecycle-stage changes are detected and recorded regardless of
which path caused them (a HubSpot sync/webhook, or an internal REST update), and a Deal reaching
`ClosedWon` automatically creates an `OnboardingRecord` — see
[docs/SALES_AUTOMATION.md](docs/SALES_AUTOMATION.md) for the full pipeline, idempotency design
(both application-level and database-unique-index-level), and sequence diagrams.

**Security:** `AutomationExecution` inspection/retry requires `CanManageAutomations`
(Admin/Operations); the stage-history/lifecycle-history/onboarding `GET` endpoints require only
`CanReadCrm` (any authenticated role) — see [docs/SECURITY.md](docs/SECURITY.md).

```
GET  /api/automations/executions?limit=50        # recent AutomationExecutions, newest first
GET  /api/automations/executions/{id}
POST /api/automations/executions/{id}/retry       # manually retry a Failed execution
GET  /api/deals/{id}/stage-history                # full DealStageTransition history
GET  /api/contacts/{id}/lifecycle-history         # full ContactLifecycleTransition history
GET  /api/onboarding?limit=50                     # recent OnboardingRecords, newest first
GET  /api/onboarding/{id}
```

```
dotnet test tests/CrmIntegration.UnitTests --filter FullyQualifiedName~Automation
dotnet test tests/CrmIntegration.IntegrationTests --filter FullyQualifiedName~Automation
```
The integration tests use the real Dockerized PostgreSQL (a fake `IHubSpotClient` for the
sync-triggered tests) to verify the actual database-level idempotency/uniqueness constraints, not
just the application-layer logic that relies on them. A live HubSpot business-workflow
verification (Company/Contact/Deal created in a real developer portal, moved to Closed Won and
back, automation observed end-to-end, then cleaned up) is documented in the Phase 6 completion
report rather than automated, for the same reason `LiveSyncVerificationTests` isn't part of the
deterministic suite.

## Sales reporting & analytics

A read-only reporting/query layer over the CRM, sync, webhook, and automation data — see
[docs/REPORTING.md](docs/REPORTING.md) for every formula, the Closed-Won revenue-date source,
currency/timezone policy, and documented limitations (no fabricated conversion funnels, no
invented pre-Phase-6 stage-history).

**Security:** the `sales/*` reports require `CanViewSalesReports` (Admin/Operations/Sales/ReadOnly
— every role); `operations/health` requires `CanViewOperationalHealth` (Admin/Operations only).

| Endpoint | Purpose |
|---|---|
| `GET /api/reports/sales/overview` | Total contacts/companies/deals, open pipeline, won revenue, average deal size, win rate, onboarding count |
| `GET /api/reports/sales/pipeline` | Deal count/value/average by stage, % of open pipeline |
| `GET /api/reports/sales/revenue?from=&to=&groupBy=day\|month` | Won revenue over time (bucketed by the Closed-Won transition date) |
| `GET /api/reports/sales/outcomes?from=&to=` | Won/lost/open counts and rates |
| `GET /api/reports/sales/conversion?from=&to=` | Observed Deal stage-transition counts |
| `GET /api/reports/sales/velocity` | Cycle-time statistics, best-effort time-per-stage |
| `GET /api/reports/sales/lifecycle` | Contact lifecycle-stage distribution and observed transitions |
| `GET /api/reports/sales/onboarding` | Onboarding record counts/status/recent handoffs |
| `GET /api/reports/sales/activity?limit=50` | Normalized recent-activity feed |
| `GET /api/reports/operations/health` | Safe aggregate sync/webhook/automation status counts |

```
dotnet test tests/CrmIntegration.UnitTests --filter FullyQualifiedName~Reporting
dotnet test tests/CrmIntegration.IntegrationTests --filter FullyQualifiedName~Reporting
```
The integration tests seed a small deterministic dataset directly into the real Dockerized
PostgreSQL (dated far in the future to isolate it from other tests' data) and verify every major
report's numbers against explicitly pre-calculated expected values — see the Phase 7 completion
report for the full dataset and results.

## HubSpot setup

The platform authenticates to HubSpot using a **private app access token** (HubSpot's recommended
approach for a single-portal server-to-server integration like this one — see
[docs/DECISIONS.md](docs/DECISIONS.md) for why this was chosen over OAuth for the current phase).

1. In your HubSpot developer/test account: **Settings → Integrations → Private Apps → Create a
   private app**.
2. Under the **Scopes** tab, grant:
   - `crm.objects.contacts.read`, `crm.objects.contacts.write`
   - `crm.objects.companies.read`, `crm.objects.companies.write`
   - `crm.objects.deals.read`, `crm.objects.deals.write`
     (associations between these objects are covered by the same scopes — no separate
     association scope exists in the CRM v4 API used here)
3. Copy the generated access token.
4. In your local `.env` (never commit this file), set:
   ```
   HubSpot__AccessToken=<your token>
   ```
   `HubSpot__BaseUrl` defaults to `https://api.hubapi.com` and normally doesn't need changing.

**Never** put a real token in `.env.example`, appsettings.json, source code, or a commit message —
`.env.example` must only ever contain placeholders. `.env` is gitignored.

Deal stage and lifecycle stage names are portal-specific (a custom pipeline's stage ids are
arbitrary generated strings, not the enum names used internally). `HubSpot:DealStageMapping` and
`HubSpot:LifecycleStageMapping` in `appsettings.json` map this project's internal enums to your
portal's actual HubSpot property values — the defaults match HubSpot's out-of-the-box "Sales"
pipeline; override them if your test portal uses a custom pipeline.

### Running HubSpot client tests

- Unit tests (`tests/CrmIntegration.UnitTests/HubSpot/`) run against a fake HTTP handler — no
  network access or real token required:
  ```
  dotnet test tests/CrmIntegration.UnitTests --filter FullyQualifiedName~HubSpot
  ```
- There is currently no automated integration test against the real HubSpot API (see the Phase 3
  completion report for what was verified manually against a live account, if anything).

## Running Phase 1 locally

1. Copy `.env.example` to `.env` and adjust values if needed (defaults work for local dev).
   If port 5432 is already in use on your machine (e.g. a native PostgreSQL install), set
   `POSTGRES_PORT` in `.env` to a free port (e.g. `5433`) and update `ConnectionStrings__Default`
   to match.
2. Start PostgreSQL:
   ```
   docker compose up -d postgres
   ```
3. Apply migrations:
   ```
   dotnet ef database update --project src/CrmIntegration.Infrastructure --startup-project src/CrmIntegration.Api
   ```
4. Run the API:
   ```
   dotnet run --project src/CrmIntegration.Api
   ```
5. Open Swagger at `http://localhost:5291/swagger`.
6. Health endpoints: `/health`, `/health/live`, `/health/ready`.

## Running tests

- Unit tests (no dependencies): `dotnet test tests/CrmIntegration.UnitTests`
- Integration tests require the docker-compose PostgreSQL container running and migrated
  (see steps above). If it's on the default port 5432, just run:
  ```
  dotnet test tests/CrmIntegration.IntegrationTests
  ```
  If you remapped the container to a different host port (e.g. because something else already
  uses 5432), point the tests at it instead:
  ```
  $env:CRM_INTEGRATION_TEST_CONNECTION_STRING = "Host=localhost;Port=5433;Database=crm_integration;Username=crm_user;Password=change-me-locally"
  dotnet test tests/CrmIntegration.IntegrationTests
  ```

## Solution layout

```
src/CrmIntegration.Api            ASP.NET Core Web API, controllers, DI composition, Swagger, health checks
src/CrmIntegration.Application    Business logic: CRM services, HubSpot client port, sync engine (Sync/)
src/CrmIntegration.Domain         Entities and enums, no external dependencies
src/CrmIntegration.Infrastructure EF Core (Npgsql), HubSpot HTTP client, repositories, migrations
tests/CrmIntegration.UnitTests           xUnit unit tests
tests/CrmIntegration.IntegrationTests    xUnit integration tests (WebApplicationFactory)
```
