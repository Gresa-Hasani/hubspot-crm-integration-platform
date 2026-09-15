# Field Mapping

This document is the source of truth for how internal domain fields map to HubSpot CRM
properties. The actual mapping code lives in `src/CrmIntegration.Application/Sync/Mapping/`
(`ContactHubSpotMapper`, `CompanyHubSpotMapper`, `DealHubSpotMapper`) — this document explains
*why* each mapping (or non-mapping) exists; the code is the executable version of it.

Only fields that actually exist on the internal domain entities are mapped. No fields were added
to the domain model solely to complete a mapping.

## Contact

| Internal field (`Contact`) | HubSpot property | Direction | Normalization / transformation | Notes |
|---|---|---|---|---|
| `Email` | `email` | Both | `Normalization.NormalizeEmail` (trim + lowercase) on import | Primary duplicate-matching key (see "Duplicate Detection" below) |
| `FirstName` | `firstname` | Both | Trim on import | |
| `LastName` | `lastname` | Both | Trim on import | |
| `Phone` | `phone` | Both | `Normalization.NormalizePhone` on import | |
| `JobTitle` | `jobtitle` | Both | Trim on import | |
| `LifecycleStage` | `lifecyclestage` | Both | Via `IHubSpotStageMapper` (`HubSpotOptions.LifecycleStageMapping`) | Unknown incoming values are ignored (existing internal value kept) rather than guessed |
| `CompanyId` | *(association, not a property)* | Both | Resolved through `EntityMapping`, not a HubSpot property | See "Associations" |
| `Source` | *(not mapped)* | — | — | No safe writable HubSpot equivalent for an arbitrary internal lead-source string; `hs_analytics_source` is HubSpot-computed/read-only, not something we can set meaningfully |
| `Id`, `HubSpotId`, `CreatedAt`, `UpdatedAt`, `LastSyncedAt` | — | — | — | Sync engine bookkeeping, not synced as CRM properties |

## Company

| Internal field (`Company`) | HubSpot property | Direction | Normalization / transformation | Notes |
|---|---|---|---|---|
| `Name` | `name` | Both | `Normalization.NormalizeCompanyName` (trim + collapse whitespace) on import | |
| `Domain` | `domain` | Both | `Normalization.NormalizeDomain` (strip protocol/`www.`/path) on import | Primary duplicate-matching key |
| `Industry` | `industry` | Both | Trim on import | |
| `Country` | `country` | Both | `Normalization.NormalizeCountry` (alias table) on import | |
| `EmployeeCount` | `numberofemployees` | Both | Parsed as `int` on import; ignored if not numeric | |
| `Id`, `HubSpotId`, `CreatedAt`, `UpdatedAt`, `LastSyncedAt`, `Contacts`, `Deals` | — | — | — | Bookkeeping / navigation properties, not synced |

Company has no `Phone` field internally, so `phone` (a real HubSpot company property) is
deliberately not mapped — adding it would mean inventing a domain field with no other use.

## Deal

| Internal field (`Deal`) | HubSpot property | Direction | Normalization / transformation | Notes |
|---|---|---|---|---|
| `Name` | `dealname` | Both | Trim on import | |
| `Amount` | `amount` | Both | Invariant-culture decimal formatting/parsing | |
| `Stage` | `dealstage` | Both | Via `IHubSpotStageMapper` (`HubSpotOptions.DealStageMapping`) | See "Deal stage mapping" below |
| `Status` | *(derived, not mapped)* | — | `DealService.DeriveStatus(Stage)` | Never sent to or read from HubSpot directly — always recomputed from `Stage` so the two can't drift apart |
| `CloseDate` | `closedate` | Both | ISO 8601 round-trip (`"O"`) on export; parsed as UTC on import | |
| `Currency` | `deal_currency_code` | Both, **export is opt-in** | `Normalization.NormalizeCurrency` (uppercase) on import | **See "Currency limitation" below — do not enable without checking your portal** |
| `CompanyId`, `ContactId` | *(associations, not properties)* | Both | Resolved through `EntityMapping` | See "Associations" |
| `Owner` | *(not mapped)* | — | — | HubSpot's `hubspot_owner_id` expects a HubSpot owner record id, not a free-text name/email — mapping our free-text `Owner` field into it would either fail validation or silently attach to the wrong owner. Deferred until an owner-lookup step exists. |
| `Id`, `HubSpotId`, `CreatedAt`, `UpdatedAt`, `LastSyncedAt` | — | — | — | Bookkeeping, not synced |

