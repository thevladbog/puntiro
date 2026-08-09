# Cloud Development and Deployment Operations

This runbook covers the reviewed local PostgreSQL 17.10 and Cloud operator sequence. It does not deploy to Timeweb. Production deployment, Timeweb proxy verification, backup retention, and restore/rotation drills remain manual gates until an operator records evidence from those environments.

## Private files and path model

Copy both templates and restrict them before adding values:

```bash
cp infra/compose/.env.cloud.example infra/compose/.env.cloud
cp infra/compose/cloud-runtime.env.example infra/compose/cloud-runtime.env
chmod 600 infra/compose/.env.cloud infra/compose/cloud-runtime.env
```

`infra/compose/.env.cloud` is the Compose/host-tool overlay. `infra/compose/cloud-runtime.env` is the raw Cloud runtime file. They are ordinary dotenv files for Compose; they are not shell programs and must never be sourced. Values use exact `NAME=VALUE` assignments. Duplicate names inside a file, `export`, quotes, inline comments, every dollar sign, backticks, tabs/control characters and leading/trailing value whitespace are rejected. Full-line comments and blanks are allowed. Semicolons, internal spaces and base64 padding are preserved literally by the checked wrapper and by `scripts/run-with-cloud-env.mjs` with `shell: false`. Passing `.env.cloud` last intentionally overrides the container connection only for host tools.

The runtime file contains every retained HMAC version, not only `CurrentVersion`. Generate independent canonical base64 encodings of exactly 32 random bytes for session, recovery, and integration purposes. Never reuse key bytes across purposes. Keep populated files ignored, untracked, mode `0600`, and in approved secret custody; never print them or paste them into CI, tickets, or shell history.

Create one private host Data Protection ring and certificate. Host provisioning reads these exact absolute paths from `cloud-runtime.env`; Compose bind-mounts the same ring into Cloud and overrides only the in-container paths. There is no second named key-ring volume.

```bash
mkdir -p infra/compose/cloud-data-protection-keys infra/compose/cloud-secrets
chmod 700 infra/compose/cloud-data-protection-keys infra/compose/cloud-secrets
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

The PKCS#12 command prompts privately. Put its password only in the runtime file. Set `Puntiro__Security__DataProtectionKeysPath` and `Puntiro__Security__DataProtectionCertificatePath` there to the absolute host paths. Set `Puntiro__Security__DataProtectionCertificateHostPath` in `.env.cloud` to that same certificate path. Set `PUNTIRO_CLOUD_UID` and `PUNTIRO_CLOUD_GID` to the non-zero numeric owner of the ring, every ring key file and certificate; Compose forces the container to that same identity. For local development these normally equal the account that runs provisioning. For Timeweb use the reviewed dedicated service identity and change ownership explicitly before preflight. The Cloud container receives `/var/lib/puntiro/data-protection-keys` and `/run/puntiro-secrets/data-protection.pfx` through explicit Compose overrides.

On Timeweb, place the ring and certificate on documented persistent host storage outside the checkout, image layers, and replaceable release directories (for example, dedicated `/srv/puntiro/state` and `/srv/puntiro/secrets` mounts selected by the operator). The configured service UID/GID owns the ring directory at exact mode `0700`, every key at `0600`, and the certificate regular file at `0600`; this supplies owner read/write for the ring and owner read for the certificate. Configure those absolute host paths before container creation and use the checked wrapper to validate the resolved bind sources before it immediately performs the requested operation. Include the host ring in the independent encrypted backup/restore process. Compose uses `create_host_path: false`; missing sources fail instead of becoming empty directories/files. A container filesystem or unnamed/ephemeral mount is not acceptable custody. The exact Timeweb volume, ownership, backup retention, and restore behavior remain `not run` until recorded on the deployed topology.

## Start PostgreSQL and apply migrations

The normal command starts PostgreSQL only:

```bash
docker compose \
  --env-file infra/compose/.env.cloud \
  --env-file infra/compose/cloud-runtime.env \
  -f infra/compose/cloud-development.yml up -d postgres
docker compose \
  --env-file infra/compose/.env.cloud \
  --env-file infra/compose/cloud-runtime.env \
  -f infra/compose/cloud-development.yml ps
