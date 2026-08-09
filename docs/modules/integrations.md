# Integrations module

`Puntiro.Modules.Integrations` owns long-lived credentials used by simple external systems that can send one fixed HTTP request with an `Authorization` header. It does not implement OAuth exchanges, refresh tokens, automatic rotation, or shipment endpoints. The Cloud host authenticates an active owner before list/create/revoke, requires a current five-minute TOTP step-up for create, and rechecks the token organization's active state before an integration use case runs.

The module references only `Puntiro.Security`. It does not reference Identity or Tenancy and never reads their schemas. Organization and user IDs are opaque ownership/audit identifiers; there are deliberately no cross-schema foreign keys.

## Bearer format and verifier

Create returns one caller-owned `SensitiveValue` in this canonical format:

```text
pnt_live_<22 character Base64Url public ID>.<43 character Base64Url secret>
```

The public ID and secret come from independent CSPRNG draws: 128 public bits and 256 secret bits. Encoding is unpadded canonical Base64Url. The parser requires the exact prefix, separators, lengths, alphabet and canonical padding bits before performing a database query.

PostgreSQL never receives the raw token or secret. It stores the public ID, a bounded key version and:

```text
HMAC-SHA-256(
  integration-key[version],
  ASCII("Puntiro.Integrations.Token.v1" + NUL + public-id) || secret
)
```

Verification selects only the version recorded on the row and compares the 32-byte candidate with `CryptographicOperations.FixedTimeEquals`. A malformed token, unknown public ID, wrong secret, unavailable/unknown key version, invalid persisted scope, future creation/use timestamp, or revoked row returns the same `null` result. Provider/configuration failures are infrastructure errors and are not translated into credential detail.

`IssuedIntegrationToken` must be disposed after its one-time response boundary. Its raw value is ignored by normal JSON serialization and debugger browsing, and token/key/entity string representations do not reveal credential material. Callers must never put `Reveal()` output, HMAC keys, or verifier bytes in logs, traces, exceptions, command arguments, environment-variable values written to output, or durable events.

## Scopes and tenant derivation

The closed MVP scope set is:

- `shipments.read` (`IntegrationScope.ShipmentsRead`);
- `shipments.write` (`IntegrationScope.ShipmentsWrite`).

Create requires one or both values. Empty sets and enum values outside this set fail before token generation or persistence. PostgreSQL repeats the allowlist as a check constraint on normalized scope rows.

An authenticated `IntegrationPrincipal` receives token ID, organization ID and scopes only from the verified durable row. A body, query parameter or custom organization header is never tenant authorization. The Cloud bearer handler remains responsible for rechecking that organization is active and applying the exact required scope.

## Owned PostgreSQL schema

Migration `202608080003_InitialIntegrations` owns only schema `integrations` and creates:

- `integration_tokens`: public metadata, versioned HMAC verifier, organization/creator ownership, active slot, timestamps, revoke metadata and optimistic `version`;
- `integration_token_scopes`: normalized closed scope rows with an in-schema token foreign key;
- `security_events`: append-only, bounded create/revoke audit rows;
- `__EFMigrationsHistory`: migration history scoped to `integrations`, never `public`.

The schema enforces a unique public ID, 32-byte verifier length, positive version, valid revoke metadata/timestamps, and a unique `(organization_id, active_slot)` for active rows. Slots are limited to `1` and `2`; revocation clears the slot. This database guard means a direct or racing write cannot create a third active credential even if application counting is bypassed.

Security events cannot be updated or deleted through EF. A PostgreSQL trigger independently rejects direct `UPDATE` and `DELETE`. Events contain organization, actor, token ID, the bounded W3C activity trace ID when available (otherwise a generated safe fallback), fixed event/result/reason values and UTC time only. They never contain display name, raw bearer material, verifier, HMAC key, request body or arbitrary caller text. Create/revoke state and their event commit in the same transaction.

## Lifecycle and concurrency

### Create

Display names are trimmed and must contain 1–100 valid Unicode scalar values. Identifiers, display name and scopes are validated before secret generation.

