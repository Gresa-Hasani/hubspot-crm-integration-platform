# Architecture Decisions

Running log of decisions made so far, and why. Updated as later phases add new ones.

## Why PostgreSQL?

Relational integrity matters for the mapping/audit tables (`EntityMapping`, `IntegrationEvent`,
`AuditLog`) that the sync engine will depend on, and EF Core's Npgsql provider is mature. No need
for a document store or multi-model database at this scale.

## Why ASP.NET Core / .NET 8?

.NET 8 is the current LTS release; ASP.NET Core gives built-in DI, health checks, and OpenAPI
generation without extra frameworks — appropriate for a single deployable integration service.

## Why separate HubSpot DTOs from domain entities?

`HubSpotObjectDto`/`HubSpotSearchResponseDto` (Infrastructure) mirror HubSpot's exact wire format
(property bags, `createdAt`/`updatedAt`, paging cursors) and would leak HubSpot's data model into
business logic if used directly. `HubSpotRecord` (Application) is the client's actual contract —
an opaque property dictionary plus HubSpot's id/timestamps, with no dependency on HubSpot's JSON
shape. The Phase 4 sync engine will map between `HubSpotRecord` and domain entities explicitly,
the same way `Normalization`/`HubSpotStageMapper` already do for individual fields.

## Why a private app access token instead of OAuth 2.0 for HubSpot?

The project specification allows either. A private app token is HubSpot's recommended approach
for a single-portal, server-to-server integration with no per-user authorization flow — exactly
this project's shape. Implementing OAuth (authorization code exchange, refresh token rotation, a
redirect endpoint) would add real complexity with no corresponding benefit here: there's one
HubSpot portal, not multiple customers each authorizing their own. Building a token-refresh OAuth
flow "for completeness" without an actual multi-tenant use case would be exactly the kind of fake
complexity the project spec says to avoid. If a future phase needs multi-portal support, OAuth
becomes the right choice and can be added without changing `IHubSpotClient`'s contract.

## Why an explicit, configurable deal-stage/lifecycle-stage mapping?

HubSpot pipeline stage ids are portal-specific — a custom pipeline's stages are arbitrary
generated strings (e.g. `"1064328593"`), not human-readable names. Assuming `DealStage.Proposal`
maps to some predictable HubSpot id would silently break the moment a real portal uses a custom
pipeline. `HubSpotOptions.DealStageMapping`/`LifecycleStageMapping` make the mapping explicit and
overridable per environment instead of guessed at runtime.
