# Manual integration-token rotation

Use this runbook for an external system that can send one fixed HTTP request with one
`Authorization` header. Puntiro integration tokens do not use an OAuth refresh flow and cannot be
recovered after their one-time create response.

## Preconditions

- Sign in as an active organization owner and complete a current TOTP step-up.
- Confirm that the Data Protection store, PostgreSQL, and every retained integration HMAC version
  are healthy through `/health/ready`.
- Have an approved way to update the external system without copying the credential into chat,
  tickets, command history, screenshots, or logs.
- Ensure the organization has only one active token. The two-token limit intentionally leaves one
  slot for rotation.

## Rotate

1. List `GET /api/admin/integration-tokens` and record the old token's safe `id`, `publicId`, scopes,
   and current `version`. The list never contains raw credentials or verifier bytes.
2. Create the replacement with `POST /api/admin/integration-tokens`, the exact required scopes, the
   current Admin session, `Origin`, and `X-Puntiro-CSRF`. Capture the returned `token` once into the
   approved secret field of the external system. If the response is lost or incomplete, do not
   assume creation failed: list metadata, locate the new token, and revoke it before trying again.
3. Configure the external system's single header as `Authorization: Bearer <new token>`. Do not add a
   second Authorization value, surrounding whitespace, a cookie, an organization header, or a
   request-body tenant override.
4. Send the external system's normal request and verify the expected successful business response.
   A generic `401` does not distinguish malformed, unknown, revoked, wrong-secret, missing-key, or
   inactive-organization states. A `403` means the verified token lacks the exact required scope. A
   `429` includes `Retry-After`: the accepted client address may have exhausted the aggregate
   pre-authentication budget, or that verified token/client-address pair may have exhausted its
   second budget.
5. Re-list the metadata and confirm the replacement's `lastUsedAt` is populated. Use the latest old
   token `version`, then call `POST /api/admin/integration-tokens/{old-id}/revoke` with the normal
   Admin `Origin` and antiforgery header. A version conflict requires a fresh list and deliberate
   review; do not blindly retry with an invented version.
6. Verify that the old credential now receives the same generic `401` as any invalid credential and
   that the replacement still succeeds.

## Failure handling

- If the new credential fails before the old one is revoked, restore the external system to the old
  header and investigate without exposing either value.
- If a create operation reports an infrastructure failure, list metadata before another create. A
  commit may have completed even when a response could not be delivered.
- If two active tokens already exist, revoke only after identifying their safe metadata and current
  versions. A third token is rejected with `integration_token.active_limit`.
- Treat a missing historical HMAC key, lost one-time credential, or unexplained token use as an
  incident. Never place a raw token, verifier, key, or request body in diagnostics.
- The pre-authentication budget is 120 attempts per minute per accepted client address. Different
  external systems behind one NAT address share it, so one noisy client can temporarily limit its
  neighbors. Forwarded addresses count only through the reviewed single-hop Timeweb allowlist;
  spoofed headers from any other direct peer are ignored.

This procedure requires deployed external-system acceptance. Automated Cloud tests prove API,
authorization, replay, and PostgreSQL behavior only; they do not prove the external system or
Timeweb proxy configuration.
