---
description: Architecture selection, dependency direction, feature organization and module boundaries.
---

# Architecture

## Ask first, recommend second

- **Never assume an architecture for a new project or major restructure.** Ask about domain complexity, team size, expected lifetime, compliance and integration needs, then recommend with rationale. Questionnaire: `.claude/csharp-coding-kit/reference/architecture-advisor.md`.
- **In an existing codebase, the existing architecture wins.** Don't introduce a second style.
- **Right-size:** the best architecture is the simplest one that handles the *actual* complexity. CRUD apps don't need DDD.

| Profile | Default |
|---|---|
| CRUD-heavy, API, MVP, small–medium team | **Vertical Slice (VSA)** |
| Medium complexity, long-lived, enforced boundaries | **Clean Architecture** |
| Complex domain, rich invariants, ubiquitous language | **DDD + Clean Architecture** |
| Multiple bounded contexts, team-per-domain | **Modular Monolith** (VSA or CA inside each module) |

When signals conflict: default to simpler; domain complexity beats team/lifetime signals; team familiarity matters; compliance forces stricter boundaries.

## Organization

- **Feature folders over layer folders.** Why: related code lives together; one feature = few files touched.

```
# DO                          # DON'T
Features/                     Controllers/OrdersController.cs
  Orders/                     Services/OrderService.cs
    CreateOrder.cs            Repositories/OrderRepository.cs
    GetOrder.cs               Models/OrderDto.cs
    OrderEndpoints.cs
```

- **One handler = one use case.** No god-services with 20 methods.
- **Features don't reference each other.** Shared code goes in `Common/` (or a shared project), not in another feature.
- **Start with one file per simple feature** (request + handler + response + validator); split only when it grows.

## Dependency direction

- **Dependencies point inward:** Domain → nothing. Application → Domain. Infrastructure → Application (+ Domain). Api/Host → Application (+ Infrastructure for composition only). Never reverse an arrow.
- **Enforce boundaries with project references.** If two modules must not couple, they have no reference to each other — communicate through integration events or a contracts project.
- **Shared kernel = contracts only** (interfaces, DTOs, integration events, primitives). Never business logic. Why: logic in shared projects becomes hidden coupling.
- **Domain has no framework dependencies** (no EF Core attributes, no ASP.NET, no logging) in CA/DDD.

## Data access

- **No generic repository / unit-of-work over EF Core.** `DbContext` already *is* both. Inject it (or a narrow `IAppDbContext` in Clean Architecture).
- **Exception:** DDD aggregate repositories (one per aggregate root, loading/saving the whole aggregate) are legitimate — they protect consistency boundaries, they are not CRUD wrappers.

## In-process messaging

- A mediator is optional. Options: **Mediator** (source-generated, MIT) · **Wolverine** (mediator + messaging) · plain handler classes injected directly.
- **MediatR v13+ is commercially licensed.** Don't add it to new projects without license approval; keep it in existing codebases that are licensed.
- Use pipeline behaviors only for genuine cross-cutting concerns (validation, logging, transactions).

## Evolution

Architecture is not permanent: VSA → add `Common/` domain logic → Clean Architecture when handlers share real domain rules; Monolith → Modular Monolith when bounded contexts appear. Record significant decisions as short ADRs (`docs/adr/NNN-title.md`).
