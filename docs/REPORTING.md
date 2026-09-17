# Sales Reporting & Analytics

Phase 7 adds a read-only reporting/analytics backend over the CRM, sync, webhook, and automation
data built in Phases 1–6. It is a query layer only — no dashboard (Phase 10), no authentication
(Phase 8), no observability stack (Phase 9). This document defines every endpoint, every formula,
and — just as importantly — what this system honestly *cannot* calculate from the data it has.

## Architecture

```
CrmIntegration.Api            SalesReportsController, OperationsReportsController — thin, no calculations
CrmIntegration.Application    ISalesReportingService/SalesReportingService — all formulas, formatting,
                               query-param validation live here
                               IOperationsReportingService/OperationsReportingService
                               ISalesReportingRepository / IOperationsReportingRepository — interfaces only
                               Dedicated DTOs (SalesReportingResponses.cs, OperationsReportingResponses.cs)
                               Internal read-model rows (ReportingReadModels.cs) — repository<->service only,
                               never returned by an endpoint
CrmIntegration.Infrastructure  SalesReportingRepository / OperationsReportingRepository — EF Core
                               GroupBy/Sum/Count/Average projections, AsNoTracking, executed in PostgreSQL
```

No EF entity is ever returned from a controller. Controllers call one service method and wrap the
result in `Ok(...)`; every formula (rates, percentages, averages, medians, bucketing) lives in the
service layer, where it's unit-testable without a database. Every repository method executes its
aggregation as a projected PostgreSQL query — no full-table loads, no N+1 per-row queries.

**No new tables or migrations were added.** Existing indexes (`Deals.Stage`,
`AutomationExecutions.Status`, `SyncJobs.Status`, `IntegrationEvents.Status`,
`DealStageTransitions.(DealId, OccurredAt)`, `ContactLifecycleTransitions.(ContactId, OccurredAt)`)
already cover every reporting query's filter/group columns at this project's scale; adding new
indexes without a measured problem would be premature.

## Endpoints

| Endpoint | Purpose |
|---|---|
| `GET /api/reports/sales/overview` | Top-level KPIs: counts, open pipeline, won revenue, average deal size, win rate, onboarding count |
| `GET /api/reports/sales/pipeline` | Deal count/value/average by DealStage, with % of open pipeline |
| `GET /api/reports/sales/revenue?from=&to=&groupBy=day\|month` | Won revenue over time, bucketed by the Closed-Won transition date |
| `GET /api/reports/sales/outcomes?from=&to=` | Won/lost/open counts and rates |
| `GET /api/reports/sales/conversion?from=&to=` | Observed DealStageTransition counts (fromStage→toStage) |
| `GET /api/reports/sales/velocity` | Cycle-time statistics (creation → Closed Won/Lost), best-effort time-per-stage |
| `GET /api/reports/sales/lifecycle` | Contact counts/percentages by LifecycleStage, observed transitions, recent changes |
| `GET /api/reports/sales/onboarding` | OnboardingRecord counts by status, associated deal value, recent handoffs |
| `GET /api/reports/sales/activity?limit=50` | Normalized recent-activity feed across 4 event sources |
| `GET /api/reports/operations/health` | Safe aggregate SyncJob/IntegrationEvent/AutomationExecution status counts |

**Security note (temporary, development-only):** none of these endpoints are authenticated yet —
JWT bearer infrastructure exists but `[Authorize]`/role-policy enforcement is Phase 8 scope, exactly
matching the posture of every other endpoint in this project today.

## Sales Overview — formulas

- **Open/Won/Lost deals**: `COUNT(*) GROUP BY Deal.Status`.
- **Open pipeline value**: `SUM(Amount) WHERE Status = Open`, grouped by `Currency`.
- **Won revenue**: `SUM(Amount) WHERE Status = Won`, grouped by `Currency`. No date filter — this
  is "all-time won revenue," distinct from the Revenue report's date-scoped total.
- **Average deal size**: `AVG(Amount)` across **all** deals (any stage/status), grouped by
  `Currency`. This is intentionally different from the Revenue report's "average **won** deal
  size" — see that section.
