# Identity Module

`Puntiro.Modules.Identity` owns global admin accounts, credentials, authentication security events, and server sessions. It does not decide which organizations a user may access and never reads `tenancy` or `integrations` tables. Organization IDs in owner provisioning and sessions are opaque identifiers supplied by an orchestrating use case; Tenancy remains responsible for active membership and organization checks.

HTTP routes, cookies, antiforgery, rate limiting, membership selection, and the interactive provisioning CLI are composed in later host/tool stages. The module exposes application contracts so those boundaries do not bypass its credential and transaction rules.

## Owned PostgreSQL schema

The module owns only schema `identity`. Migration `202608080002_InitialIdentity` creates:

- `admin_users`: UUIDv7 ID, display email, exact normalized identity key, `provisioning|active|suspended` status, positive authentication epoch, nullable provisioning organization ID, UTC timestamps, and optimistic-concurrency version;
- `password_credentials`: one credential per user with salt, Argon2id hash, persisted algorithm parameters, set/rehash timestamps, and version;
- `totp_credentials`: one credential per user with Data Protection ciphertext, confirmation/replacement timestamps, last accepted RFC 6238 counter, and version;
- `recovery_codes`: one row per HMAC verifier with batch ID, recovery-key version, issue/use timestamps, and version;
- `sessions`: UUIDv7 record ID, unique random public ID, versioned HMAC verifier, user and selected organization IDs, captured authentication epoch, idle/absolute timestamps, optional TOTP freshness timestamp, revoke metadata, and version;
- `security_events`: append-only, bounded, redacted authentication/session events with subject, nullable actor, trace ID, result, and reason code;
- `__EFMigrationsHistory`: migration metadata scoped to `identity`, never `public`.

PostgreSQL enforces the exact unique `normalized_email`, positive user/session authentication epochs, one password/TOTP row per user, unique session public ID, lowercase account states, in-schema foreign keys, and indexes used by recovery, revocation, and expiry paths. EF rejects tracked security-event updates/deletes, and a PostgreSQL trigger rejects direct updates/deletes. Security events contain bounded reason codes and identifiers only: no email, password, TOTP, recovery code, raw session token, HMAC verifier, Data Protection ciphertext, user-agent, or arbitrary caller text.

All factor acceptance, replay counters, recovery consumption, credential replacement, account suspension, session revocation, and corresponding security events commit in the same Identity transaction. User-row locks serialize competing TOTP/recovery and authentication-epoch operations. Session creation locks and reloads the user, and session validation locks then reloads both user and session; neither path trusts stale EF tracked state. A caller never receives a session principal for an inactive user, a mismatched epoch, a revoked record, or a record observed at or after an expiry boundary.

## Email identity key

Email normalization uses the version 1 identity-key policy below. Database uniqueness uses the resulting `Normalized` value exactly and does not add a different collation or culture-sensitive transform.

1. Trim surrounding whitespace while retaining that trimmed input as the display value.
2. Normalize the full address to Unicode NFC and require exactly one non-edge `@`.
3. Fold each Unicode scalar in the local part with invariant upper-case followed by invariant lower-case. This collapses peers such as Greek sigma/final sigma without depending on process culture.
4. Convert the domain through `IdnMapping` with STD3 ASCII rules, then lower-case the ASCII result invariantly.
5. Reject a normalized key longer than 320 UTF-8 bytes.

The policy deliberately omits provider-specific mailbox rules. Changing the fold is a data migration and requires a new policy version.

## Password material

New passwords are validated by Unicode scalar count and strict UTF-8 byte count and are not Unicode-normalized. The current Argon2id policy is 19,456 KiB memory, two iterations, parallelism one, a unique 16-byte salt, and a 32-byte output. The stored algorithm and parameters allow a successful login to rehash a weaker supported credential with the current policy in the same transaction.

Verification applies a bounded input policy before Argon2 allocation. Unknown, suspended, and otherwise unavailable accounts still execute one current-policy Argon2id verification against a fixed dummy credential before returning the same `null` result used for every credential failure. Public contracts therefore do not reveal whether an email exists or which factor failed.

Salt/hash values are excluded from JSON, debugger, and string representations. `AdminCredentials` is an application object rather than an HTTP DTO: all four fields are explicitly ignored by System.Text.Json, hidden from debugger browsing, and replaced by the type name in string output. Comparisons are constant-time, and temporary derived buffers are cleared where managed memory permits. Password, factor, and credential objects override string representations with type names or redacted output rather than values.

## TOTP and recovery factors

