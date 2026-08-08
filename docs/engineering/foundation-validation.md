# Repository Foundation Validation

## Local automated

- Documentation contracts: pass
- Dependency policy: pass
- All npm dependency vulnerability audit at high severity and transitive NuGet audit: pass
- Repository contracts: pass
- Admin shell typecheck/build: pass
- Kiosk shell typecheck/build: pass
- .NET solution build into a unique empty temporary output root on development host: pass
- Evaluated MSBuild provenance and effective friend-assembly metadata for those exact Release outputs: pass

## CI

- Windows compile: pending CI until the workflow runs

## Manual

- WPF runtime on Windows: not run
- Windows Service installation: not run
- Touch and gloves: not run
- Screen reader: not run
- Physical printers: not run

Automated evidence does not upgrade any manual status.
