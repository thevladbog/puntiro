# Cloud Authentication API Reference

The generated contract is available at `GET /openapi/v1.json`. Errors use `application/problem+json` and include stable `code` plus an opaque `traceId`; credential failures do not disclose which account or factor exists.

## Admin session routes

| Route | Authentication | Request/response |
| --- | --- | --- |
| `POST /api/admin/auth/login` | Exact configured HTTPS `Origin`; no existing session | JSON contains email, password and exactly one of six-digit `totpCode` or `recoveryCode`. Success is `204` and sets the session cookie. |
| `GET /api/admin/auth/session` | Admin session cookie and active owner membership | Returns safe user/organization metadata, idle/absolute expiry, optional TOTP freshness and an antiforgery token. |
| `POST /api/admin/auth/step-up` | Admin cookie, exact `Origin`, `X-Puntiro-CSRF` | Accepts only a replay-protected current TOTP. Success is `204`. |
| `POST /api/admin/auth/logout` | Admin cookie, exact `Origin`, `X-Puntiro-CSRF` | Revokes the server session and expires the cookie. Success is `204`. |

`__Host-puntiro_session` is `Secure`, `HttpOnly`, `SameSite=Strict`, `Path=/`, and has no `Domain`. PostgreSQL stores only its public ID and a versioned HMAC verifier. Recovery login creates a session without fresh step-up.

## Integration-token administration

All routes require the Admin cookie, active owner membership and the normal CSRF/Origin boundary for unsafe methods.

| Route | Additional rule | Response |
| --- | --- | --- |
| `GET /api/admin/integration-tokens` | Metadata-only list | Safe IDs, name, scopes, timestamps, revoke state and optimistic version; never raw token/verifier. |
| `POST /api/admin/integration-tokens` | TOTP step-up not older than five minutes; one or both approved scopes | `201`; raw `pnt_live_...` token appears exactly once with safe metadata. |
| `POST /api/admin/integration-tokens/{id}/revoke` | Exact expected optimistic version | `204`; repeat after accepted revoke is idempotent. |

Scopes are exactly `shipments.read` and `shipments.write`. At most two tokens may be active for an organization. Rotation is manual: issue the second, update and verify the external system, then revoke the first.

## Integration bearer behavior

Future `/api/v1` use cases authenticate only `Authorization: Bearer pnt_live_<public-id>.<secret>`. An Admin cookie is not a fallback. Organization and scopes are derived from the verified durable token; body, query and custom headers cannot establish tenant context. Revocation and active organization state are rechecked on every request.

The pre-authentication limiter permits 120 attempts per minute per accepted client address and performs no token lookup after exhaustion. A verified token also consumes a 120-per-minute token-public-ID/client partition. Behind Timeweb, the client address changes only through one symmetric forwarded hop from the explicit proxy/network allowlist. Spoofed forwarding headers from an unknown peer are ignored.

## Stable problem codes

- Request/routing: `request.invalid`, `request.failed`, `request.not_found`, `request.method_not_allowed`, `request.too_large`, `request.unsupported_media_type`.
- Admin authentication/authorization: `auth.invalid_credentials`, `auth.session_expired`, `auth.forbidden`, `auth.csrf_invalid`, `auth.rate_limited`, `auth.organization_selection_required`, `auth.step_up_required`.
- Integration token administration: `integration_token.active_limit`, `integration_token.creation_conflict`, `integration_token.not_found`, `integration_token.version_conflict`.
- Integration bearer: `integration.invalid_credentials`, `integration.scope_forbidden`, `integration.rate_limited`.
- Readiness: `configuration.not_ready`.

`401` intentionally merges malformed, unknown, wrong-secret, revoked and unavailable-key integration credentials. `403` signals a verified principal without the required authorization/scope. `409` is a durable state/version conflict. `429` includes `Retry-After`.
