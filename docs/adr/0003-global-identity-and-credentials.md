# ADR 0003: Global Identity and Credential Boundaries

- Status: Accepted
- Date: 2026-08-08

## Context

Puntiro needs one administrative identity that may belong to an organization without copying credentials into every tenant. The MVP external system can send a single configured HTTP request and header, but cannot perform an OAuth exchange or rotate credentials automatically. Authentication data must remain independently reviewable from organization lifecycle and integration access.

## Decision

- `Puntiro.Modules.Identity` owns globally unique normalized admin accounts, Argon2id password credentials, RFC 6238 TOTP, one-time recovery codes and revocable server sessions in schema `identity`.
- `Puntiro.Modules.Tenancy` owns organizations and memberships in schema `tenancy`. A global account receives organization access only from an active membership; request input is never tenant authorization.
- `Puntiro.Modules.Integrations` owns HMAC-only integration-token verifiers, scopes and revoke state in schema `integrations`. The raw bearer is returned once and never persisted.
- Each module owns its EF context and migrations. Cross-module identifiers are opaque UUIDs and no module reads another module's tables.
- Admin login uses one request containing password and TOTP or recovery code. The resulting Secure, HttpOnly, SameSite=Strict host-only cookie refers to a server-authoritative HMAC-verified session.
- The first owner and owner TOTP recovery are private deployment CLI workflows. There is no public bootstrap or self-service credential-reset endpoint in this stage.
- Independent versioned HMAC key sets protect sessions, recovery codes and integration tokens. TOTP secrets use a persistent ASP.NET Core Data Protection ring protected by the deployment certificate.

## Consequences

Forwarded client addresses are accepted only through one symmetric hop from an explicit immediate-proxy address or bounded CIDR. The unspecified addresses `0.0.0.0` and `::`, the unrestricted networks `0.0.0.0/0` and `::/0`, an empty allowlist, and the ASP.NET Core platform forwarding shortcut all fail Cloud startup closed. This prevents an unrestricted network from turning attacker-controlled `X-Forwarded-For` values into rate-limit partitions.

- Email uniqueness is global while organization authorization remains membership-scoped.
- Credential reset can revoke every server session without changing Tenancy data.
- Two active integration tokens permit deliberate manual replacement without an automatic refresh flow.
- Production startup must fail when key material is unavailable and must never apply migrations.
- PostgreSQL, Data Protection state, certificate custody and retained HMAC versions are separate backup and recovery concerns.

## Rejected Alternatives

- ASP.NET Core Identity's default shared credential/data model was rejected for this stage because it would obscure the approved module-owned schemas and explicit one-time/HMAC persistence contracts. The decision does not reject its security components categorically; adopting it later requires a new schema and migration decision.
- Duplicating admin accounts per organization was rejected because it creates conflicting email identity and credential lifecycles.
- OAuth Client Credentials or an authorization server was rejected for MVP because the source system supports only one preconfigured request and cannot perform an exchange.
- Persisting recoverable raw session or integration secrets was rejected because database disclosure would become immediate credential disclosure.
