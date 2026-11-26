# Choosing an architecture (read on demand)

Use for new projects or major restructures. Ask only the questions that apply, then recommend **one** architecture with reasons, and record the decision as a short ADR.

## Questionnaire

| # | Category | Question | Low signal | High signal |
|---|---|---|---|---|
| 1 | Domain | How many business entities? | < 10 | 20+ with relationships |
| 2 | Domain | Do rules span multiple entities? | Per-entity CRUD | Invariants across entity groups |
| 3 | Domain | Multi-step workflows? | Request → response | Sagas, approvals, state machines |
| 4 | Domain | Specialized vocabulary? | create/update | underwrite, adjudicate… |
| 5 | Team | Team size? | 1–3 | 8+ / multiple teams |
| 6 | Team | Do teams own separate areas? | One team | Team per domain |
| 7 | Team | Experience with patterns? | Junior/mixed | Senior |
| 8 | Lifetime | Expected lifetime? | < 2 yrs / MVP | 5+ yrs |
| 9 | Scale | Load? | < 100 RPS | 1000+ RPS, variable |
| 10 | Scale | Independent scaling by area? | Uniform | Hot spots |
| 11 | Compliance | Audit/regulatory needs? | Basic logging | SOX/HIPAA/PCI/GDPR audit trails |
| 12 | Compliance | Separate security boundaries? | One | Multi-tenant / data isolation |
| 13 | Codebase | Greenfield or brownfield? | Greenfield | Legacy migration |
| 14 | Codebase | Established team conventions? | None | Strong |
| 15 | Integration | External systems? | 0–2 simple | 5+ complex contracts |
| 16 | Integration | Async/event-driven needs? | Sync only | Pub/sub, eventual consistency |

## Decision matrix

| Profile | Recommend | Why |
|---|---|---|
| Low complexity, small team, short lifetime | **Vertical Slice** | Minimal ceremony, fastest delivery |
| Low–medium complexity, API-focused | **Vertical Slice** | Feature cohesion, natural fit for Minimal APIs |
| Medium complexity, long lifetime | **Clean Architecture** | Boundaries enforced by project references, testable core |
| High complexity, rich invariants, ubiquitous language | **DDD + Clean Architecture** | Aggregates protect invariants, value objects, domain events |
| Several bounded contexts, team-per-domain | **Modular Monolith** | Module isolation, own data per module, path to services |
| Brownfield layered (N-tier) codebase | **Clean Architecture** | Familiar layers, fixed dependency direction |

**Conflicting signals:** default to simpler · domain complexity wins over team size/lifetime · team familiarity matters · compliance pushes toward stricter boundaries.

Microservices are not on this list on purpose: start with a (modular) monolith and split only when independent deployment/scaling is a proven need.

## Structures

```
# Vertical Slice
src/MyApp.Api/
  Features/Orders/CreateOrder.cs     # request + validator + handler + response
  Features/Orders/OrderEndpoints.cs
  Common/Persistence/AppDbContext.cs
  Common/Behaviors/…

# Clean Architecture (+ DDD)
src/MyApp.Domain/          # entities/aggregates, value objects, domain events — no dependencies
src/MyApp.Application/     # use cases, DTOs, validation, IAppDbContext — refs Domain
src/MyApp.Infrastructure/  # EF Core, external services — refs Application
src/MyApp.Api/             # thin endpoints, composition root

# Modular Monolith
src/MyApp.Host/            # composes modules
src/Modules/Orders/        # own features, own DbContext/schema, own internal architecture
src/Modules/Billing/
src/Shared/Contracts/      # integration events and public module contracts only
```

## DDD rules (when DDD is chosen)

- Aggregates are consistency boundaries; keep them small (root + 0–3 children); modify only through the root.
- One repository per aggregate root (loads/saves the whole aggregate). Plain `DbContext`/`IAppDbContext` for read models and queries.
- Value objects (records) instead of primitives for concepts with rules: `Money`, `Email`, `OrderNumber`.
- Domain events inside a bounded context (same transaction); integration events across contexts (via outbox + bus).
- Strongly-typed IDs for aggregate roots.
- Don't apply DDD to CRUD/settings/audit-log areas — plain entities are fine there.

## Changing architecture later

- VSA → Clean Architecture: when handlers repeatedly need shared domain rules that don't belong in `Common/`.
- Clean Architecture → DDD: when invariants span entity groups and the business vocabulary gets richer.
- Monolith → Modular Monolith: when distinct bounded contexts / team ownership emerge.
- Modular Monolith → services: only for proven independent scaling/deployment needs.

## ADR template

```markdown
# ADR-NNN: <Title>
## Status
Proposed | Accepted | Superseded by ADR-XXX
## Context
<forces, constraints, questionnaire answers>
## Decision
<what we chose>
## Consequences
<positive / negative / follow-ups>
```
