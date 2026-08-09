# Cloud Configuration Reference

ASP.NET Core maps `__` in environment names to configuration sections. Populated values live only in approved deployment secret storage or ignored local files. Checked-in examples contain blank assignments, and checked-in JSON contains non-secret limits only. Blank or missing required values fail startup or readiness. Never log resolved configuration.

## Files and execution model

`infra/compose/.env.cloud.example` defines Compose interpolation plus host-only connection overlays. Copy it to ignored `.env.cloud`. `infra/compose/cloud-runtime.env.example` defines values passed to Cloud through Compose `env_file` with `format: raw`; copy it to ignored `cloud-runtime.env`. Both populated files must be untracked and mode `0600`.

The format is strict dotenv: one raw `NAME=VALUE` assignment per line. Full-line comments beginning with `#` and blank lines are allowed. Duplicate names within one file, `export`, quotes, inline `#` comments, every dollar sign, backticks, control characters, tabs and leading/trailing value whitespace are rejected. Do not source either file. A later file passed to `run-with-cloud-env.mjs` may intentionally override an earlier file, which is how the host connection overlay replaces the container connection. Use the checked Compose wrapper for service starts; use `node scripts/run-with-cloud-env.mjs --env-file ... -- command` for host tools. Raw semicolons, internal ASCII spaces and base64 `=` padding are preserved exactly under `shell: false`.

Compose loads `cloud-runtime.env` without enumerating HMAC versions or proxy indices. This lets operators supply `v0`, `v1`, `v2`, and later retained versions without changing Compose. Missing proxy variables stay absent; never create blank indexed elements.

## Compose and host-tool variables

| Variable | Format | Class | Notes |
| --- | --- | --- | --- |
| `PUNTIRO_POSTGRES_PORT` | Loopback TCP port | Non-secret | Defaults to `5432`; Compose never binds PostgreSQL on all interfaces. Use a distinct port for restore drills. |
| `POSTGRES_DB` | PostgreSQL identifier | Secret-adjacent | Local/restore database. Restore validation requires a `puntiro_restore_...` name. |
| `POSTGRES_USER` | PostgreSQL identifier | Secret-adjacent | Local database principal. |
| `POSTGRES_PASSWORD` | Strong random password | Secret | Local database password; never commit or echo. |
| `PUNTIRO_CLOUD_IMAGE` | Immutable reviewed image reference | Operational | Defaults to the local placeholder only for opt-in development. Production must use a reviewed immutable image/digest. |
| `PUNTIRO_CLOUD_RUNTIME_ENV_FILE` | Absolute or Compose-relative file path | Operational | Raw ignored runtime env file. Restore validation requires it to equal the file being validated. |
| `PUNTIRO_CLOUD_UID` | Non-zero numeric Unix UID | Operational | Exact owner of the host ring/key files and certificate and the UID forced for the Cloud container. Normal and restore preflight fail on an ownership mismatch. |
| `PUNTIRO_CLOUD_GID` | Non-zero numeric Unix GID | Operational | Exact group of the host ring/key files and certificate and the GID forced for the Cloud container. |
| `PUNTIRO_RESTORE_PROJECT` | Isolated Compose project marker | Operational | Blank during normal use. Restore validation requires an exact dedicated project name before any service is created/started. |
| `Puntiro__Security__DataProtectionCertificateHostPath` | Absolute existing private PKCS#12 path | Operational | Bind-mount source. Must equal the host runtime certificate path in restore validation. |
| `PUNTIRO_TEST_POSTGRES` | Npgsql maintenance connection string | Secret | Host-only integration-test connection; principal must create/drop disposable databases. During restore it must target the isolated restore database, credentials and distinct loopback port exactly. |
| `ConnectionStrings__Puntiro` | Npgsql connection string | Secret | In `.env.cloud`, a host-only loopback overlay for EF/provisioning; in `cloud-runtime.env`, the Cloud connection to Compose service `postgres:5432`. Restore validation requires both host-tool connections and the container connection to use the same isolated database/user/password. The helper loads the host overlay last. |

## Cloud runtime variables

