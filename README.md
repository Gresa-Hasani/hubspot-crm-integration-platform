# HubSpot CRM Integration & Sales Automation Platform

Status: **Phase 1 — Foundation** (solution structure, database, Docker, Swagger, health checks).
The full README (architecture diagrams, demo script, API docs) will be written in Phase 12 once the
rest of the platform is implemented — this is a placeholder covering what exists today.

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
