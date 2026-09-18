# Security, Authentication & Authorization

Phase 8 adds JWT-based authentication and role-based access control (RBAC) to the internal CRM
Integration Platform. This document describes exactly what is implemented, how it works, and
what it honestly does not cover — see "Known limitations" and "Do not overclaim" at the end.

## Authentication architecture

Standard ASP.NET Core building blocks only — no custom cryptography, no custom JWT signing:

- **Passwords**: `Microsoft.Extensions.Identity.Core`'s `PasswordHasher<TUser>` (PBKDF2-based,
  framework-maintained), wrapped by `IPasswordHasherService`.
- **JWT access tokens**: `System.IdentityModel.Tokens.Jwt` (`JwtSecurityTokenHandler`), issued by
  `IJwtTokenService`, validated by ASP.NET Core's own `Microsoft.AspNetCore.Authentication.JwtBearer`
  middleware.
- **Refresh tokens**: `System.Security.Cryptography.RandomNumberGenerator` for the raw token,
  `SHA256` for the stored hash — both BCL primitives, no custom algorithm.
- **Users/refresh tokens**: PostgreSQL via EF Core (`ApplicationUsers`, `RefreshTokens` tables,
  Phase 8 migration).
- **Authorization**: ASP.NET Core policy-based authorization (`AddAuthorizationBuilder()...AddPolicy(...)`),
  never scattered raw-role-string checks in controllers.

```
CrmIntegration.Api            AuthController, UsersController — thin, [Authorize(Policy=...)] only
CrmIntegration.Application    IAuthenticationService / AuthenticationService     (login orchestration)
                               IJwtTokenService                                   (interface only)
                               IRefreshTokenService / RefreshTokenService         (issue/rotate/revoke)
                               IUserManagementService / UserManagementService     (admin user CRUD)
                               IAdminBootstrapService / AdminBootstrapService     (one-time bootstrap)
                               AuthorizationPolicies                             (centralized policy names)
                               PasswordPolicy                                    (static validation)
CrmIntegration.Infrastructure JwtTokenService, PasswordHasherService            (framework-library adapters)
                               UserRepository, RefreshTokenRepository
```