| Variable | Format | Class | Notes |
| --- | --- | --- | --- |
| `Puntiro__Admin__AllowedOrigin` | Absolute HTTPS origin ending `/`, no query/fragment | Non-secret | Exact origin accepted for Admin login and unsafe Admin requests. |
| `Puntiro__Proxy__Enabled` | `true` or `false` | Non-secret | Disabled requires zero allowlist values. Enable only behind the reviewed trusted proxy boundary. |
| `Puntiro__Proxy__KnownProxies__<index>` | Literal IPv4 or IPv6 address | Operational | Optional immediate trusted proxy addresses, densely indexed from zero. Unspecified `0.0.0.0` and `::` are rejected. |
| `Puntiro__Proxy__KnownNetworks__<index>` | IPv4/IPv6 CIDR | Operational | Optional immediate trusted proxy networks, densely indexed from zero. Enabled mode needs at least one actual proxy/network; unrestricted `0.0.0.0/0` and `::/0` are rejected. |
| `Puntiro__Security__DataProtectionKeysPath` | Absolute existing mode-`0700` host directory | Operational | One persistent ring used by host provisioning and bind-mounted into Cloud. Before startup it must contain at least one private regular `key-<uuid>.xml` with the expected protected ASP.NET Data Protection key structure. |
| `Puntiro__Security__DataProtectionCertificatePath` | Absolute existing mode-`0600` host PKCS#12 path | Operational | Host-tool path containing nonempty PKCS#12 material. Compose overrides it with the read-only in-container mount path. |
| `Puntiro__Security__DataProtectionCertificatePassword` | PKCS#12 password | Secret | Store separately from certificate backup. |
| `Puntiro__Security__SessionHmac__CurrentVersion` | 1–32 ASCII letters/digits/`-`/`_` | Non-secret | Version used for new server sessions. |
| `Puntiro__Security__SessionHmac__Keys__v1` | Canonical base64 of exactly 32 random bytes | Secret | Example retained-key name. Add `Keys__<version>` for every stored-session version, including old versions. |
| `Puntiro__Security__RecoveryHmac__CurrentVersion` | 1–32 ASCII letters/digits/`-`/`_` | Non-secret | Version used for new recovery batches. |
| `Puntiro__Security__RecoveryHmac__Keys__v1` | Canonical base64 of exactly 32 random bytes | Secret | Example retained-key name. Keep every version referenced by an unused recovery code. |
| `Puntiro__Security__IntegrationHmac__CurrentVersion` | 1–32 ASCII letters/digits/`-`/`_` | Non-secret | Version used for newly issued integration tokens. |
| `Puntiro__Security__IntegrationHmac__Keys__v1` | Canonical base64 of exactly 32 random bytes | Secret | Example retained-key name. Keep every version referenced by an unrevoked token. |

The generic retained-key forms are `Puntiro__Security__SessionHmac__Keys__<version>`, `Puntiro__Security__RecoveryHmac__Keys__<version>`, and `Puntiro__Security__IntegrationHmac__Keys__<version>`. All configured retained values are validated, not only each current version. Key bytes must be distinct across the three purposes. `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is forbidden because it bypasses the explicit allowlist.

## Non-secret limits

These keys are committed in `apps/cloud/appsettings.json` and may be overridden deliberately through equivalent `Puntiro__Security__...` environment names:

| Key | Default | Accepted range |
| --- | ---: | ---: |
| `LoginIpLimit` | 10 | 1–1000 |
| `LoginAccountLimit` | 5 | 1–1000 |
| `StepUpSessionLimit` | 5 | 1–1000 |
| `RateLimitWindowSeconds` | 60 | 1–3600 |
| `MaximumRateLimitPartitions` | 10000 | 100–1000000 |
| `MaximumRequestBodyBytes` | 16384 | 1024–1048576 |
| `MaximumHeaderBytes` | 32768 | 4096–1048576 |

Integration bearer pre-authentication, verified-token/direct-client, and Admin token-management limits are fixed at 120 per minute in this stage. When the trusted boundary is enabled, the rate-limit client address comes only from the accepted single forwarded hop. Headers from unknown peers are ignored.

Populated runtime or example configuration is rejected by `node scripts/check-cloud-security.mjs`. It enumerates every Git-tracked UTF-8 text file regardless of path or extension, recognizes JSON, block/inline YAML, Kubernetes name/value pairs, dotenv/INI/TOML/property, XML and Compose fallback assignments, and recursively scans bounded canonical/base64 integration tokens. Pure variable references and documentation names stay allowed. Local secret files are outside that scan only when they are both ignored and untracked; force-tracked ignored files are scanned. Diagnostics identify only paths and classes, never resolved values.

Before normal Cloud creation or startup, run `node scripts/cloud-compose.mjs --mode normal --compose-env infra/compose/.env.cloud --runtime-env infra/compose/cloud-runtime.env --operation up-cloud`. This is one checked operation: it removes ambient Cloud/PostgreSQL/Compose interpolation variables, validates exact private files and ownership, parses the container and host application connections with Npgsql, cryptographically loads the PKCS#12 certificate, performs a real ASP.NET Data Protection protect/unprotect roundtrip against the restored ring, resolves and verifies Compose, then immediately starts the requested service. Restore mode additionally parses and compares the maintenance connection against the isolated target. Compose independently marks the runtime env as required and uses long bind syntax with `create_host_path: false`, so missing paths cannot be replaced with empty Docker-created directories or files. Do not bypass the wrapper with direct Cloud or restore-project start commands.