TOTP follows RFC 6238 with HMAC-SHA-1, six digits, a 30-second period, and deterministic `current, previous, next` checking. A user-row lock protects `last_accepted_counter`; an accepted counter may never be accepted again, including by a concurrent login or step-up request. TOTP bytes are encrypted before being assigned to an EF entity by an `IDataProtector` whose purpose chain contains `Puntiro.Identity.Totp.v1` and the user ID.

Recovery batches contain ten unique 128-bit values rendered as grouped uppercase unpadded Base32. PostgreSQL receives only purpose-bound HMAC-SHA-256 verifier bytes and a recovery key version. Verification selects the recorded version so an explicitly retained old recovery key remains usable during controlled rotation. A recovery login atomically marks exactly one row used and creates a verified identity whose factor is `RecoveryCode`; it never grants fresh TOTP step-up.

Owner TOTP reset is an opaque two-phase contract. Preparation verifies the active account password and an unused old recovery row, but stores no candidate. It returns caller-owned mutable secret output plus an opaque object containing the candidate material and old row version. Completion first validates the new TOTP, then re-locks and reloads the user and verifies that the old recovery row is still unused at the captured version. One transaction advances the authentication epoch, replaces the TOTP credential and recovery batch, invalidates all old recovery rows, revokes all existing sessions, and appends a redacted event. A failed first code changes no stored credential or session. Account suspension uses the same user-first lock order, advances the epoch, and revokes all sessions in one transaction.

Password hash, TOTP secret, and recovery batch acquisition share one cleanup ownership boundary. A failure at any later acquisition or persistence step clears every earlier mutable derived/secret buffer and disposes every one-time code that was not transferred to the caller. Reset candidate acquisition follows the same rule.

`SensitiveValue` owns mutable characters, clears them on idempotent disposal, and exposes an immutable string only through the explicit `Reveal` boundary. Provisioning/HTTP callers must dispose `PendingOwnerIdentity`, `PendingOwnerTotpReset`, and `IssuedAdminSession` after their one-time output boundary. They must never place revealed values in structured logs, exceptions, command arguments, environment variables, traces, or persisted output.

## Server sessions

An issued raw token has canonical form:

```text
pns_<32 lowercase hex public-id>.<43 character base64url secret>
```

The public ID and independent 32-byte secret are generated through `ISecretGenerator`. PostgreSQL stores only the public ID, current session-key version, and `HMAC-SHA-256("Puntiro.Identity.Session.v1" || NUL || public-id || secret)`. Parsing is length-bounded and reformats the public ID before an ordinal comparison, so uppercase, mixed-case, and other non-canonical IDs are rejected before database lookup. Verification uses the key version on the row and constant-time comparison. A malformed token, missing/retired key, wrong verifier, inactive account, stale authentication epoch, revoked record, or expired record returns `null` without exposing a provider error.

Session lifetime is server-authoritative:

- idle lifetime: 30 minutes;
- absolute lifetime: 12 hours;
- a request at either exact boundary is expired;
- successful activity persists `last_seen_at` and extends idle expiry at most once per five minutes;
- idle extension is capped by absolute expiry;
- logout, single revoke, bulk user revoke, and successful TOTP reset take effect immediately.

`VerifiedIdentity` carries the durable authentication epoch read under the factor transaction. Session creation accepts it only when a locked and reloaded active user still has that exact epoch. Every session stores the epoch it was created under; validation rechecks the current active user and epoch. Reset and suspension advance the epoch before committing, so a stale verified identity cannot create a valid post-change session even when creation races the security operation.

TOTP login copies the factor verification time to `second_factor_verified_at`. Recovery login stores `null`. `StepUpTotpAsync` verifies a new replay-protected TOTP counter and returns its accepted UTC time; the Cloud host is responsible for applying that result to its current-session use case and five-minute authorization policy.

## Application contracts

`IIdentityProvisioningService` provides resumable first-owner identity creation, initial TOTP confirmation, owner activation, two-phase owner TOTP reset, and atomic account suspension. A provisioning account may be resumed only for the same `provisioning_organization_id`; a different organization, active account, or suspended account fails closed rather than rebinding a global identity.

`FindUserIdForTrustedProvisioningAsync` is a non-mutating normalized-email lookup for the non-public provisioning/recovery composition only. It returns only an optional opaque user ID and never authenticates, consumes a factor, changes a session, or appends an event. HTTP handlers must not expose it as an account-discovery response. Recovery authorization remains entirely inside `PrepareOwnerTotpResetAsync` and its atomic completion path.

`IAdminAuthenticationService` verifies password plus exactly one TOTP/recovery factor and performs replay/one-time state transitions. It returns `VerifiedIdentity?`, so every invalid external credential shape and value has the same result. `StepUpTotpAsync` accepts only TOTP.

