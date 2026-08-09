# Tenancy Module

`Puntiro.Modules.Tenancy` owns organization lifecycle, organization membership, and the durable security events emitted by those operations. The module treats Identity user IDs as opaque UUIDv7 values and never reads or writes the `identity` schema.

## Owned PostgreSQL schema

The module owns only schema `tenancy`. Its initial migration is `202608080001_InitialTenancy` and creates:

- `organizations`: UUIDv7 ID, display name, normalized unique slug, lifecycle status, UTC timestamps, and an optimistic-concurrency version;
- `memberships`: UUIDv7 ID, organization ID, opaque external user ID, role, status, UTC timestamps, and an optimistic-concurrency version;
- `security_events`: append-only, redacted organization security events with actor user ID, a bounded trace ID, bounded event type, result, reason code, and UTC occurrence time;
- `__EFMigrationsHistory`: EF migration history scoped to `tenancy`, so this module does not create migration metadata in `public` or another module's schema.

PostgreSQL enforces unique `organizations.slug`, unique `(memberships.organization_id, memberships.user_id)`, and the approved lowercase organization, membership-role, and membership-status values with check constraints. Unknown stored lifecycle values therefore fail closed. Foreign keys remain inside `tenancy`; there is deliberately no database foreign key from a membership to an Identity table.

`TenancyDbContext` rejects modified or deleted tracked security events, and a PostgreSQL trigger rejects direct `UPDATE` or `DELETE` operations against `security_events`. Organization activation and suspension update the organization and append their corresponding redacted event in the same database transaction. Events contain no display name, email, credential material, or other personal data.

## Normalization and lifecycle

Organization slugs are normalized to lower-case ASCII `a-z`, `0-9`, and single hyphens. Surrounding whitespace is removed and runs of whitespace or hyphens collapse to one hyphen. Empty values, non-ASCII characters, punctuation other than hyphens, and values longer than 63 characters fail before persistence.

The supported organization transitions are:

1. `Provisioning` is created by `GetOrCreateProvisioningAsync`.
2. `Provisioning` becomes `Active` only after an active owner membership has been verified.
3. `Active` may become `Suspended`; suspension does not delete or revoke memberships.

Activation is idempotent once the organization is active, but the supplied audit actor must still be an active owner of that organization. A different or revoked user cannot activate an organization or claim an activation event. A suspended organization is not implicitly reactivated. Memberships support the MVP role `Owner` and states `Active` and `Revoked`; `EnsureOwnerMembershipAsync` is idempotent for an existing active owner at the same `(organizationId, userId)`.

An active organization must always retain at least one active owner. `RevokeOwnerMembershipAsync` rejects revocation of its last active owner instead of implicitly changing organization lifecycle. Revocation remains allowed when another active owner exists, or when the organization is still provisioning or already suspended.

## Application contracts

`ITenancyProvisioningService` provides:

- non-mutating normalized-slug lookup returning only an optional organization ID for trusted non-public provisioning/recovery composition;
- create-or-find provisioning organization by normalized slug;
- idempotent owner membership creation;
- owner membership revocation that preserves the active-organization owner invariant;
- owner-gated atomic organization activation and suspension with a required `TenancyAuditContext`;
- a trusted active-owner mutation lease for non-public provisioning/recovery composition.

`TenancyAuditContext` requires a non-empty opaque actor user ID and a 1–128 character trace ID. Trace IDs accept only ASCII letters, digits, `.`, `_`, `:`, and `-`; whitespace, separators, control characters, and unbounded text are rejected before database access. Callers must pass an already-redacted correlation value and must never put an email, credential, activation code, token, or other personal data in it. Activation verifies that this exact actor has an active owner membership in the same transaction before changing state or appending the event. Even an idempotent activation performs the actor check before returning.

Activation, suspension, owner creation, owner revocation, and the trusted mutation lease use a PostgreSQL `ReadCommitted` transaction and acquire the organization row with `FOR UPDATE` before reading or changing memberships. The shared organization-first lock order serializes every state-changing path. The lease reloads the exact organization and owner after locking and remains held across the caller's Identity reset commit; it exposes no identity or authorization data and is not an HTTP authorization contract. Membership lifecycle methods are aggregate-local transitions only; production persistence must invoke the service rather than mutate tracked entities directly because authorization and last-owner rules span organization and membership rows.

`ITenantAccessService` derives tenant access from active memberships joined to active organizations:

- no active membership returns `null`;
- one active membership returns `TenantAccess`;
- more than one active membership throws `OrganizationSelectionRequiredException` instead of choosing a tenant;
- `IsActiveOwnerAsync` verifies both active owner membership and active organization state.

The Cloud host will translate the selection exception to `409 auth.organization_selection_required`. Request body, query, or header organization IDs are not proof of tenant access.

`FindOrganizationIdForTrustedProvisioningAsync` does not create an organization, change lifecycle state, or append an event. It exists only so the private CLI can bind its operator-supplied slug before `ITenantAccessService` verifies active ownership; HTTP handlers must not treat the optional ID as authorization or expose it as organization discovery.

## Configuration and migrations

Runtime composition calls `AddTenancyModule` with the PostgreSQL connection string and may inject a `TimeProvider`. The design-time factory reads only `ConnectionStrings__Puntiro`; there is no checked-in fallback connection string. Production startup does not apply migrations.

Restore the pinned tool and create a migration through the module's design-time factory:

```bash
dotnet tool restore
ConnectionStrings__Puntiro='Host=127.0.0.1;Database=puntiro_design;Username=puntiro_design' \
  dotnet ef migrations add MigrationName \
  --project src/Puntiro.Modules.Tenancy \
  --startup-project apps/cloud \
  --context TenancyDbContext \
  --output-dir Persistence/Migrations
```

Review every generated operation before commit. It must use only schema `tenancy`.

## Failure modes

- Invalid display names, slugs, empty identifiers, null audit context, or unsafe/overlong trace IDs fail with argument exceptions before lookup or an idempotent return.
- Missing organizations or owner memberships fail with `KeyNotFoundException`.
- Activation by a different, revoked, or non-owner actor; activation from a suspended state; reuse of a revoked/non-owner membership; or revocation of the last active owner of an active organization fails closed with `InvalidOperationException`.
- Revoked memberships and inactive organizations never grant tenant access.
- Concurrent writes may produce `DbUpdateConcurrencyException`; callers must not overwrite another accepted change.
- Direct modification or deletion of a stored security event is rejected by PostgreSQL.
- PostgreSQL unique conflicts are reconciled only for the two idempotent create paths. Other provider errors propagate without exposing connection details.
- Missing `ConnectionStrings__Puntiro` fails design-time context creation.
- Missing `PUNTIRO_TEST_POSTGRES` fails integration-test setup; tests never fall back to SQLite, EF in-memory, or mocks.

## Verification

`PUNTIRO_TEST_POSTGRES` must be a maintenance connection whose principal can create and drop databases. The test collection creates a uniquely named database and removes it after the run.

```bash
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj \
  --configuration Release --filter FullyQualifiedName~Tenancy

PUNTIRO_TEST_POSTGRES='Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres' \
  dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj \
  --configuration Release --filter FullyQualifiedName~Tenancy
```
