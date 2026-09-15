# HubSpot CRM Integration & Sales Automation Platform

Status: **Phase 3 — HubSpot Client** (foundation, core CRM domain, and a HubSpot API client are in
place). The full README (architecture diagrams, demo script, API docs) will be written in Phase 12
once the rest of the platform is implemented — this is a placeholder covering what exists today.

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
src/CrmIntegration.Api            ASP.NET Core Web API, DI composition, Swagger, health checks
src/CrmIntegration.Application    Business logic, DTOs, service interfaces (empty scaffold so far)
src/CrmIntegration.Domain         Entities and enums, no external dependencies
src/CrmIntegration.Infrastructure EF Core (Npgsql), entity configurations, migrations
tests/CrmIntegration.UnitTests           xUnit unit tests
tests/CrmIntegration.IntegrationTests    xUnit integration tests (WebApplicationFactory)
```
