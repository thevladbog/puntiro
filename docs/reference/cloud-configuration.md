# Cloud Configuration Reference

ASP.NET Core maps `__` in environment names to configuration sections. Secret values come from an approved deployment secret source or the ignored local env file; checked-in JSON contains only non-secret limits. Blank or missing required values fail startup or readiness. Do not log the resolved configuration.

## Runtime variables

| Variable | Format | Class | Notes |
| --- | --- | --- | --- |
| `ConnectionStrings__Puntiro` | Npgsql connection string | Secret | Required by Cloud, EF design-time factories and provisioning. Never commit or echo it. |
| `Puntiro__Admin__AllowedOrigin` | Absolute HTTPS origin with `/`, no query/fragment | Non-secret | Exact `Origin` accepted for Admin login and unsafe Admin requests. |
| `Puntiro__Proxy__Enabled` | `true` or `false` | Non-secret | Enable only behind the reviewed trusted proxy boundary. |
| `Puntiro__Proxy__KnownProxies__<index>` | Literal IPv4 or IPv6 address | Operational | Immediate trusted proxy address. At least one proxy/network is required when enabled. |
| `Puntiro__Proxy__KnownNetworks__<index>` | IPv4/IPv6 CIDR | Operational | Immediate trusted proxy network. Puntiro processes one symmetric forwarded hop. |
| `Puntiro__Security__DataProtectionKeysPath` | Absolute existing directory | Operational | Persistent writable key ring, mounted with least privilege. |
| `Puntiro__Security__DataProtectionCertificatePath` | Absolute existing PKCS#12 path | Operational | Protection certificate mounted read-only where supported. |
| `Puntiro__Security__DataProtectionCertificatePassword` | PKCS#12 password | Secret | Store separately from the certificate backup. |
| `Puntiro__Security__SessionHmac__CurrentVersion` | 1–32 ASCII letters/digits/`-`/`_` | Non-secret | Version used for new server sessions. |
| `Puntiro__Security__SessionHmac__Keys__<version>` | Base64 of exactly 32 random bytes | Secret | Keep every version referenced by a stored session. |
| `Puntiro__Security__RecoveryHmac__CurrentVersion` | 1–32 ASCII letters/digits/`-`/`_` | Non-secret | Version used for new recovery-code batches. |
| `Puntiro__Security__RecoveryHmac__Keys__<version>` | Base64 of exactly 32 random bytes | Secret | Keep every version referenced by an unused recovery code. |
| `Puntiro__Security__IntegrationHmac__CurrentVersion` | 1–32 ASCII letters/digits/`-`/`_` | Non-secret | Version used for newly issued integration tokens. |
| `Puntiro__Security__IntegrationHmac__Keys__<version>` | Base64 of exactly 32 random bytes | Secret | Keep every version referenced by an unrevoked token. |

The three HMAC current keys must exist and all key bytes must be distinct across purposes. `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is forbidden because it bypasses the explicit trusted-proxy allowlist.

## Non-secret limits

These keys are committed in `apps/cloud/appsettings.json` and may be overridden deliberately through the equivalent `Puntiro__Security__...` environment name:

| Key | Default | Accepted range |
| --- | ---: | ---: |
| `LoginIpLimit` | 10 | 1–1000 |
| `LoginAccountLimit` | 5 | 1–1000 |
| `StepUpSessionLimit` | 5 | 1–1000 |
| `RateLimitWindowSeconds` | 60 | 1–3600 |
| `MaximumRateLimitPartitions` | 10000 | 100–1000000 |
| `MaximumRequestBodyBytes` | 16384 | 1024–1048576 |
| `MaximumHeaderBytes` | 32768 | 4096–1048576 |

Integration bearer pre-authentication, verified-token/direct-client, and Admin token-management limits are fixed at 120 per minute in this stage. When the trusted proxy boundary is enabled, the rate-limit client address is derived from the accepted single forwarded hop. Headers from unknown peers are ignored.

## Local and test-only variables

| Variable | Format | Class | Notes |
| --- | --- | --- | --- |
| `POSTGRES_DB` | PostgreSQL identifier | Local secret-adjacent | Used only by the development compose service. |
| `POSTGRES_USER` | PostgreSQL identifier | Local secret-adjacent | Used only by the development compose service. |
| `POSTGRES_PASSWORD` | Strong random password | Secret | Used only by the ignored local compose env file. |
| `PUNTIRO_POSTGRES_PORT` | Loopback TCP port | Non-secret | Defaults to `5432`; compose never binds all interfaces. |
| `PUNTIRO_TEST_POSTGRES` | Npgsql maintenance connection string | Secret | Required by integration tests; the principal must create/drop disposable databases. |

The template `infra/compose/.env.cloud.example` contains names and generation guidance only. Populated runtime/example configuration is rejected by `node scripts/check-cloud-security.mjs`.
