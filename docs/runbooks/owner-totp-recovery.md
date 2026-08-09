# Owner TOTP Recovery

`reset-owner-totp` is a non-public deployment command for an existing active owner. There is no HTTP self-service reset in this stage.

## Prerequisites

- Follow the secure-terminal, reviewed-migration, PostgreSQL, Data Protection certificate/key-ring, and retained HMAC-key requirements in [First owner provisioning](first-owner-provisioning.md).
- Confirm the requested organization is active and the account is its active owner.
- Have the account password and one unused recovery code available through separate approved custody where possible.
- Stop shell/terminal recording, output capture, screen sharing, scrollback synchronization, `script`, and `tee` before running the command.

The password and recovery code are hidden interactive input. Never supply them as arguments, environment variables, redirected input, or shell-history text. The command permits one first-TOTP confirmation within the shared 10-minute timeout.

## Command

The identifiers below are dummy examples:

```bash
dotnet run --project tools/Puntiro.Provisioning --configuration Release --no-restore -- \
  reset-owner-totp \
  --organization-slug example-warehouse \
  --email owner@example.test
```

Puntiro normalizes the slug and email through their canonical module rules, resolves only a generic optional identifier through trusted CLI composition, and verifies the active owner membership. These read-only lookups do not authenticate, consume a recovery code, create an event, or change a session.

The CLI then reads the password and unused recovery code without echo. After both are verified, it shows a pending TOTP enrollment URI and replacement recovery-code batch once. Enroll the new URI, store the new codes safely, and enter the first new TOTP code without echo.

## Atomic behavior

Preparing the replacement keeps the candidate TOTP secret and recovery batch only in process memory. No stored credential changes before a valid first new TOTP code.

Successful completion performs one Identity transaction that:

- rechecks that the authorizing old recovery row is still unused;
- replaces and confirms the TOTP credential;
- removes every old recovery code and stores only the replacement batch verifiers;
- advances the authentication epoch and revokes every existing session;
- appends a redacted security event.

An invalid first new TOTP code returns exit `4`; the old TOTP/recovery credentials and sessions remain unchanged. If the process stops before commit, discard the displayed candidate material and rerun with the same still-unused old recovery code. If completion may have committed but the terminal result was lost, first verify session revocation and credential state through an approved administrative investigation; do not repeatedly guess with old or new secrets.

Exit `0` means the replacement committed. Exit `2`, `4`, or `5` means invalid identifiers, generic invalid credentials/confirmation, or infrastructure failure respectively. Credential errors never reveal whether the account, password, recovery code, organization, or membership was the failing element.