`IAdminSessionService` creates, validates, revokes one, or revokes every user session. `AdminSessionPrincipal` receives its user and organization identifiers only from the durable verified row. Callers must not treat an organization ID from request input as authorization; the Cloud host must recheck Tenancy.

Every provisioning, authentication, factor-change, session-create, and session-revoke operation that emits an event requires an `IdentityAuditContext`. Its trace ID is 1–128 characters and accepts only ASCII letters, digits, `.`, `_`, `:`, and `-`; an optional actor cannot be the empty GUID. Unauthenticated login uses a null caller actor and derives the actor only after successful authentication. Self-service operations derive the subject as actor when no actor is supplied; administrative suspension/revoke may supply the already-authorized actor. The caller must pass only an opaque correlation ID, never an email, network address, credential, code, token, or arbitrary request text. Session validation deliberately accepts no audit context because it emits no per-request event and must not create an attacker-controlled durable event stream.

## Key configuration and migrations

`IdentityKeyOptions` requires separate 32-byte versioned key sets for session and recovery HMAC purposes. Current versions must be present and new records always use them; retained versions verify older rows. The options object and every credential/token model have redacted string/debugger output. Reusing a session key as a recovery, integration-token, Data Protection, or rate-limit key is forbidden.

Cloud and the private provisioning CLI share these deployment configuration names; values come only from the approved secret source:

```text
ConnectionStrings__Puntiro
Puntiro__Security__DataProtectionKeysPath
Puntiro__Security__DataProtectionCertificatePath
Puntiro__Security__DataProtectionCertificatePassword
Puntiro__Security__SessionHmac__CurrentVersion
Puntiro__Security__SessionHmac__Keys__<version>
Puntiro__Security__RecoveryHmac__CurrentVersion
Puntiro__Security__RecoveryHmac__Keys__<version>
```

HMAC values are base64 encodings of exactly 32 random bytes. Every retained version needed by a stored verifier must remain configured; the current version must be present in its corresponding key set.

`AddIdentityModule` registers the Identity context and services, but deployment composition owns durable Data Protection configuration. Cloud and provisioning must use the same persistent key ring and application name, protect the ring with the configured deployment certificate, and fail readiness when the directory, certificate, current versioned keys, or required historical keys are unavailable. Losing a Data Protection or HMAC key is an incident, not a normal reset path.

The design-time factory reads only `ConnectionStrings__Puntiro`; it has no fallback credential. Production startup never applies migrations. Restore the pinned tool and generate/review migrations with an explicitly supplied development connection:

```bash
dotnet tool restore
ConnectionStrings__Puntiro='<development connection from an approved secret source>' \
  dotnet ef migrations add MigrationName \
  --project src/Puntiro.Modules.Identity \
  --startup-project apps/cloud \
  --context IdentityDbContext \
  --output-dir Persistence/Migrations
```

Never paste a production connection string into shell history or documentation. Deployment applies a reviewed migration bundle or SQL before starting the new Cloud process.

## Failure modes

- Empty identifiers, invalid email/password policy, unsafe revoke reason codes, or malformed key configuration fail before persistence.
- Existing email conflicts, mismatched provisioning organization, incomplete TOTP confirmation, inactive accounts, stale reset candidates, and missing users fail closed.
- Credential authentication returns only generic `null`; detailed but credential-free reason codes remain in the private security-event stream.
- Missing Data Protection/HMAC keys, an unreadable key ring, invalid ciphertext, or unknown key versions are configuration/security incidents and never trigger implicit credential replacement.
- A PostgreSQL uniqueness, serialization, concurrency, or provider error is not translated into credential detail and must not be logged with connection or parameter values.
- A request at or after idle/absolute expiry and every request after committed revoke returns no principal.
- Direct security-event mutation is rejected by both EF and PostgreSQL.
- Missing `ConnectionStrings__Puntiro` fails design-time context creation.
- Missing `PUNTIRO_TEST_POSTGRES` fails integration setup; tests never fall back to SQLite, EF in-memory, or mocks.

## Verification

Use a maintenance connection whose principal may create and drop isolated test databases. Do not put credentials in committed scripts or reports.

```bash
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj \
  --configuration Release \
  --filter "FullyQualifiedName~Identity|FullyQualifiedName~Session"

PUNTIRO_TEST_POSTGRES='<maintenance connection from an approved secret source>' \
  dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj \
  --configuration Release \
  --filter FullyQualifiedName~Identity
```

The PostgreSQL suite applies the real migration and verifies model parity, schema ownership, normalized-email uniqueness, encrypted TOTP storage, HMAC-only recovery/session persistence, strict expiry/revoke behavior, atomic replay/recovery concurrency, and all-or-nothing TOTP reset. These checks are automated evidence only; deployment key recovery remains an operational acceptance gate.
