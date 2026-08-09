# Cloud Development and Deployment Operations

This runbook covers a local PostgreSQL 17.10 environment and the reviewed operational sequence for Puntiro Cloud. It does not deploy to Timeweb. Production deployment, restore drill, and external-system acceptance remain separate manual gates.

## Local secret preparation

Use a private terminal. Copy `infra/compose/.env.cloud.example` to the ignored `infra/compose/.env.cloud`, set file mode `0600`, and populate it locally. Values must not be committed, printed into CI output, pasted into tickets, or stored in shell history. Each session, recovery and integration HMAC key is an independently generated base64 encoding of exactly 32 random bytes. Never reuse one value across purposes.

Create a private Data Protection directory and PKCS#12 certificate outside the tracked tree or under the ignored local paths:

```bash
mkdir -p infra/compose/cloud-data-protection-keys infra/compose/cloud-secrets
chmod 700 infra/compose/cloud-data-protection-keys infra/compose/cloud-secrets
chmod 600 infra/compose/.env.cloud
openssl req -x509 -newkey rsa:3072 -sha256 -nodes \
  -keyout infra/compose/cloud-secrets/data-protection.key \
  -out infra/compose/cloud-secrets/data-protection.crt \
  -days 30 -subj '/CN=Puntiro local Data Protection'
openssl pkcs12 -export \
  -out infra/compose/cloud-secrets/data-protection.pfx \
  -inkey infra/compose/cloud-secrets/data-protection.key \
  -in infra/compose/cloud-secrets/data-protection.crt
chmod 600 infra/compose/cloud-secrets/*
```

The PKCS#12 command prompts privately for its password. Store that password and every HMAC value only in the ignored local env file or an approved secret source. The example file intentionally contains blank assignments only.

## Start PostgreSQL and load configuration

```bash
docker compose --env-file infra/compose/.env.cloud -f infra/compose/cloud-development.yml up -d
docker compose --env-file infra/compose/.env.cloud -f infra/compose/cloud-development.yml ps
set -a
. infra/compose/.env.cloud
set +a
dotnet tool restore
```

The compose service pins `postgres:17.10-bookworm`, persists data in the named `puntiro-cloud-postgres` volume, publishes PostgreSQL only on `127.0.0.1`, and reports healthy through `pg_isready`. `PUNTIRO_TEST_POSTGRES` is a maintenance connection whose local principal may create and drop disposable test databases. Never echo it.

The same file contains an opt-in `cloud-runtime` profile for validating a separately reviewed Cloud
image. It is not used by the normal database-only command and this repository does not claim that a
Timeweb image has been built or deployed. The profile is read-only, drops capabilities, waits for
PostgreSQL health, persists the Data Protection ring in its own named volume, mounts the PKCS#12
file read-only, and injects versioned HMAC values through the ignored env/approved secret source.
Apply all reviewed migrations before starting that profile; it is not a migration runner.

## Apply reviewed migrations

Cloud and the provisioning CLI never migrate at startup. After reviewing the generated SQL and confirming the intended backup, apply each module migration explicitly:

```bash
dotnet ef database update --project src/Puntiro.Modules.Tenancy --startup-project apps/cloud --context TenancyDbContext
dotnet ef database update --project src/Puntiro.Modules.Identity --startup-project apps/cloud --context IdentityDbContext
dotnet ef database update --project src/Puntiro.Modules.Integrations --startup-project apps/cloud --context IntegrationsDbContext
```

Start the host only after all three commands succeed. `/health/live` proves only that the process responds. `/health/ready` additionally requires PostgreSQL connectivity, no pending module migrations, a usable persistent Data Protection ring, current HMAC keys, and every retained key version referenced by durable credentials.

Probe the two boundaries separately through the same address the operator intends to admit to traffic:

```bash
PUNTIRO_CLOUD_HEALTH_ORIGIN=https://replace-with-reviewed-operator-origin.invalid
curl --fail --silent --show-error "$PUNTIRO_CLOUD_HEALTH_ORIGIN/health/live" >/dev/null
curl --fail --silent --show-error "$PUNTIRO_CLOUD_HEALTH_ORIGIN/health/ready" >/dev/null
```

The placeholder origin is intentionally unusable. Replace it only in the private operator shell; do not commit a deployment hostname. A liveness success never compensates for a readiness failure.

For the first organization, use the private workflow after readiness prerequisites are in place:

```bash
dotnet run --project tools/Puntiro.Provisioning -- bootstrap-owner --organization-name Puntiro --organization-slug puntiro --email owner@example.test
```

Follow [First owner provisioning](first-owner-provisioning.md); do not redirect or record its one-time output.

## Trusted Timeweb proxy boundary

Before enabling forwarded headers, obtain the exact immediate reverse-proxy IP addresses or CIDR networks from the deployed Timeweb topology. Set `Puntiro__Proxy__Enabled=true` and populate at least one indexed `KnownProxies` or `KnownNetworks` entry. Puntiro accepts only one symmetric `X-Forwarded-For`/`X-Forwarded-Proto` hop. Unknown direct peers cannot change the client IP or scheme with spoofed headers.

