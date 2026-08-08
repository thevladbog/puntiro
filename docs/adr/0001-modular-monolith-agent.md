# ADR 0001: Modular Monolith and Windows Agent

- Status: Accepted
- Date: 2026-08-08

## Context

Puntiro must retain one reviewable product boundary while supporting shipment-label workflows that continue through short network outages and record physical printer attempts safely. The cloud owns canonical shipment intent and durable audit, while the Windows kiosk must use local state and communicate with printers without allowing the React UI direct device access.

## Decision

Build the cloud as an ASP.NET Core modular monolith. Build a separate .NET Windows Agent that owns local operational state and printer communication. Host the React kiosk in a WPF/WebView2 shell and communicate with the Agent through the defined local boundary. Store canonical cloud data in PostgreSQL and use SQLite as the Agent's local projection and durable record for offline work.

## Consequences

- Cloud modules can evolve within one deployable host while retaining explicit ownership boundaries.
- Agent failures and offline operation are isolated from the cloud API and React UI.
- The kiosk uses the shared React design system without receiving SQLite or printer credentials.
- PostgreSQL remains the source of truth; SQLite data must be synchronized and reconciled through explicit contracts.
- The repository must maintain OpenAPI, IPC contracts, migrations, and runbooks as the boundaries evolve.

## Rejected Alternatives

- Splitting the cloud into microservices now would add deployment and operational complexity before MVP needs it.
- Putting SQLite access or printer I/O in React would violate local durability and device-security boundaries.
- A browser-only kiosk cannot provide the required Windows-native local Agent integration.
- Using cloud PostgreSQL directly from the kiosk would prevent reliable offline operation and blur ownership of physical attempts.
