# First Owner Provisioning

Use this runbook only from the private deployment environment. Puntiro does not expose an HTTP bootstrap endpoint.

## Prerequisites

- Apply the reviewed Tenancy and Identity migrations before running the command. The CLI never applies migrations.
- Use the exact .NET SDK pinned by `global.json` and a locked restore.
- Supply `ConnectionStrings__Puntiro` from the approved deployment secret source.
- Mount the same persistent Data Protection key ring and PKCS#12 protection certificate used by Cloud.
- Supply all retained session and recovery HMAC key versions, including the configured current versions. Use the configuration names listed in [Identity](../modules/identity.md); never commit their values.
- Run in a private terminal whose standard input and output are attached directly to the operator. The CLI rejects redirected input or output.
- Confirm that no shell recorder, terminal sharing, `script`, `tee`, CI log capture, screen recording, or scrollback synchronization is active.

Do not put a password, TOTP value, recovery code, connection string, certificate password, or HMAC key on the command line. Do not paste a production secret into shell history. The 10-minute command timeout and one confirmation attempt are deliberate safety bounds.

## Command

The identifiers below are dummy examples:

```bash
dotnet run --project tools/Puntiro.Provisioning --configuration Release --no-restore -- \
  bootstrap-owner \
  --organization-name "Example Warehouse" \
  --organization-slug example-warehouse \
  --email owner@example.test
```

The CLI reads the password without echo. It then shows the TOTP enrollment URI and the new recovery-code batch once through the dedicated secret-output boundary. Enroll the URI, store the recovery codes in the approved password manager or offline custody process, and enter the first TOTP code without echo.

Exit codes are stable and contain no credential detail:

- `0`: completed, including a safe idempotent retry;
- `2`: invalid identifiers or command shape;
- `3`: durable organization/account state conflicts with the request;
- `4`: the first TOTP confirmation is invalid;
- `5`: secure-terminal, timeout, configuration, PostgreSQL, or other infrastructure failure.

## Durable sequence and retries

The durable order is pending organization, pending identity credentials, one-time enrollment output, first TOTP confirmation, active owner membership, organization activation, then identity-correlation cleanup. The organization cannot become active before the confirmed factor and owner membership exist.

- If the run stops after enrollment output but before TOTP confirmation, rerun the same slug and normalized email. The pending password, TOTP secret, and recovery batch are replaced; the organization, user, and membership are not duplicated. Earlier output is invalid and must be destroyed.
- A different email cannot claim an organization that already has a pending owner identity. It exits with conflict and does not create another account.
- If organization activation committed but final Identity cleanup did not, rerun the same command. Puntiro verifies the same active owner membership and performs only correlation cleanup. It does not read another password, rotate active credentials, or create another membership.
- An already-active matching organization/owner is unchanged. A wrong email, wrong owner membership, suspended state, or ambiguous durable state fails closed.
- After exit `5`, inspect redacted deployment and database health evidence before retrying. Never infer completion only from terminal output.

Structured application logs do not receive password, TOTP, recovery, URI, or HMAC values. The terminal itself is the explicit one-time secret boundary and therefore remains an operator-controlled manual acceptance surface.