Each service is small and focused (per the phase's own guidance) rather than one large "AuthService".

## ApplicationUser domain model

```csharp
public class ApplicationUser
{
    public Guid Id { get; set; }
    public string Email { get; set; }         // normalized (trim + lower-invariant), unique index
    public string PasswordHash { get; set; }  // PasswordHasher<TUser> output — never plaintext
    public UserRole Role { get; set; }        // Admin | Operations | Sales | ReadOnly
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
```

No profile fields beyond what RBAC and auditing actually need (no name, avatar, phone, etc.) —
this is an internal application account, not a CRM Contact.

## Password hashing

- `IPasswordHasherService` wraps ASP.NET Core's `IPasswordHasher<ApplicationUser>`
  (`PasswordHasher<TUser>`) — PBKDF2 with a random per-password salt, verified live: hashing the
  same password twice produces two different hashes, and both verify correctly (see
  `PasswordHasherServiceTests`).
- **Password policy** (`PasswordPolicy`): minimum 8 characters, at least one letter and one digit.
  Deliberately simple for a portfolio-scale internal tool — see "Known limitations" for what a
  production system would add.
- Enforced on user creation and password reset; never on login (a login policy violation would
  leak information about which accounts predate the policy).
- `PasswordHash` is never returned by any endpoint — verified in `UserManagementApiTests` by
  asserting the raw response body doesn't contain the word "passwordHash".

## Admin bootstrap

`IAdminBootstrapService.BootstrapAsync()` runs once at startup (`Program.cs`, before `app.Run()`):

1. If `BootstrapAdmin:Email` or `BootstrapAdmin:Password` is unset → do nothing.
2. If any `ApplicationUsers` row already exists → do nothing (never overwrites).
3. If the configured password fails `PasswordPolicy` → log a warning (never the password) and
   skip.
4. Otherwise create one Admin user with that email/password.

Configured via environment/`.env` only (`BootstrapAdmin__Email`, `BootstrapAdmin__Password`) —
`.env.example` ships both blank (bootstrap disabled by default), `appsettings.json` ships both
empty strings. No real value is ever placed in source control. There is no public
self-registration endpoint anywhere in the API.

## Login behavior

`POST /api/auth/login` — `{ email, password }` → `{ accessToken, accessTokenExpiresAt,
refreshToken, refreshTokenExpiresAt, userId, email, role }`.

- Email is normalized before lookup (same `Normalization.NormalizeEmail` used throughout the
  project).
- **Every** failure — unknown email, wrong password, inactive account — returns the identical
  generic `401 "Invalid email or password."` response. Verified explicitly:
  `Login_UnknownEmail_ReturnsUnauthorized_IndistinguishableFromWrongPassword` asserts the two
  response bodies are equal (aside from the per-request correlation id).
  `AuthenticationService.LoginAsync` still calls `VerifyPassword` even when the user doesn't
  exist (against whatever hash is available) so the code path shape doesn't itself leak which
  case occurred — this is a minor mitigation, not a claim of full timing-attack resistance.
- On success: `LastLoginAt`/`UpdatedAt` are updated, a `LoginSucceeded` audit row is written, and
  fresh access + refresh tokens are issued.
- On failure: a `LoginFailed` audit row is written (the attempted normalized email, never the
  password) and a warning is logged.
- The submitted password is never logged anywhere.

## JWT design and claims

`IJwtTokenService.CreateAccessToken` produces a token with:

| Claim | Meaning |
|---|---|
| `sub` | User id (GUID) |
| `email` | Normalized email |
| `role` (`ClaimTypes.Role`) | One of `Admin`/`Operations`/`Sales`/`ReadOnly` |
| `jti` | Unique token id (new GUID per token) |
| `iat` | Issued-at (Unix seconds, UTC) |

Signed with `HmacSha256` using `Jwt:SigningKey` (`SymmetricSecurityKey`). Issuer, audience,
signing key, and access-token lifetime (`Jwt:ExpirationMinutes`, default 60) all come from
configuration (`JwtOptions`) — never hardcoded. All timestamps are UTC (`TimeProvider`, not
`DateTime.Now`).

**Important implementation detail found and fixed during testing**: ASP.NET Core's JWT Bearer
handler silently remaps short claim names (`sub`, `email`) to long legacy `ClaimTypes` URIs when
*validating* an incoming token, by default — even though the token was *issued* with the short
names. Without `options.MapInboundClaims = false` in `Program.cs`, `User.FindFirstValue("sub")`
returns `null` for every authenticated request, breaking `/api/auth/me` and `/api/auth/logout`
with a 500. This was caught by the real-HTTP-pipeline tests (not unit tests of token generation
alone) and fixed — see "Bugs discovered and fixes" in the Phase 8 completion report.

## Access-token validation

`TokenValidationParameters` in `Program.cs` explicitly validates, in this order: signature
(`ValidateIssuerSigningKey`), issuer (`ValidateIssuer`), audience (`ValidateAudience`), and
lifetime (`ValidateLifetime`) — nothing is disabled. `ClockSkew` is reduced from the 5-minute
default to 30 seconds (tight enough to make the "expired token" test in `JwtPipelineTests`
deterministic, generous enough for real clock drift between machines).

Verified through the **real ASP.NET Core authentication pipeline** (not just by calling
`IJwtTokenService` directly) in `JwtPipelineTests`, hand-crafting one token per failure mode:
no token, malformed token, expired token, wrong issuer, wrong audience, wrong signing key,
tampered signature, an unsigned (`alg: none`)-style token, and a fully valid token — all nine
executed as real HTTP requests against a protected endpoint.

## Refresh-token design

```csharp
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; }       // SHA-256 hex of the raw token — unique index
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }    // non-null = permanently unusable
    public Guid? ReplacedByTokenId { get; set; } // rotation chain, for traceability
}
```

- The raw token is 256 bits from `RandomNumberGenerator`, URL-safe base64-encoded. **Only its
  SHA-256 hash is ever persisted** — the raw value is returned to the client exactly once, at
  issuance/rotation, and never logged, audited, or stored anywhere else.
- Lifetime: `Jwt:RefreshTokenExpirationDays` (default 14).
- `POST /api/auth/refresh` — anonymous by design (its whole purpose is to work after the access
  token has expired) — validates, rotates, and returns a new access+refresh token pair.
- `POST /api/auth/logout` — requires a valid access token (`[Authorize]`) *and* the refresh token
  to revoke in the body; the token must belong to the caller (`RefreshTokenService.RevokeAsync`
  checks `existing.UserId == userId`) — verified by
  `Logout_WithAnotherUsersRefreshToken_ReturnsUnauthorized`.

## Refresh rotation / replay protection

**Rotation**: every successful `/api/auth/refresh` call revokes the presented token and issues a
brand-new one (`ReplacedByTokenId` links them). The old token is immediately unusable —
`Refresh_ReplayOfAnAlreadyRotatedToken_ReturnsUnauthorized` proves a second use of the same raw
token after rotation fails.

**A genuine concurrency bug was found and fixed during this phase.** The first implementation
checked `existing.IsActive` (an in-memory boolean check) and only *then* wrote the revocation via
a normal load-mutate-`SaveChangesAsync` — under PostgreSQL's default READ COMMITTED isolation,
two truly simultaneous rotation requests for the same token could **both** pass that check before
either committed, meaning both could succeed, defeating single-use rotation. The fix mirrors the
exact pattern Phase 5 established for `IntegrationEvent` claiming: `IRefreshTokenRepository.TryRevokeAsync`
issues one atomic conditional `UPDATE RefreshTokens SET RevokedAt = @now WHERE Id = @id AND
RevokedAt IS NULL` via EF Core's `ExecuteUpdateAsync`, and only proceeds to mint/persist a new
token if that single UPDATE actually affected a row. This was verified under **real concurrency**,
not simulated: `ConcurrencyTests.ConcurrentRotation_OfTheSameRefreshToken_OnlyOneCallerSucceeds`
fires 8 simultaneous `RotateAsync` calls (each in its own DI scope / database connection) against
the same real Postgres-backed token and asserts exactly 1 succeeds and 7 fail safely.

`RevokeAsync` (logout) uses the identical atomic claim, so a logout racing a refresh (or two
concurrent logouts) is equally safe.

## Logout / revocation semantics

Be precise about what "logout" actually revokes, because it is easy to overclaim here:

- **Refresh-token revocation is real and immediate.** Once `POST /api/auth/logout` (or a
  rotation) revokes a `RefreshToken` row, that exact raw token can never be used again — verified
  against real PostgreSQL.
- **The paired access token is NOT revoked.** JWT access tokens are stateless and are validated
  entirely offline (signature/issuer/audience/expiry) — there is no per-request database check of
  `ApplicationUsers.IsActive` or a token-blocklist. A previously-issued access token remains
  valid, exactly as signed, until its own (short, `Jwt:ExpirationMinutes`) expiry — logout does
  not shorten that. This is the honest, standard tradeoff of stateless JWTs, not a bug: it is
  exactly why access-token lifetimes are kept short (default 60 minutes) while refresh tokens
  carry the real, revocable session. This project does **not** claim instant access-token
  revocation.
- Deactivating a user (`IsActive = false`) has the same characteristic: it blocks all *future*
  logins and refresh-token rotations (`RotateAsync` checks the user is still active before
  rotating) immediately, but does not invalidate that user's already-issued, still-valid access
  tokens before they naturally expire.

## Roles

```csharp
public enum UserRole { Admin, Operations, Sales, ReadOnly }
```

## Authorization policies

Centralized in `AuthorizationPolicies` (constants) and mapped once in `Program.cs` — controllers
reference policy names, never raw role strings:

| Policy | Admin | Operations | Sales | ReadOnly |
|---|:-:|:-:|:-:|:-:|
| `CanReadCrm` | ✅ | ✅ | ✅ | ✅ |
| `CanWriteCrm` | ✅ | ✅ | ✅ | ❌ |
| `CanManageIntegrations` | ✅ | ✅ | ❌ | ❌ |
| `CanManageAutomations` | ✅ | ✅ | ❌ | ❌ |
| `CanViewSalesReports` | ✅ | ✅ | ✅ | ✅ |
| `CanViewOperationalHealth` | ✅ | ✅ | ❌ | ❌ |
| `CanManageUsers` | ✅ | ❌ | ❌ | ❌ |

A `FallbackPolicy` (`RequireAuthenticatedUser()`) is registered so any endpoint added in the
future defaults to **requiring authentication** unless explicitly marked `[AllowAnonymous]` —
secure-by-default rather than relying on every controller remembering to add `[Authorize]`.

## Complete endpoint / RBAC matrix

| Endpoint(s) | Policy | Notes |
|---|---|---|
| `GET /api/contacts`, `/api/companies`, `/api/deals` (+ `/{id}`) | `CanReadCrm` | |
| `POST`/`PUT` on the same | `CanWriteCrm` | |
| `GET /api/deals/{id}/stage-history`, `GET /api/contacts/{id}/lifecycle-history` | `CanReadCrm` | CRM-adjacent read views, not automation admin — see deviation note below |
| `GET /api/onboarding`, `GET /api/onboarding/{id}` | `CanReadCrm` | Same reasoning |
| `POST/GET /api/sync/*` (all sync trigger + job inspection/retry) | `CanManageIntegrations` | |
| `POST /api/webhooks/hubspot` | **none — `[AllowAnonymous]`** | See "HubSpot webhook authentication exception" |
| `GET /api/webhooks/events`, `GET .../events/{id}`, `POST .../events/{id}/retry` | `CanManageIntegrations` | Real internal administration, unlike ingestion |
| `GET/POST /api/automations/executions*` | `CanManageAutomations` | |
| `GET /api/reports/sales/*` (all 9 report endpoints) | `CanViewSalesReports` | |
| `GET /api/reports/operations/health` | `CanViewOperationalHealth` | |
| `GET/POST/PUT /api/users/*` | `CanManageUsers` | Admin only |
| `POST /api/auth/login`, `POST /api/auth/refresh` | **none — `[AllowAnonymous]`** | That's the point of them |
| `POST /api/auth/logout`, `GET /api/auth/me` | `[Authorize]` (any authenticated role) | |
| `GET /health`, `/health/ready`, `/health/live` | **none — `[AllowAnonymous]`** | See below |

**Documented deviations from the phase spec's literal matrix**: the spec's RBAC narrative
mentions "webhook event inspection/retry → Admin, Operations" and separately "onboarding reads
where appropriate" for Sales/ReadOnly and "automation inspection/retry → Admin, Operations" —
this project maps the Deal-stage-history/Contact-lifecycle-history/Onboarding **read** endpoints
(all `GET`) to `CanReadCrm` (available to every role, matching how the spec explicitly wants Sales
and ReadOnly to see onboarding/history data), while `AutomationExecution` inspection/retry
specifically stays `CanManageAutomations` (Admin/Operations only) since that really is automation
administration, not a CRM-adjacent read.

## HubSpot webhook authentication exception

`POST /api/webhooks/hubspot` is explicitly `[AllowAnonymous]` — HubSpot itself calls this
endpoint and cannot present a JWT. Its security is unchanged from Phase 5: HubSpot's own v3
signature validation (`X-HubSpot-Signature-v3` / `X-HubSpot-Request-Timestamp`, see
`docs/WEBHOOKS.md`). This is the **only** endpoint given `[AllowAnonymous]` outside of
login/refresh/health — no other internal endpoint was casually exempted.

Regression-tested explicitly in `WebhookAuthenticationExceptionTests`:
- a validly-signed request with **no** `Authorization` header at all is accepted;
- a validly-signed request with an **unrelated, valid** JWT attached is *also* accepted (proving
  the JWT is simply irrelevant to this endpoint, not merely tolerated when absent);
- a request with **no signature** is rejected 401, even carrying a valid Admin JWT;
- a request with an **invalid signature** is rejected 401, even carrying a valid Admin JWT — a
  real internal token can never substitute for HubSpot's signature;
- by contrast, the inspection endpoints (`GET /api/webhooks/events`) **do** require
  authentication, proving the anonymous exception is scoped to ingestion only.

## Health endpoint security

`/health`, `/health/ready`, `/health/live` remain `[AllowAnonymous]`. Rationale: infrastructure
probes (Docker healthchecks, a future load balancer/orchestrator) generally cannot present a JWT,
and these endpoints return only an aggregate `Healthy`/`Unhealthy` status — no connection strings,
no table names, no stack traces, no version numbers. Verified reachable anonymously in both
`RbacMatrixTests.Anonymous_HealthEndpoints_AreAccessible` and `HealthCheckTests`.

## User management endpoints

All under `[Authorize(Policy = CanManageUsers)]` (Admin only):

```
GET  /api/users
GET  /api/users/{id}
POST /api/users                       # provisioning — NOT public self-registration
PUT  /api/users/{id}/role
PUT  /api/users/{id}/status
POST /api/users/{id}/reset-password
```

- `PasswordHash` is never serialized into any response.
- Email uniqueness is enforced by the same normalized-unique-index pattern used everywhere else
  in this project.
- An invalid `Role` value in a request body is rejected with `400` by ASP.NET Core's own JSON
  enum-binding validation (`JsonStringEnumConverter` throws on an unrecognized name) — no extra
  application code needed, and verified explicitly
  (`Create_InvalidRoleValue_ReturnsBadRequest_ViaFrameworkModelBinding`).

### Last-Admin safety

`IUserManagementService.ChangeRoleAsync`/`SetActiveStatusAsync` refuse to demote or deactivate the
**last active Admin** — implemented with `IUnitOfWork.ExecuteSerializableAsync` (a real
PostgreSQL `SERIALIZABLE` transaction around the count-check-then-write), not just an in-process
check. This matters because "count active Admins, then decide" is the textbook write-skew
anomaly: two concurrent requests can each see "2 active Admins" and both proceed, leaving zero,
under weaker isolation. `SERIALIZABLE` (Postgres's SSI) detects the conflicting read/write
dependency and aborts one transaction, which this project translates to a `409 Conflict`
(`ConcurrencyConflictException`).

Verified three ways:
1. Functional unit tests (`UserManagementServiceTests`) with fakes — demoting/deactivating the
   sole Admin throws; doing so when 2+ Admins exist succeeds.
2. Real-HTTP integration tests (`UserManagementApiTests.LastAdminProtection_*`) against real
   PostgreSQL — temporarily isolates the shared test database down to exactly one active Admin,
   confirms both role-change and deactivation are rejected with `409`, and restores state
   afterward so no other test is affected.
3. **Genuine concurrency** (`ConcurrencyTests.ConcurrentDemotion_OfTheLastTwoActiveAdmins_OnlyOneSucceeds`):
   two real Admins simultaneously try to deactivate each other — under `SERIALIZABLE` isolation
   exactly one succeeds and the other is safely rejected; the database never ends up with zero
   active Admins.

## Swagger / OpenAPI JWT integration

Swagger already had a `Bearer` `SecurityDefinition`/global `SecurityRequirement` configured since
Phase 1 (dormant until real `[Authorize]` attributes existed). Workflow:

1. `POST /api/auth/login` in Swagger UI (or `curl`) with a real user's credentials.
2. Copy the returned `accessToken`.
3. Click **Authorize** in Swagger UI, paste `Bearer <token>` (or just the raw token — Swashbuckle
   prepends the scheme), click **Authorize**.
4. Every subsequent "Try it out" call in Swagger UI now carries that token.

No real token is ever embedded in Swagger's own configuration — the scheme is generic
(`Description = "JWT Authorization header using the Bearer scheme..."`).

## CORS / security configuration

- **CORS**: `Cors:AllowedOrigins` (a string array, empty by default). CORS middleware
  (`app.UseCors(...)`) is only registered **at all** when at least one origin is configured —
  never `AllowAnyOrigin()` combined with `AllowCredentials()` (ASP.NET Core would itself throw at
  runtime if that were attempted, but this project doesn't even construct that configuration).
  Inert today (no frontend exists yet); Phase 10's dashboard origin(s) will populate this list.
- **HTTPS/HSTS**: `app.UseHttpsRedirection()` (already present since Phase 1) is unchanged.
  `app.UseHsts()` now runs when `!Environment.IsDevelopment()` — standard ASP.NET Core template
  behavior. docker-compose's local dev setup runs with `ASPNETCORE_ENVIRONMENT=Development`, so
  this never triggers locally. This is **not** a claim of a hardened production TLS setup — a
  real deployment still needs a reverse proxy/load balancer terminating TLS in front of this app.
- **Error responses**: the existing global exception handler (`Program.cs`) gained mappings for
  the four new exception types (`InvalidCredentialsException`/`InvalidRefreshTokenException` →
  401, `DuplicateEmailException`/`ConcurrencyConflictException` → 409) — no stack traces, no
  signing-key or password-hash content, ever, in any error response (unchanged policy from prior
  phases, `docs/SYNC_POLICY.md`'s error table now extended).

## Secret handling

| Secret | Where it lives |
|---|---|
| HubSpot AccessToken/ClientSecret/WebhookSigningSecret | `.env` (gitignored), never in `appsettings.json` |
| JWT signing key (`Jwt:SigningKey`) | `.env` (gitignored); `appsettings.json` ships an empty string |
| Bootstrap Admin password | `.env` (gitignored) only; never in `appsettings.json`, README, or any test/doc |
| Refresh tokens | Only their SHA-256 hash is ever persisted; the raw value is never logged or stored |

`.env.example` was updated with the two new `BootstrapAdmin__*` keys and a `Jwt__RefreshTokenExpirationDays`
line, both placeholder/blank — verified by direct inspection (no realistic-looking secret value
that could trip GitHub Push Protection). See the Phase 8 completion report's "Secret/security
scan" section for the actual scan executed against every new/modified file.

## Local development setup

```
BootstrapAdmin__Email=admin@example.test        # set in .env to get an initial Admin on first run
BootstrapAdmin__Password=SomeStrongPassword1    # must satisfy PasswordPolicy (8+ chars, letter+digit)
```
Start the API once with those set; the bootstrap Admin is created automatically. Leave them blank
on subsequent runs (harmless either way — bootstrap is a no-op once any user exists).

## Production recommendations (not implemented here — portfolio scope)

- A managed secrets store (Azure Key Vault, AWS Secrets Manager, etc.) instead of `.env` files.
- A dedicated reverse proxy/load balancer terminating TLS.
- Shorter access-token lifetimes plus a real-time revocation/blocklist mechanism if instant
  access-token revocation becomes a hard requirement.
- Rate limiting on `/api/auth/login` (not implemented — brute-force throttling is out of this
  phase's scope).
- A stronger password policy (breach-list checking, configurable complexity) and optional MFA.

## Known limitations

- Access tokens cannot be revoked before their natural expiry (see "Logout / revocation
  semantics" above) — by design of stateless JWTs, not an oversight.
- No rate limiting / brute-force protection on login.
- Password policy is minimal (length + letter + digit) — adequate for a portfolio-scale internal
  tool, not a production consumer-facing policy.
- No MFA, no SSO, no external identity provider — intentionally out of Phase 8's scope (see below).
- `RbacMatrixTests`/`UserManagementApiTests` share one persistent Docker Postgres database across
  the whole test suite; tests that need an isolated "zero other admins" state achieve it by
  temporarily deactivating other admins and restoring them in a `finally` block, rather than
  wiping the database — documented in the tests themselves.

## Do not overclaim

This is a portfolio implementation. It does **not** claim: full enterprise IAM, zero-trust
architecture, GDPR/SOC2/compliance certification, instant JWT revocation, penetration-tested
security, or production hardening beyond what's explicitly listed above. What's actually
implemented is: real password hashing (framework primitive), real signed-and-validated JWTs, real
role-based policy enforcement across every endpoint from Phases 1–7, a genuinely
concurrency-safe refresh-token rotation and last-Admin invariant (both verified under real
concurrent load against real PostgreSQL), and an honest audit trail of security-relevant events.

## Scope boundary

Not implemented (explicitly out of Phase 8's scope): Azure AD/Entra ID, Auth0, Okta, social
login, MFA, SSO, a full OAuth/OIDC identity-provider server, Phase 9 observability
(OpenTelemetry/Prometheus/Grafana), Phase 10's React dashboard, Phase 11's broader hardening, and
Phase 12's final polish.
