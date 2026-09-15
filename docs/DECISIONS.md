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

## Why retry the whole sync action, not just the HTTP call? (Phase 4)

`HubSpotRetryExecutor` wraps the entire entity-specific sync action (mapping lookup, duplicate
search, HubSpot call, persistence) rather than only the outbound HTTP request. Retrying just the
HTTP call would be simpler, but the action already re-checks `EntityMapping` and re-runs the
duplicate search on every invocation — making the *whole* action retryable is what turns "safe to
run repeatedly" (idempotency) into "safe to run repeatedly after a partial failure," since a retry
after a create-then-local-failure will find the just-created HubSpot record via the same
duplicate search a normal sync uses. See `docs/SYNC_POLICY.md` for the one case (Deals) where this
mitigation doesn't apply, and why.

## Why is `deal_currency_code` opt-in, not default-on? (Phase 4)

Discovered live: HubSpot rejects `deal_currency_code` with a 400 (`INVALID_OPTION`) unless the
portal has multi-currency enabled *and* the value is one of its configured currencies. Most
portals don't have multi-currency on. Sending it unconditionally would break deal sync for the
common case to support the uncommon one. `HubSpotOptions.SyncDealCurrencyCode` defaults to
`false`; portals that do use multi-currency can turn it on.

## Why does Deal sync never search for duplicates? (Phase 4)

Contact and Company both have a field confident enough to dedupe on (normalized email; normalized
domain). Deals don't — deal names collide constantly ("Renewal", "Q1 Deal") and there's no other
deterministic identifier available before a mapping exists. Searching by name and auto-attaching
would risk silently merging two unrelated deals, which is a worse outcome than occasionally
creating a duplicate that a human can merge later. `EntityMapping` remains the only protection
against duplicate Deals — see the tradeoff this implies in `docs/SYNC_POLICY.md`'s Consistency
section.

## Why does a manual dead-letter retry create a new SyncJob instead of resuming in place? (Phase 4)

`ISyncJobRetryService.RetryAsync` re-invokes the same entity/direction sync from scratch rather
than mutating the dead-lettered `SyncJob` row. The entire point of making the sync action
idempotent (mapping-aware, duplicate-search-aware) is that re-running it from scratch is safe;
building separate "resume" logic would duplicate that safety property in a second code path for
no real benefit. The original and retry jobs share a `CorrelationId` so they're easy to find
together.