dotnet tool restore
```

The service pins `postgres:17.10-bookworm`, persists data in the named `puntiro-cloud-postgres` volume, publishes PostgreSQL only on `127.0.0.1`, and reports health with `pg_isready`. `PUNTIRO_TEST_POSTGRES` and the host `ConnectionStrings__Puntiro` overlay in `.env.cloud` target the loopback port. The runtime connection targets the Compose service name `postgres` for the Cloud container.

Cloud and provisioning never migrate at startup. Run host tools through the secret-safe helper, placing `.env.cloud` last so its host connection overrides the container connection without evaluating either file as shell:

```bash
node scripts/run-with-cloud-env.mjs \
  --env-file infra/compose/cloud-runtime.env \
  --env-file infra/compose/.env.cloud \
  -- dotnet ef database update --project src/Puntiro.Modules.Tenancy --startup-project apps/cloud --context TenancyDbContext
node scripts/run-with-cloud-env.mjs \
  --env-file infra/compose/cloud-runtime.env \
  --env-file infra/compose/.env.cloud \
  -- dotnet ef database update --project src/Puntiro.Modules.Identity --startup-project apps/cloud --context IdentityDbContext
node scripts/run-with-cloud-env.mjs \
  --env-file infra/compose/cloud-runtime.env \
  --env-file infra/compose/.env.cloud \
  -- dotnet ef database update --project src/Puntiro.Modules.Integrations --startup-project apps/cloud --context IntegrationsDbContext
```

Review generated SQL and confirm the backup before applying production migrations. A failed command stops the release; do not start Cloud with pending migrations.

Provision the first organization with the same host paths, HMAC versions, and host connection overlay. On a new installation this host operation also creates the first protected Data Protection key; Cloud is not allowed to create an empty ring implicitly:

```bash
node scripts/run-with-cloud-env.mjs \
  --env-file infra/compose/cloud-runtime.env \
  --env-file infra/compose/.env.cloud \
  -- dotnet run --project tools/Puntiro.Provisioning -- bootstrap-owner --organization-name Puntiro --organization-slug puntiro --email owner@example.test
```

Follow [First owner provisioning](first-owner-provisioning.md). Do not redirect or record one-time output.

Before any normal Cloud `create`, `up` or `start`, reconcile the ring/key/certificate owner with the numeric `PUNTIRO_CLOUD_UID`/`PUNTIRO_CLOUD_GID`, then use the checked wrapper. It strips ambient Cloud/PostgreSQL/Compose interpolation variables, checks both env files without echoing values, requires the runtime file at exact mode `0600`, validates exact private modes and ownership, uses the actual Npgsql parser, cryptographically loads the certificate/password, and exercises the real ASP.NET Data Protection stack against the protected ring. It then resolves Compose, verifies the resolved image, identity, environment, loopback database port and bind sources against those exact validated files, and immediately performs the requested operation:

```bash
node scripts/cloud-compose.mjs \
  --mode normal \
  --compose-env infra/compose/.env.cloud \
  --runtime-env infra/compose/cloud-runtime.env \
  --operation up-cloud