`pipeline` is not mapped: the `Deal` entity has no `Pipeline` field. Deals created by this
platform land in HubSpot's default pipeline; deals imported from a non-default pipeline keep
their `dealstage` (translated if the id is in `HubSpotOptions.DealStageMapping`, otherwise the
stage change is silently not applied — see `DealHubSpotMapper.ApplyHubSpotProperties`).

### Currency limitation (found during live verification)

`deal_currency_code` is **off by default** (`HubSpotOptions.SyncDealCurrencyCode = false`).
During Phase 4 live testing against a real HubSpot developer account, sending
`deal_currency_code: "EUR"` on a portal without multi-currency enabled returned:

```
400 Bad Request — "EUR" is not a part of the current effective currency codes for portal ...
(error: INVALID_OPTION, name: deal_currency_code)
```

Most HubSpot portals are single-currency and reject this property outright. Enable
`HubSpot:SyncDealCurrencyCode` only if your portal has multi-currency turned on and every
currency this platform uses is actually configured there. Reading `deal_currency_code` on import
is always safe and always happens.

### Deal stage mapping

HubSpot pipeline stage ids are portal-specific — a custom pipeline's stages are arbitrary
generated strings, not human-readable names. `HubSpotOptions.DealStageMapping` is an explicit,
overridable dictionary (`DealStage` enum name → HubSpot stage id); the defaults match HubSpot's
out-of-the-box "Sales" pipeline (`qualifiedtobuy`, `presentationscheduled`, `contractsent`,
`closedwon`, `closedlost`). An unrecognized incoming stage id is ignored rather than guessed.

### Lifecycle stage mapping

`HubSpotOptions.LifecycleStageMapping` follows the same pattern. HubSpot's default lifecycle
stage values happen to be the lowercased enum names (`subscriber`, `lead`,
`marketingqualifiedlead`, `salesqualifiedlead`, `opportunity`, `customer`, `evangelist`, `other`),
but the mapping is still an explicit, overridable table rather than a runtime
`ToString().ToLower()` assumption.

## Associations

| Relationship | HubSpot association | Sync behavior |
|---|---|---|
| Contact → Company | `contact_to_company` (v4 default association) | Created only if the Company already has an `EntityMapping`; otherwise skipped (logged as `AssociationSkipped`, not an error) |
| Deal → Company | `deal_to_company` (v4 default association) | Same as above |
| Deal → Contact | `deal_to_contact` (v4 default association) | Same as above |

Associations are never used to guess a relationship that doesn't already exist as an
`EntityMapping` on both sides — see `docs/SYNC_POLICY.md`.

## Duplicate Detection

Deterministic only — no fuzzy or AI matching in this phase.

- **Contact**: normalized `Email` is the sole matching key. An exact single hit is `ExactMatch`;
  more than one existing record with the same normalized email is `Ambiguous` (never
  auto-resolved).
- **Company**: normalized `Domain` is the primary key (`ExactMatch` on exactly one hit,
  `Ambiguous` on more than one). If no domain is available, normalized `Name` is used as a
  fallback — but a name-only hit is **always** classified `Ambiguous`, never `ExactMatch`, even
  when there's exactly one match. Company names are common enough (e.g. "Acme") that treating a
  name match as confident would risk merging unrelated companies.
- **Deal**: no duplicate search at all, in either direction. There is no deterministic key safe
  to dedupe deals by (deal names collide constantly — see the dedicated test
  `SyncToHubSpotAsync_NeverSearchesForDuplicates_EvenWithIdenticalName`). The only protection
  against duplicate deals is the `EntityMapping` itself; see `docs/SYNC_POLICY.md` for the
  resulting limitation around deal creation retried after a local persistence failure.
