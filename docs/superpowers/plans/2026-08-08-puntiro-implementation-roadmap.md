# Puntiro Implementation Roadmap

Дата: 2026-08-08  
Источник: `docs/superpowers/specs/2026-08-08-puntiro-technical-architecture-design.md`.

## Правило декомпозиции

Техническая архитектура охватывает несколько независимо проверяемых подсистем. Они реализуются последовательными implementation plans. Каждый план заканчивается работающим вертикальным или инфраструктурным срезом, собственным review и отдельным commit history.

## Последовательность

| Этап | План | Результат | Зависит от |
| --- | --- | --- | --- |
| 0 | `2026-08-08-puntiro-repository-foundation.md` | AGENTS.md, documentation map, dependency policy, .NET/React product skeletons, базовый CI | утверждённая архитектура |
| 1 | Cloud identity and tenancy | Организация, owner login, Argon2id, TOTP, sessions, integration tokens | 0 |
| 2 | Shipment and template domain | Integration API, tasks, revisions, cancellation, immutable ZPL/TSPL template versions | 1 |
| 3 | Fleet and device sync | Kiosk binding, credentials, printers, change feed, cursor, heartbeat, preflight | 2 |
| 4 | Windows Agent foundation | SQLCipher, DPAPI, local projections, outbox, offline grace, Named Pipe IPC | 3 |
| 5 | Printing pipeline | Template renderer, label sets, UUIDv7 places, Spooler/TCP transports, recovery state machine | 4 |
| 6 | Product applications | React admin and real kiosk flow connected to Cloud/Agent contracts | 5 |
| 7 | Timeweb production | Compose, DBaaS, S3, migrations, backups, telemetry, SBOM и signed MSI pipeline | 6 |
| 8 | Physical acceptance | Windows scaling, touch/gloves, Zebra/TSC/Bixolon, USB queue/Ethernet evidence | 7 |

## Сквозные требования каждого плана

- Перед выбором или обновлением библиотеки используется Context7, затем official security advisories.
- NuGet/npm версии фиксируются точно; lockfile и package audit входят в CI.
- Поведение и полезная документация меняются в одном change set.
- Root или scoped `AGENTS.md` обновляется при изменении команд, границ или gates.
- Каждый production step начинается с RED и заканчивается GREEN-проверкой.
- Browser/CI proof не засчитывается как Windows/hardware acceptance.
- Новые repository skills создаются только по критериям архитектурной спецификации.

## Gate между этапами

Следующий план детализируется перед началом соответствующего этапа с учётом фактических публичных интерфейсов предыдущего этапа. Это предотвращает копирование предполагаемых signatures в несколько планов и позволяет сохранить type consistency.