```

The runtime env is mandatory for this profile. The Cloud service is read-only, uses the configured non-root UID/GID, drops capabilities, uses `no-new-privileges`, refuses Docker-created bind sources, mounts the certificate read-only, and receives arbitrary retained HMAC versions from the ignored raw runtime env file. This repository does not claim that a Timeweb image has been built or deployed.

## Health and trusted Timeweb boundary

`/health/live` proves only that the process responds. `/health/ready` additionally requires PostgreSQL connectivity, no pending module migrations, a usable persistent Data Protection ring, current HMAC keys, and every retained version referenced by durable credentials.

```bash
PUNTIRO_CLOUD_HEALTH_ORIGIN=https://replace-with-reviewed-operator-origin.invalid
curl --fail --silent --show-error "$PUNTIRO_CLOUD_HEALTH_ORIGIN/health/live" >/dev/null
curl --fail --silent --show-error "$PUNTIRO_CLOUD_HEALTH_ORIGIN/health/ready" >/dev/null
```

The committed origin is intentionally unusable. Replace it only in the private operator shell. A liveness success never compensates for a readiness failure.

Before enabling forwarded headers, obtain the exact immediate Timeweb reverse-proxy IP addresses or CIDR networks. Set `Puntiro__Proxy__Enabled=true` and add only the applicable indexed `KnownProxies` and/or `KnownNetworks` assignments to `cloud-runtime.env`. Do not add empty indexed values. Disabled mode has zero allowlist values; enabled mode requires at least one actual proxy or network.

Puntiro accepts one symmetric `X-Forwarded-For`/`X-Forwarded-Proto` hop. Forwarded headers from a direct peer outside the configured trust boundary are ignored. Do not set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. Restrict direct listener access to expected proxies and the reviewed operator path, then revalidate after every Timeweb topology change. Automated tests cover trusted/unknown peers; deployed Timeweb verification remains `not run` until recorded there.

## Deployment, rollback, and key rotation

1. Back up PostgreSQL and separately snapshot the host Data Protection ring, certificate/password custody, and every retained HMAC version.
2. Restore those artifacts in an isolated environment and verify path ownership, modes, and readability.
3. Apply reviewed expand-compatible migrations explicitly.
4. Start the reviewed application image with old and new retained key versions.
5. Require liveness, readiness, and a bounded authentication smoke test with no body or credential logging.
6. Enable traffic only after every gate succeeds.

Rollback returns to the previously reviewed image while retaining the forward-compatible schema and all required key material. Never automate an EF down-migration against production data. Destructive schema reversal requires a separately reviewed restore/migration plan.

To rotate an HMAC purpose, add a new independently generated `Keys__<version>` assignment while retaining all old versions, deploy, change `CurrentVersion`, and verify readiness. Remove an old version only after no durable verifier references it. Integration credentials normally require explicit replacement and revoke. Data Protection certificate rotation is not automated at this stage; test multi-certificate unprotect support before changing the active certificate. Missing ring, certificate, password, or retained HMAC material is an incident, never a reason to delete credentials or silently reset factors.

## Backup and isolated restore drill

Choose a private absolute backup directory. The dump command reads database names inside the container and does not load secret files into the shell:

```bash
umask 077
PUNTIRO_BACKUP_DIR=/replace/with/a/private/puntiro-backup-directory
install -d -m 700 "$PUNTIRO_BACKUP_DIR"
docker compose \
  --env-file infra/compose/.env.cloud \
  --env-file infra/compose/cloud-runtime.env \
  -f infra/compose/cloud-development.yml \
  exec -T postgres sh -ceu 'pg_dump --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --format=custom --no-owner --no-privileges' \
  > "$PUNTIRO_BACKUP_DIR/postgres.dump"
test -s "$PUNTIRO_BACKUP_DIR/postgres.dump"
cp -Rp infra/compose/cloud-data-protection-keys "$PUNTIRO_BACKUP_DIR/data-protection-keys"
cp -p infra/compose/cloud-secrets/data-protection.pfx "$PUNTIRO_BACKUP_DIR/data-protection.pfx"
chmod -R go-rwx "$PUNTIRO_BACKUP_DIR"
```

The copied host ring is the same ring mounted into Cloud and used by provisioning. Retained HMAC values and the certificate password stay in approved secret custody; record only version identifiers and custody references next to the backup record.

Restore only into a new Compose project, database name, loopback port, ring directory, and ignored env files. Before any `docker compose create`, `up`, or `start` for the restore project:

```bash
PUNTIRO_RESTORE_PROJECT=puntiro-restore-drill
cp infra/compose/.env.cloud.example infra/compose/.env.cloud.restore
cp infra/compose/cloud-runtime.env.example infra/compose/cloud-runtime.restore.env
chmod 600 infra/compose/.env.cloud.restore infra/compose/cloud-runtime.restore.env
install -d -m 700 infra/compose/cloud-data-protection-keys.restore
install -d -m 700 infra/compose/cloud-secrets
cp -Rp "$PUNTIRO_BACKUP_DIR/data-protection-keys/." infra/compose/cloud-data-protection-keys.restore/
cp -p "$PUNTIRO_BACKUP_DIR/data-protection.pfx" infra/compose/cloud-secrets/data-protection.restore.pfx
chmod 700 infra/compose/cloud-data-protection-keys.restore
chmod 600 infra/compose/cloud-data-protection-keys.restore/key-*.xml
chmod 600 infra/compose/cloud-secrets/data-protection.restore.pfx
```

Populate `.env.cloud.restore` with the exact project marker, `POSTGRES_DB=puntiro_restore_drill`, the isolated username/password, a distinct non-`5432` loopback port such as `55440`, an absolute `PUNTIRO_CLOUD_RUNTIME_ENV_FILE`, the service `PUNTIRO_CLOUD_UID`/`PUNTIRO_CLOUD_GID`, and the restore certificate host path. Both host-only `ConnectionStrings__Puntiro` and `PUNTIRO_TEST_POSTGRES` must use `Host=127.0.0.1`, that exact distinct port, `Database=puntiro_restore_drill`, and the same username/password as `POSTGRES_USER`/`POSTGRES_PASSWORD`. Populate `cloud-runtime.restore.env` with `Host=postgres;Port=5432;Database=puntiro_restore_drill;...` and those same credentials, the absolute restore ring/certificate paths, the certificate password, and every retained HMAC version from custody. Make the configured service UID/GID the exact owner of the restore ring, each `key-<uuid>.xml`, and certificate. Do not reuse the normal database, credentials, loopback port or normal ring.

Use the same checked wrapper for the first restore service. It validates all three connection targets/credentials with the actual Npgsql parser, rejects duplicate aliases and unsupported multi-host/routing properties, validates private file modes and service ownership, cryptographically loads the certificate/password, and performs a real Data Protection protect/unprotect roundtrip using the restored ring before it resolves Compose and immediately starts isolated PostgreSQL:

```bash
node scripts/cloud-compose.mjs \
  --mode restore \
  --compose-env infra/compose/.env.cloud.restore \
  --runtime-env infra/compose/cloud-runtime.restore.env \
  --restore-project "$PUNTIRO_RESTORE_PROJECT" \
  --operation up-postgres
