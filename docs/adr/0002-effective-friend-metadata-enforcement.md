# ADR 0002: Effective Friend Metadata Enforcement

- Status: Accepted
- Date: 2026-08-08

## Context

Protected production assemblies expose internals only to the unit- and integration-test assemblies. A source-provenance rule previously required the declarations to originate in one exact `Properties/AssemblyInfo.cs` file and reserved the attribute token everywhere else. That syntactic rule was stricter than the effective security boundary, difficult to make complete across arbitrary MSBuild imports and alternate C# representations, and rejected equivalent builds whose final metadata was unchanged.

The metadata check also must not accept a stale DLL from normal `bin` or `obj` state, a symlink/reparse point to such a DLL, or a same-named DLL produced by an unprotected project. A successful incremental build or a filename match in a shared output tree is insufficient evidence that the inspected assembly came from the requested project in the current invocation. Static `.csproj` parsing likewise cannot see `ProjectReference` items injected through imported or inherited MSBuild files.

## Decision

Enforce only the final effective friend-assembly allowlist for the protected assembly identities. Each must contain exactly `Puntiro.UnitTests` and `Puntiro.IntegrationTests`, once each, with no additional friend metadata. The attribute constructor must be the exact `.ctor(string)` member reference scoped to a trusted .NET 10 BCL assembly version and Microsoft public key token; a local or forged same-named type, scope, member, or signature does not count. The source location and mechanism that produce valid attributes are not policy inputs. The existing `Properties/AssemblyInfo.cs` layout remains a preferred review convention, not a security invariant.

The definitive check first evaluates the effective `ProjectReference` items for every managed project and requires the exact approved acyclic graph. It builds the complete solution under a unique per-run artifacts root, then builds the verifier and every protected project separately under distinct per-project `--artifacts-path` roots. MSBuild's evaluated producer path, assembly name, output path, intermediate path, artifacts path, and exact target path are bound before and after each project build. Each isolated tree and evidence path must contain no symlink/junction/reparse component and must resolve inside the same unique root. The metadata verifier receives expected-identity/path pairs, verifies the assembly definition identity, and compares the full `InternalsVisibleTo` multiset without executing protected code.

## Consequences

- Equivalent declarations from relocated source or imported MSBuild configuration are accepted when final metadata is exact.
- Comments, strings, aliases, escapes, generated source, and build-file tokens are not statically reserved.
- The source-and-graph foundation check cannot establish friend access; the definitive .NET gate is mandatory.
- Imported or inherited project references cannot bypass the exact effective graph, while imports remain free to produce equivalent approved friend metadata.
- Normal `bin` and `obj` state, linked artifacts, and same-named outputs from other projects cannot substitute for the exact isolated regular-file `TargetPath`.
- Missing targets, graph drift/cycles, links/reparse points, escaped or changing plans, duplicate inputs, forged BCL references, identity mismatches, and missing, duplicate, or unapproved friend names fail closed.
- Reviewers should still prefer the conventional source block when no build-generation requirement justifies another form.

## Rejected Alternatives

- Keeping the exact-file provenance invariant would require treating the full imported MSBuild graph and every compiler input representation as a trusted allowlist. It adds syntactic restrictions without strengthening the selected final-metadata boundary.
- Verifying a DLL found by filename beneath a shared or fixed output tree cannot prove which protected project produced it or whether it belongs to the current invocation.
- Loading protected assemblies for reflection would execute assembly-loading behavior; BCL metadata inspection provides the required evidence without executing production code.
