# Tenancy Module

`Puntiro.Modules.Tenancy` owns organization lifecycle, organization membership, and the durable security events emitted by those operations. The module treats Identity user IDs as opaque UUIDv7 values and never reads or writes the `identity` schema.

## Owned PostgreSQL schema

The module owns only schema `tenancy`. Its initial migration is `202608080001_InitialTenancy` and creates:

- `organizations`: UUIDv7 ID, display name, normalized unique slug, lifecycle status, UTC timestamps, and an optimistic-concurrency version;
- `memberships`: UUIDv7 ID, organization ID, opaque external user ID, role, status, UTC timestamps, and an optimistic-concurrency version;
- `security_events`: append-only, redacted organization security events with bounded event type, result, reason code, and UTC occurrence time;
- `__EFMigrationsHistory`: EF migration history scoped to `tenancy`, so this module does not create migration metadata in `public` or another module's schema.

PostgreSQL enforces unique `organizations.slug` and unique `(memberships.organization_id, memberships.user_id)`. Foreign keys remain inside `tenancy`; there is deliberately no database foreign key from a membership to an Identity table.

`TenancyDbContext` rejects modified or deleted tracked security events. Organization activation updates the organization and appends `organization.activated` in the same database transaction. The event contains no display name, email, credential material, or other personal data.

## Normalization and lifecycle

Organization slugs are normalized to lower-case ASCII `a-z`, `0-9`, and single hyphens. Surrounding whitespace is removed and runs of whitespace or hyphens collapse to one hyphen. Empty values, non-ASCII characters, punctuation other than hyphens, and values longer than 63 characters fail before persistence.

The supported organization transitions are:

1. `Provisioning` is created by `GetOrCreateProvisioningAsync`.
2. `Provisioning` becomes `Active` only after an active owner membership has been verified.
3. `Active` may become `Suspended`; suspension does not delete or revoke memberships.

Activation is idempotent once the organization is active. A suspended organization is not implicitly reactivated. Memberships support the MVP role `Owner` and states `Active` and `Revoked`; `EnsureOwnerMembershipAsync` is idempotent for an existing active owner at the same `(organizationId, userId)`.

## Application contracts

`ITenancyProvisioningService` provides:

- create-or-find provisioning organization by normalized slug;
- idempotent owner membership creation;
- owner-gated atomic organization activation.

`ITenantAccessService` derives tenant access from active memberships joined to active organizations:

- no active membership returns `null`;
- one active membership returns `TenantAccess`;
- more than one active membership throws `OrganizationSelectionRequiredException` instead of choosing a tenant;
- `IsActiveOwnerAsync` verifies both active owner membership and active organization state.

The Cloud host will translate the selection exception to `409 auth.organization_selection_required`. Request body, query, or header organization IDs are not proof of tenant access.

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

- Invalid display names, slugs, or empty identifiers fail with argument exceptions before a write.
- Missing organizations fail with `KeyNotFoundException`.
- Activation without an active owner, activation from a suspended state, or reuse of a revoked/non-owner membership fails closed with `InvalidOperationException`.
- Concurrent writes may produce `DbUpdateConcurrencyException`; callers must not overwrite another accepted change.
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