```

Only after the wrapper succeeds may the dump be restored:

```bash
docker compose -p "$PUNTIRO_RESTORE_PROJECT" \
  --env-file infra/compose/.env.cloud.restore \
  --env-file infra/compose/cloud-runtime.restore.env \
  -f infra/compose/cloud-development.yml \
  exec -T postgres sh -ceu 'pg_restore --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --exit-on-error --no-owner --no-privileges' \
  < "$PUNTIRO_BACKUP_DIR/postgres.dump"
```

Apply any reviewed forward migrations through the helper, using the restore host overlay last. Then create and start only the isolated Cloud container:

```bash
node scripts/run-with-cloud-env.mjs \
  --env-file infra/compose/cloud-runtime.restore.env \
  --env-file infra/compose/.env.cloud.restore \
  -- dotnet ef database update --project src/Puntiro.Modules.Tenancy --startup-project apps/cloud --context TenancyDbContext
node scripts/run-with-cloud-env.mjs \
  --env-file infra/compose/cloud-runtime.restore.env \
  --env-file infra/compose/.env.cloud.restore \
  -- dotnet ef database update --project src/Puntiro.Modules.Identity --startup-project apps/cloud --context IdentityDbContext
node scripts/run-with-cloud-env.mjs \
  --env-file infra/compose/cloud-runtime.restore.env \
  --env-file infra/compose/.env.cloud.restore \
  -- dotnet ef database update --project src/Puntiro.Modules.Integrations --startup-project apps/cloud --context IntegrationsDbContext
node scripts/cloud-compose.mjs \
  --mode restore \
  --compose-env infra/compose/.env.cloud.restore \
  --runtime-env infra/compose/cloud-runtime.restore.env \
  --restore-project "$PUNTIRO_RESTORE_PROJECT" \
  --operation create-cloud
node scripts/cloud-compose.mjs \
  --mode restore \
  --compose-env infra/compose/.env.cloud.restore \
  --runtime-env infra/compose/cloud-runtime.restore.env \
  --restore-project "$PUNTIRO_RESTORE_PROJECT" \
  --operation start-cloud
```

Require both health probes and bounded authentication/readiness acceptance without request/response bodies. Do not point this procedure at production, reuse a production project name, or call the drill successful until PostgreSQL, ring, certificate, and every retained HMAC version have been exercised. Cleanup is an explicit operator action after evidence retention and is omitted to avoid accidental volume deletion.

Record every restore and rotation drill separately. Passing local or CI automation is not proof of Timeweb persistence, storage permissions, certificate custody, backup retention, or external-system behavior.
