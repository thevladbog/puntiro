# Repository Foundation Validation

## Local automated

- Documentation contracts: pass
- Dependency policy: pass
- All npm dependency vulnerability audit at high severity and transitive NuGet audit: pass
- Repository contracts: pass
- Admin shell typecheck/build: pass
- Kiosk shell typecheck/build: pass
- .NET solution plus protected-project builds with outputs and intermediates isolated in unique temporary artifacts roots on development host: pass
- Protected producer path, assembly identity, exact `TargetPath`, and effective friend-assembly metadata binding for those Release outputs: pass

## CI

- Windows compile: pending CI until the workflow runs

## Manual

- WPF runtime on Windows: not run
- Windows Service installation: not run
- Touch and gloves: not run
- Screen reader: not run
- Physical printers: not run

Automated evidence does not upgrade any manual status.