Creation runs in a PostgreSQL `Serializable` transaction, counts active rows for the organization, selects an available active slot, inserts the token/scopes and appends the audit event. Serialization/deadlock failures and collisions on the two owned uniqueness guards retry at most four complete attempts with a fresh secret. After the bound, callers receive the generic `IntegrationTokenCreationConflictException`. A count of two produces `ActiveTokenLimitException` without generating a token.

When the Cloud production Npgsql retry strategy is enabled, every create attempt is a complete
replayable unit. Its token and audit identifiers, creation time, public ID, verifier, and one-time raw
material remain stable across provider replay of that attempt. Before retrying a transaction, the
service checks the stable token/event pair; an ambiguous successful commit returns the original
one-time material instead of inserting a second durable row or generating a second credential.
Bounded serializable/unique-slot conflict attempts may still generate fresh material after a
confirmed rollback.

The service owns the transaction, raw token and verifier until both commit and explicit transaction disposal succeed. It never uses an implicit `await using` return path for creation and never transfers the raw credential before disposal completes. A failed body always records its original exception, attempts transaction disposal, clears verifier bytes and attempt-owned tracked state, then either classifies that original exception for bounded retry or rethrows it with its preserved stack. A disposal error never causes a retry.

If both the body and disposal fail, `IntegrationTokenAttemptCleanupException` contains the original body failure first and the disposal failure second; the original is also `InnerException`. If commit was confirmed but disposal then fails, the durable token may already exist without any recoverable raw credential. The service clears all credential material and throws `IntegrationTokenCommittedWithoutCredentialException` with safe token/organization IDs. Operators must locate and revoke that token; callers must not retry automatically. Transaction disposal failure is never described as a successful rollback. Cleanup is idempotent, best effort and cannot replace the recorded primary failure. List projects only metadata and scopes; it never loads or returns the verifier or raw value.

### Authenticate

Authentication parses the bounded credential before lookup, locks the located token row, reloads its scopes/state, verifies the HMAC and checks revocation while holding the row lock. Successful authentication writes `last_used_at` immediately when absent, then at most once per 15 minutes. The write increments optimistic `version`; the absolute source of truth remains the database row. Tokens have no automatic expiry in this MVP and remain valid until manual revoke, but a row whose creation or last-use timestamp is in the future fails closed.

Authentication, including the coalesced usage write, runs as one execution-strategy transaction.
After an ambiguous successful commit, replay observes the already-updated timestamp and does not
advance the version twice.

### Revoke

Revoke locks and selects by both organization and token ID. A missing token and a token owned by another organization produce the same `KeyNotFoundException`. An active token requires the exact expected version and otherwise throws `DbUpdateConcurrencyException`. The first accepted revoke writes UTC revoke metadata, clears its active slot and appends one event. Repeating revoke for that same organization/token is idempotent regardless of the old expected version and creates no duplicate event.

Revoke uses one stable audit identifier inside a complete execution-strategy transaction. A replay
after an ambiguous commit observes the revoked row and returns idempotently; the append-only event
remains singular.

## Cloud HTTP boundary

Active owners manage safe metadata at `GET /api/admin/integration-tokens`, issue a token at
`POST /api/admin/integration-tokens`, and revoke at
`POST /api/admin/integration-tokens/{id}/revoke`. Unsafe Admin calls require the exact configured
Origin and `X-Puntiro-CSRF`; create additionally requires a TOTP-backed session freshness timestamp
not older than five minutes. All three routes share a fixed 120-per-minute accepted-client-IP Admin
limit and return `429 auth.rate_limited` with `Retry-After` when it is exhausted. The raw bearer
appears only in a successful `201` create response.