Do not set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`; that platform shortcut removes the explicit trust boundary and Puntiro rejects it. Restrict direct network access to the Cloud listener so only the expected proxy and operator path can reach it. Revalidate the allowlist after Timeweb topology changes. Automated tests exercise trusted and unknown peers, but the deployed Timeweb boundary remains `not run` until verified there.

## Deployment order and rollback

1. Back up PostgreSQL and separately snapshot the Data Protection ring, its protection certificate/password custody, and every retained HMAC key version.
2. Restore those artifacts in an isolated environment and verify permissions/readability before calling the backup accepted.
3. Apply reviewed expand-compatible migrations with an explicit deployment command.
4. Start the new application version with the existing and new key versions mounted.
5. Require `/health/live`, then `/health/ready`, then a bounded authentication smoke test whose bodies and credentials are not logged.
6. Enable traffic only after readiness succeeds.

Rollback means returning to the previously reviewed application image while retaining forward-compatible schema and key material. Use expand/contract migrations across releases. Never run an automatic EF down-migration against production data. A destructive schema reversal requires a separately reviewed restore/migration plan.

## Backup, recovery and key rotation

- Create PostgreSQL dumps through an approved encrypted backup process. Verify restore into an isolated PostgreSQL 17.10 instance and run all three migration/readiness checks.
- Back up the persistent Data Protection directory independently from PostgreSQL. Keep the certificate and its password in separate approved custody. Restoring the database without the matching ring and certificate is an incident because stored TOTP payloads become unreadable.
- Snapshot every retained HMAC version independently. Restore readiness must cover unused recovery rows, stored sessions and unrevoked integration tokens before traffic is enabled.
- To rotate an HMAC purpose, add a new independently generated version while retaining the old version, deploy, set `CurrentVersion` to the new version, and verify readiness. Remove an old session/recovery/integration version only after no durable verifier references it; integration credentials normally require explicit replacement and revoke.
- Data Protection certificate rotation is not an automatic operator workflow in this stage. Keep recoverable custody of the active certificate and ring; introduce and test multi-certificate unprotect support before changing the protection certificate.
- Lost ring, certificate, password, or required HMAC version is an incident. Do not delete credential rows, silently reset factors, or retry unknown operations as a substitute for recovery.

For a local drill, first load the ignored env as shown above, choose a private absolute backup directory, and keep the restrictive umask. The following writes a custom-format dump without printing the connection string or password:

```bash
umask 077
PUNTIRO_BACKUP_DIR=/replace/with/a/private/puntiro-backup-directory
install -d -m 700 "$PUNTIRO_BACKUP_DIR"
docker compose --env-file infra/compose/.env.cloud -f infra/compose/cloud-development.yml \
  exec -T postgres pg_dump --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --format=custom --no-owner --no-privileges > "$PUNTIRO_BACKUP_DIR/postgres.dump"
test -s "$PUNTIRO_BACKUP_DIR/postgres.dump"
```

Copy the Data Protection ring only after the `cloud` service has created it, and preserve the separately protected certificate without displaying either artifact:

```bash
docker compose --env-file infra/compose/.env.cloud -f infra/compose/cloud-development.yml \
  --profile cloud-runtime cp \
  cloud:/var/lib/puntiro/data-protection-keys "$PUNTIRO_BACKUP_DIR/data-protection-keys"
cp -p infra/compose/cloud-secrets/data-protection.pfx "$PUNTIRO_BACKUP_DIR/data-protection.pfx"
chmod -R go-rwx "$PUNTIRO_BACKUP_DIR"
```

Retained HMAC versions and the certificate password stay in the approved secret store; they are not exported by these commands. Record their version identifiers and custody references, never values, next to the backup record.

Restore only into a new isolated Compose project and a deliberately different loopback port. Populate the ignored env with the backup's retained key versions and certificate before creating the Cloud container:

```bash
PUNTIRO_RESTORE_PROJECT=puntiro-restore-drill
PUNTIRO_POSTGRES_PORT=55440 docker compose -p "$PUNTIRO_RESTORE_PROJECT" \
  --env-file infra/compose/.env.cloud -f infra/compose/cloud-development.yml up -d postgres
PUNTIRO_POSTGRES_PORT=55440 docker compose -p "$PUNTIRO_RESTORE_PROJECT" \
  --env-file infra/compose/.env.cloud -f infra/compose/cloud-development.yml \
  exec -T postgres pg_restore --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --exit-on-error --no-owner --no-privileges < "$PUNTIRO_BACKUP_DIR/postgres.dump"
PUNTIRO_POSTGRES_PORT=55440 docker compose -p "$PUNTIRO_RESTORE_PROJECT" \
  --env-file infra/compose/.env.cloud -f infra/compose/cloud-development.yml \
  --profile cloud-runtime create cloud
PUNTIRO_POSTGRES_PORT=55440 docker compose -p "$PUNTIRO_RESTORE_PROJECT" \
  --env-file infra/compose/.env.cloud -f infra/compose/cloud-development.yml \
  --profile cloud-runtime cp \
  "$PUNTIRO_BACKUP_DIR/data-protection-keys/." cloud:/var/lib/puntiro/data-protection-keys
```

Before starting the isolated Cloud container, set its private `ConnectionStrings__Puntiro` to that restore project rather than the normal database. Start it with `--profile cloud-runtime`, require both health probes, and execute a bounded authentication/readiness acceptance that records no bodies. Do not point this procedure at production, reuse a production Compose project name, or call a drill successful until PostgreSQL, ring, certificate and every retained HMAC version have all been exercised. Teardown of the isolated project is an explicit operator cleanup after evidence has been retained; it is not included here to avoid an accidental volume deletion.

Record every restore and rotation drill separately. A passing local or CI suite is not proof of Timeweb storage, backup retention, filesystem permissions, certificate custody, or external-system behavior.
