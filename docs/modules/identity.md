# Identity cryptographic primitives

`Puntiro.Modules.Identity` currently provides cryptographic and canonicalization primitives only. Persistence, login orchestration, HTTP endpoints, sessions, provisioning commands, and integration-token administration are separate later stages.

## Email identity key

Email normalization uses the version 1 identity-key policy below. Any database uniqueness constraint must use the resulting `Normalized` value exactly; it must not apply a different collation or an additional culture-sensitive transform.

1. Trim surrounding whitespace while retaining that trimmed input as the display value.
2. Normalize the full address to Unicode NFC and require exactly one non-edge `@`.
3. Fold each Unicode scalar in the local part with invariant upper-case followed by invariant lower-case. This deterministic simple fold collapses peers such as Greek sigma/final sigma without depending on the process culture.
4. Convert the domain through `IdnMapping` with STD3 ASCII rules, then lower-case its ASCII result invariantly.
5. Reject a normalized key longer than 320 UTF-8 bytes.

The policy intentionally does not add culture-sensitive comparison, provider-specific mailbox rules, or further IDN behavior. Changes to the fold are data migrations and require an explicit policy version.

## Password material

New passwords are validated by Unicode scalar count and strict UTF-8 byte count and are passed to Argon2id without Unicode normalization. Creation currently uses 19,456 KiB, two iterations, parallelism one, a 16-byte random salt, and a 32-byte output. Verification applies a separate, larger hard input bound before Argon2 so a future creation-policy change cannot reject historical conforming credentials. Invalid Unicode and over-bound ordinary candidates return a failed verification; null arguments and cancellation remain programmer/control-flow errors.

Salt and hash bytes are excluded from the production JSON contract and from string/debugger displays. Hash comparison is constant-time, and temporary encoded/derived buffers are cleared where managed memory permits.

## TOTP and recovery codes

TOTP follows RFC 6238 interoperability parameters: HMAC-SHA-1, six digits, and a 30-second period. Recovery batches contain ten unique 16-byte random values rendered as canonical uppercase unpadded Base32. Generation, grouping, normalization, and HMAC input assembly use owned mutable byte/character buffers that are cleared on every exit path; they do not materialize the raw code as an immutable string. Only purpose-bound HMAC-SHA-256 verifiers are retained. Duplicate draws are retried within a strict bound; exhausting that bound clears the partial batch and fails without returning it.

`SensitiveValue` copies secret text into owned mutable storage, provides scoped span access, clears that storage on idempotent disposal, and throws on access after disposal. Its JSON, string, and debugger representations are redacted. An immutable raw recovery-code string first appears only when the explicit `Reveal` boundary creates the caller-owned terminal/response value. Constructing `SensitiveValue` from another managed `string` cannot clear that caller-owned immutable input; callers that can hold mutable characters should use the span constructor and clear their source buffer.