Integration use cases use the dedicated `IntegrationToken` Bearer scheme and the closed policies
`integration.shipments.read` and `integration.shipments.write`. Exactly one bounded canonical
`Authorization: Bearer ...` value is accepted. The Admin cookie is never a fallback for `/api/v1`.
The organization and scopes come only from the verified durable token, and active organization plus
revocation state are checked for every request. Successful scope authorization establishes the
request-scoped tenant context from that token ID and organization, never from request input. A fixed
one-minute pre-authentication gate permits 120 total integration attempts per accepted client and is
reserved atomically before credential parsing or database authentication. Once exhausted, later
requests return `429` without calling the token service or PostgreSQL. Malformed and unknown values
consume only this gate. A successfully verified token additionally consumes a separate 120-request
public-ID/accepted-client-IP partition. This deliberate double gate bounds database work, but it also
means clients sharing one NAT peer share the aggregate 120-request pre-authentication budget even
when they use different valid tokens. Forwarded IP headers affect these partitions only when Cloud
enables its single-hop boundary with an explicit immediate proxy/network allowlist. Unknown peers
cannot change the client address.

OpenAPI declares the manually parsed create/revoke JSON bodies, exact metadata/issued success
schemas, no-content revoke, and every stable Admin or integration problem code. Raw credential
examples and defaults are forbidden. Integration policy probes are contributed only by the
integration-test assembly; the base Cloud Program exposes none in Testing or Production.

## Runtime and migrations

Runtime composition calls `AddIntegrationsModule` with a PostgreSQL connection string, validated `IntegrationKeyOptions` and optional `TimeProvider`. Each integration HMAC key is exactly 32 bytes, versions are bounded ASCII identifiers, the current version must be present, and new rows always use it. Retain every historical version referenced by a stored row until those credentials are revoked and no longer need authentication. Integration keys must be generated independently from Identity session/recovery, Data Protection and rate-limit keys; the deployment composition must reject cross-purpose reuse.

`IIntegrationTokenReadinessService` performs a read-only distinct-version query over every unrevoked token and fails closed when any referenced HMAC version is absent. It returns only a generic readiness boolean and never token identifiers, verifier material, scopes or organization data.

The design-time factory reads only `ConnectionStrings__Puntiro`; it has no fallback credential. Production startup does not apply migrations. Restore the pinned tool and create/review migrations with an explicitly supplied development connection:

```bash
dotnet tool restore
ConnectionStrings__Puntiro='<approved development connection>' \
  dotnet ef migrations add MigrationName \
  --project src/Puntiro.Modules.Integrations \
  --startup-project apps/cloud \
  --context IntegrationsDbContext \
  --output-dir Persistence/Migrations
```

Review every operation before commit. An Integrations migration may touch only schema `integrations`; deployment applies reviewed migration artifacts before the Cloud process starts.

## Failure and operational boundaries

- The external system may keep one fixed bearer header; refresh-token and automated-rotation assumptions are forbidden.
- Manual rotation is: create the second token, update and verify the external system, then revoke the first. A third active token is rejected.
- A lost raw value cannot be recovered. Create a replacement within the two-token window and revoke the lost credential.
- Authentication returns no reason that distinguishes malformed, missing, retired-key, wrong-secret or revoked credentials.
- Missing `ConnectionStrings__Puntiro` fails design-time context creation.
- Missing `PUNTIRO_TEST_POSTGRES` fails integration-test setup; tests never fall back to SQLite, EF in-memory or mocks.
- Active owner/organization authorization, five-minute TOTP step-up, HTTP rate limiting and `application/problem+json` mapping belong to Cloud composition and are not implemented by this module.

## Verification

Use the exact repository SDK and a maintenance connection whose principal may create/drop isolated test databases:

```bash
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj \
  --configuration Release --filter FullyQualifiedName~Integrations

PUNTIRO_TEST_POSTGRES='<approved PostgreSQL 17.10 maintenance connection>' \
  dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj \
  --configuration Release --filter FullyQualifiedName~Integrations

dotnet ef migrations has-pending-model-changes \
  --project src/Puntiro.Modules.Integrations \
  --startup-project apps/cloud \
  --context IntegrationsDbContext
```

The PostgreSQL suite proves migration ownership/parity, constraints, append-only audit, HMAC-only persistence, exact two-token concurrency, tenant-concealed revoke, optimistic versioning, immediate revoke, scope derivation and 15-minute `last_used_at` coalescing. It does not claim production deployment, external-system acceptance, Windows, kiosk, printer or physical-operator validation.
