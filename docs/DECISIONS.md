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

## Why HubSpot webhook signature v3, not v1/v2? (Phase 5)

Verified directly against HubSpot's current documentation rather than assumed: v1/v2 are hex-
encoded, have no timestamp, and provide no replay protection; HubSpot's own docs describe v3 (Base64,
HMAC-SHA256 over method+URI+body+timestamp, with a 5-minute freshness window) as "the latest and
most secure version." v1/v2 remain supported only for backwards compatibility with old
integrations — a new implementation has no reason to use them. See `docs/WEBHOOKS.md` for the
full algorithm and sources.

## Why is event claiming a single conditional UPDATE, not row-locking? (Phase 5)

`IIntegrationEventRepository.TryClaimNextAsync` issues one `UPDATE ... WHERE Id = x AND Status =
'Received'` (via EF Core's `ExecuteUpdateAsync`) rather than `SELECT ... FOR UPDATE SKIP LOCKED`
or an external distributed lock. The conditional `WHERE` clause already makes the state transition
atomic at the database level — two concurrent workers racing for the same row will have exactly
one `UPDATE` affect a row and the other affect zero, with no explicit locking needed. Adding
row-level locking or a distributed lock service would be solving a problem this simpler approach
already solves, for a portfolio-scale single-database deployment.

## Why two separate, bounded retry layers instead of one? (Phase 5)

Phase 4's `HubSpotRetryExecutor` (tight, second-scale exponential backoff, bounded by
`SyncRetryOptions.MaxAttempts`) and Phase 5's `IntegrationEventProcessor` (coarse, poll-interval-
scale re-attempts, bounded by `WebhookOptions.MaxProcessingAttempts`) solve different problems: the
former absorbs brief blips (a rate limit, one flaky 5xx) within a single sync call; the latter
gives a longer-outage extra wall-clock time across separate processing passes without re-running a
tight retry loop that already ran to completion. A deterministic failure (Phase 4 throws rather
than returning a `DeadLettered` `SyncJob`) is retried at **neither** layer — see
`docs/WEBHOOKS.md`'s "Retry boundary" for the full table and reasoning.

## Why does IntegrationEvent.Payload store parsed fields, not the raw HTTP request? (Phase 5)

Storing the full raw request would mean storing headers (including, if ever misconfigured,
Authorization) and unbounded/unvalidated JSON. `IntegrationEvent.Payload` stores only the already-
parsed, already-validated event fields (eventId, subscriptionType, objectId, propertyName,
occurredAt, attemptNumber) as JSON — sufficient for diagnostics and replay, with nothing beyond
what's needed. See `docs/WEBHOOKS.md`'s "Security considerations".

## Why is AutomationExecution.IdempotencyKey derived per-transition, not per-Deal? (Phase 6)

A naive `DealClosedWonOnboarding:{dealId}` key would make it impossible to distinguish "this Deal
has never triggered onboarding automation" from "this Deal already went through Closed Won once
and came back" — the second Closed Won entry needs its own auditable `AutomationExecution` row
(the spec is explicit that re-entering Closed Won must still produce a fresh, visible attempt), but
must not create a second `OnboardingRecord`. Keying by `DealStageTransition.Id` instead solves
exactly this: every meaningful transition gets its own execution row, while the actual duplicate-
`OnboardingRecord` protection comes from `OnboardingRecords.DealId`'s own unique index (a Phase 1
constraint, not new) and `OnboardingService`'s own DealId lookup — two different constraints for
two different invariants, not one constraint doing double duty. See `docs/SALES_AUTOMATION.md`
"Re-entering Closed Won".

## Why is automation idempotency enforced at both the application layer and the database? (Phase 6)

`AutomationExecutor` checks for an existing terminal execution before doing any work — this avoids
a redundant `INSERT` attempt (and its associated `SaveChangesAsync` round-trip) in the overwhelming
majority of calls, which are not concurrent races. But an application-level check-then-insert is
inherently racy under real concurrency (two callers can both pass the check before either commits),
so `AutomationExecutions.IdempotencyKey` also has a database-level unique index as the actual
correctness guarantee, with the resulting Postgres unique-violation translated to
`SyncMappingConflictException` — the same translation `UnitOfWork` already performs for
`EntityMappings` (Phase 4) and `OnboardingRecords.DealId` (Phase 6). This mirrors the general
pattern established for `IIntegrationEventRepository.TryClaimNextAsync` (Phase 5): the database
constraint is the source of truth, the application check is an optimization on top of it, not a
replacement for it.

## Why does Contact lifecycle automation attach no side-effecting business action? (Phase 6)

Every real LifecycleStage transition is recorded and run through the same
`AutomationExecutor`/idempotency machinery as Closed-Won onboarding, but no email, marketing
campaign enrollment, or task creation is fabricated for any specific stage. The Phase 6
specification is explicit that inventing such integrations (with no corresponding infrastructure —
no email service, no task system — actually built) would be exactly the kind of fake complexity
the project avoids elsewhere. This still delivers real value: a durable, already-idempotent,
audited hook that a later phase can attach a genuine action to without redesigning this pipeline.

## Why does automation run after the triggering save, not inside the same transaction? (Phase 6)

A Deal's Stage (and a Contact's LifecycleStage) must never depend on whether downstream automation
succeeds — the entity update is the authoritative, already-durable fact; onboarding is a
consequence of it, not a precondition for it. This mirrors Phase 5's "process only after durable
persistence" boundary for the same reason: a failed automation is visible
(`AutomationExecution.Status = Failed`) and retryable, never silently lost, but it also never rolls
back or blocks the state change that triggered it. See `docs/SALES_AUTOMATION.md` "Transaction
boundaries".