- **Win rate**: `WonDeals / (WonDeals + LostDeals)`. Open deals are excluded from the denominator
  (a deal that hasn't closed yet is neither a win nor a loss). Returns `0` when there are no closed
  deals, never `NaN` or a division error.
- **Onboarding count**: `COUNT(*)` on `OnboardingRecords`.

## Pipeline — definitions

Grouped by `(Stage, Currency)`: `DealCount`, `TotalAmount = SUM(Amount)`,
`AverageAmount = AVG(Amount)`.

- **Open Pipeline** = the sum of `TotalAmount` across the three non-terminal stages
  (`QualifiedToBuy`, `Proposal`, `Negotiation`) for a given currency.
- **Won Revenue** and **Lost Value** are the `ClosedWon`/`ClosedLost` rows respectively — these are
  **never** included in "open pipeline," by definition and by code (`IsOpenStage` explicitly
  excludes them).
- `PercentageOfOpenPipeline` = this stage's `TotalAmount` / that currency's Open Pipeline total ×
  100, rounded to 2 decimals. It is `null` (not `0`) for `ClosedWon`/`ClosedLost` rows, and `null`
  when a currency's open pipeline total is `0`, to avoid a misleading "0%" that isn't really zero,
  it's undefined.

## Revenue — formulas and date source

**Closed-Won revenue date source of truth**: the **latest** `DealStageTransition.OccurredAt` where
`ToStage = ClosedWon` for that Deal — never `Deal.CreatedAt`, and never `Deal.CloseDate` (a
HubSpot-editable "expected/actual close date" property that isn't guaranteed to reflect when the
Deal's stage actually changed, and can be set by a user well before or after the real transition).
"Latest" matters because a Deal can re-enter `ClosedWon` after leaving it (see
`docs/SALES_AUTOMATION.md` "Re-entering Closed Won") — the most recent entry is what "currently
Closed Won, as of when" means.

**`ExcludedWonDealsWithoutTransitionHistory`**: a Won Deal with no such transition (data created
before Phase 6's transition tracking existed, or seeded directly bypassing the automation
pipeline) cannot be placed on any timeline. This count is **global** — every Won deal lacking
transition history, regardless of the requested date range — because a deal with no date at all
can never fall inside *any* range; scoping it to the request's range would be meaningless. This
count is a data-quality signal, not something that changes based on `from`/`to`.

- **Total won revenue** / **average won deal size**: computed only from Won deals with valid
  transition history, filtered to `[from, to]`, grouped by `Currency`.
- **Grouping**: `groupBy=day` buckets by calendar date; `groupBy=month` (default) buckets by
  `(Year, Month)`. Both are computed in UTC — see "Timezone policy" below. Invalid values return
  `400`.
- **Date range**: `from`/`to` are `yyyy-MM-dd`, inclusive on both ends (see "Timezone policy").
  Omitting either means unbounded on that side. `from > to` returns `400`.

## Won / Lost Outcomes — formulas

- **Win rate** = `Won / (Won + Lost)`; **loss rate** = `Lost / (Won + Lost)`. Both `0` when there
  are no closed deals.
- Without `from`/`to`: every Won/Lost deal counts via `Deal.Status` directly — no transition
  history required, so this always reflects the true total.
- With `from`/`to`: scoping requires the same Closed-Won/Closed-Lost transition timestamp Revenue
  uses, so deals lacking that history are excluded from the range-filtered result and counted in
  `ExcludedDealsWithoutTransitionHistory` (this field, unlike Revenue's, genuinely is 0 whenever no
  date filter is applied).

## Conversion — what this report deliberately does NOT do

`DealStage` (`QualifiedToBuy → Proposal → Negotiation → ClosedWon`/`ClosedLost`) reads like a
funnel, but it is **not schema-enforced as one**: `CreateDealRequest`/`UpdateDealRequest` allow
setting `Stage` to any value directly, and Phase 6's own live HubSpot verification proved a Deal
can leave `ClosedWon` and re-enter it. Given that, a synthesized "conversion rate from stage A to
stage B" would silently assume a strict sequence that doesn't actually hold, producing a number
that looks precise but means something different from what a reader would assume.

This report therefore returns **only observed transition counts** —
`GROUP BY (FromStage, ToStage) → COUNT(*)` from `DealStageTransitions`, optionally filtered by
`from`/`to` on `OccurredAt` — and `FunnelConversionRatesAvailable` is always `false`. A skipped
stage (e.g. `QualifiedToBuy → ClosedWon` directly) and a repeated/revisited stage both show up
honestly as their own counted rows.

## Velocity — formulas and limitations

- **Average/median days to Closed Won**: `(ClosedWonTransitionTimestamp - Deal.CreatedAt)` in
  days, across Won deals with transition history; **excludes** Won deals without it (same
  limitation as Revenue). Median: standard sorted-midpoint (average of the two middle values for
  an even count).
- **Average days to Closed Lost**: same calculation against the `ClosedLost` transition.
- **Recent Closed Won cycle times**: the 10 most recently closed (by transition timestamp) Won
  deals with their exact duration.
- **Average time per stage** (best-effort, may be sparse): for each Deal's ordered transition
  history, the time spent in a stage is measured only between a transition **into** that stage and
  the Deal's **next** transition (out of it). A Deal's current/most-recent stage is **never**
  included — its exit time isn't known yet, and estimating it against "now" would mix a
  closed-ended average with an open-ended guess. Because Phase 6's transition tracking is new, this
  metric will be sparse (or empty) until enough deals accumulate multi-hop history; this is
  expected, not a bug. **No historical stage-entry time is ever invented for a Deal created before
  Phase 6 existed** — such a Deal simply contributes no rows here until it makes a transition.

## Contact Lifecycle — formulas

- **Distribution**: `COUNT(*) GROUP BY LifecycleStage`, plus each stage's share of the total
  Contact count (rounded to 2 decimals; `0` if there are no contacts).
- **Observed transitions**: `GROUP BY (FromStage, ToStage) → COUNT(*)` from
  `ContactLifecycleTransitions` — the same "report what's observed, don't assume a funnel"
  principle as the Deal Conversion report (LifecycleStage also isn't schema-enforced as sequential).
- **Recent changes**: the 10 most recent `ContactLifecycleTransition` rows with the Contact's name.
- No marketing-attribution data (campaign source, channel, first-touch/last-touch) is fabricated —
  the schema doesn't capture it, so it isn't reported.

## Onboarding — formulas

- **Total** / **by status**: straight counts over `OnboardingRecords`.
- **Associated deal value**: `SUM(Deal.Amount)` for every Deal with an `OnboardingRecord`, grouped
  by `Currency` (an `OnboardingRecord` always has a Deal — the FK is `Cascade`, so one can't outlive
  its Deal).
- **Recent handoffs**: the 10 most recent records with Deal name/amount/currency, Company name, and
  Contact name where present (`ContactId` is optional on `OnboardingRecord`, per
  `docs/SALES_AUTOMATION.md`).
- This is reporting only — no onboarding *management* workflow (assigning owners, checklists,
  status transitions) is built here; that's out of Phase 7's scope entirely.

## Sales Activity Feed

Merges four independently-queried, already-limited, already-sorted sources —
`DealStageTransition`, `ContactLifecycleTransition`, terminal `AutomationExecution` rows
(`Succeeded`/`Skipped`/`Failed`; `Pending`/`Running` are excluded as not-yet-meaningful), and
`OnboardingRecord` creations — then merges and re-sorts by timestamp, taking the top `limit`. Each
source is queried with the same `limit` before merging, which is sufficient for the merged result
to always be correct (a standard bounded k-way-merge property): the true global top-`limit` items
can never come from beyond any one source's own top-`limit`.

`limit` defaults to 50, clamped to `[1, 200]` — a limit of `0` or a negative value is treated as
the default rather than rejected, and anything above 200 is silently capped, not rejected.

Descriptions never include `AuditLog.Metadata`, raw exception text, or payload content — each
description is built from safe, already-typed fields (`Deal.Name`, `Contact` name, `Stage`/
`LifecycleStage` enum values, `AutomationExecution.ResultSummary` which is itself never more than a
short business summary — see `docs/SALES_AUTOMATION.md`).

## Operational Sales Health

Safe aggregate counts only — this is reporting, not Phase 9 observability:

- `SyncJobsByStatus`, `IntegrationEventsByStatus`, `AutomationExecutionsByStatus`: `COUNT(*) GROUP
  BY Status` on each table.
- `*FailuresLast24Hours`: count of rows in a failing status (`SyncJob`: `Failed`/`DeadLettered`;
  `IntegrationEvent`: `Failed`/`DeadLettered`; `AutomationExecution`: `Failed`) whose start/receive
  timestamp is within the last 24 hours of `TimeProvider.GetUtcNow()`.
- **Never returned**: `SyncJob.ErrorMessage`, `IntegrationEvent.LastError`/`Payload`,
  `AutomationExecution.ErrorMessage`, or any HubSpot token/header — only status names and counts.

## Filtering and validation

- `from`/`to`: `yyyy-MM-dd`. An unparseable value, or `from > to`, returns `400` via the existing
  `DomainValidationException → 400` convention (`docs/SYNC_POLICY.md`'s error-mapping table
  applies here unchanged).
- `groupBy` (Revenue only): `day` or `month` (case-insensitive), default `month`. Anything else is
  `400`.
- `limit` (Activity only): see above — clamped, not rejected, except that it's still always a
  non-negative bound in the response.
- `dealStage`/`dealStatus` filters were considered but intentionally **not** added: Pipeline
  already partitions by stage and Outcomes already partitions by status, so a redundant filter on
  top of an already-grouped report would add API surface without adding a real capability. This is
  a deliberate scope decision, not an oversight — see `docs/DECISIONS.md`.

## Timezone policy

All timestamps in this system are stored and compared in **UTC** (matching every prior phase's
convention — `TimeProvider.GetUtcNow()`, `DateTime.UtcNow` throughout). `from`/`to` date-only
inputs are interpreted as **UTC calendar-day boundaries**: `from` = `00:00:00.000` UTC that day
(inclusive), `to` = `23:59:59.999` UTC that day (inclusive). There is no local-timezone conversion
anywhere in this layer — a caller in a non-UTC timezone is responsible for converting their
intended local date range to the equivalent UTC calendar dates before calling these endpoints.

## Currency policy

`Deal.Currency` is a **per-deal** field (`Deal.cs`), not a single portal-wide setting — although
every Deal created through this project's own services defaults to `EUR` unless the caller
specifies otherwise, the schema allows any 3-letter ISO code per deal. Given that, **every
monetary aggregate in this reporting layer is grouped by currency and never summed across
currencies** (`MoneyByCurrency` — a list of `{Currency, Amount}`, never a single bare number for
any total that could span currencies). No currency conversion is implemented or planned for this
phase — mixing currencies via an assumed exchange rate would be a fabricated number.

## Query performance approach

- Every repository method is an EF Core LINQ query ending in `GroupBy`/`Sum`/`Count`/`Average`
  projections executed as SQL aggregates in PostgreSQL, with `AsNoTracking()` throughout (no
  change-tracking overhead for read-only queries).
- Revenue/Outcomes/Velocity fetch one bounded projection of Won/Lost deals (id, name, currency,
  amount, timestamps) — not full Deal entities, and not the Open deals table at all — then compute
  bucketing/median/duration arithmetic in C#, since that fetch is already small (won+lost deals are
  a fraction of the table) and the arithmetic itself (median, day/month bucketing) doesn't map
  cleanly onto a single portable SQL aggregate.
- The Activity feed issues 4 independently-bounded queries (`Take(limit)` each) rather than one
  giant UNION or, worse, loading unbounded history — see "Sales Activity Feed" above for why this
  is still provably correct.
- No Redis, Elasticsearch, OLAP store, or materialized view was introduced — none of these
  reporting queries have shown a measured performance problem at this project's scale, and adding
  that infrastructure without one would be exactly the premature complexity the phase spec warns
  against.

## Security / privacy

No token, `ClientSecret`, `WebhookSigningSecret`, `Authorization` header, raw webhook payload,
stack trace, or internal exception detail is ever returned by any reporting endpoint — verified by
scanning every new Phase 7 file for secret-shaped values (see the Phase 7 completion report). The
Activity feed and recent-handoff/recent-change lists include Contact/Deal/Company **names**
(already returned by the existing `/api/contacts`, `/api/deals`, `/api/companies` endpoints since
Phase 2) but nothing beyond that level of exposure.

**Authentication note (temporary)**: all ten endpoints are currently unauthenticated. This matches
the project-wide posture that Phase 8 (JWT/RBAC enforcement) has not been built yet; it is not a
Phase 7-specific gap.
