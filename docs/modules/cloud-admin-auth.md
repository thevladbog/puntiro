# Cloud Admin Authentication Boundary

`Puntiro.Cloud` composes the Identity, Tenancy, and Integrations modules without taking ownership of their domain rules or tables. Production startup validates configuration and builds the host, but never applies or creates a database migration.

## HTTP contract

The first Admin boundary exposes:

```text
POST /api/admin/auth/login
POST /api/admin/auth/logout
GET  /api/admin/auth/session
POST /api/admin/auth/step-up
GET  /api/admin/integration-tokens
POST /api/admin/integration-tokens
POST /api/admin/integration-tokens/{id}/revoke
GET  /health/live
GET  /health/ready
GET  /openapi/v1.json
```

Login accepts email, password, and exactly one TOTP or recovery factor. Credential failures are externally indistinguishable. One active organization membership is selected from Tenancy; no organization ID from the body, query, or headers can establish tenant context. Multiple active memberships fail with `409 auth.organization_selection_required` until an explicit selection use case exists.

The server session is carried only by `__Host-puntiro_session`. It is `Secure`, `HttpOnly`, `SameSite=Strict`, has `Path=/`, and has no `Domain`. Its value is never returned as JSON or written to application logs. Recovery login leaves `secondFactorVerifiedAt` empty. A successful replay-protected TOTP step-up is committed to the current, still-active server session in the same Identity security boundary; revoke, reset, suspension, and expiry win safely when concurrent.

`GET /session` issues an antiforgery token. Login and every other unsafe Admin request require one exact configured HTTPS `Origin`; login is exempt only from the antiforgery token and authenticated-session requirements. Logout, step-up, and future state-changing Admin routes additionally require the token in `X-Puntiro-CSRF`. Content type, method-override headers, SameSite behavior, or request-supplied tenant identifiers do not bypass this policy. OpenAPI marks that header as required on every authenticated unsafe Admin operation.

Integration-token list/create/revoke revalidate the active owner and organization through the same
server-derived tenant context. Create requires a TOTP-backed `secondFactorVerifiedAt` within the
inclusive five-minute window; recovery login alone is insufficient. The raw integration credential
is serialized once by its owning response result and disposed immediately after that response
boundary. List and revoke expose metadata only.

## Limits and errors

Auth JSON and aggregate request headers are bounded before credential work. Unknown fields and malformed JSON are rejected. Login is limited simultaneously by the direct connection IP and a process-random keyed hash of the normalized email partition; raw email is not a limiter key. Forwarded client-IP headers are not trusted by this host stage. Step-up is limited per verified server session. Partitions have a fixed upper bound, and `429` includes deterministic `Retry-After`.

Errors use `application/problem+json` with a stable `code` and opaque `traceId`. This includes bounded route-not-found and method-not-allowed fallbacks; routing headers such as `Allow` are preserved. Public responses never contain stack traces, provider messages, hashes, credentials, or foreign-tenant identifiers. Request-body logging is not enabled for authentication routes.

## Startup and readiness

Deployment supplies the PostgreSQL connection, persistent Data Protection directory and certificate, plus independent versioned session, recovery, and integration HMAC keys. Startup fails closed when required values are absent, malformed, too short, or reused across purposes. `appsettings.json` contains only non-secret limits.

`/health/ready` performs read-only checks: PostgreSQL connectivity, no pending migration in all three module schemas, durable Data Protection round trip, current HMAC usability, every historical HMAC version referenced by an unrevoked integration token, retained Identity key versions, and decryptability of active-account TOTP payloads. It never calls `Migrate`, `MigrateAsync`, or `EnsureCreated`.

Production Npgsql retries wrap each complete Identity login, TOTP step-up and server-session transaction. Every retry starts from a cleared tracker and a fresh entity/event attempt; commit verification uses a stable bounded audit identifier so a transient replay cannot duplicate an authentication event or session.

Production Npgsql retries also wrap complete Integration create, authenticate/usage, and revoke
transactions. Stable identifiers and durable commit checks prevent duplicate tokens or audit events;
usage replay remains within the 15-minute coalescing rule. The dedicated external Bearer scheme
rechecks token revocation and active organization state per request, applies exact scope policies,
and never falls back to the Admin cookie or Admin antiforgery boundary.

For a real PostgreSQL 17.10 test database whose principal may create and drop disposable databases:

```bash
PUNTIRO_TEST_POSTGRES='<approved maintenance connection>' \
  dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj \
  --configuration Release --filter FullyQualifiedName~Cloud
```

Automated tests prove HTTP, persistence, and OpenAPI behavior only. Timeweb deployment and production certificate/key restore remain `not run` until exercised in that environment.
