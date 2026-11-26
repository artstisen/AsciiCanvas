---
description: Modern C# conventions, naming, type design and file organization.
---

# C# coding style

> `.editorconfig` and analyzer settings in the repo override anything here.

## Files

- **File-scoped namespaces** always. Why: saves a level of indentation for zero cost.
- **One top-level type per file**, file name = type name. Exception: a small vertical slice (request + handler + response) may share one feature file.
- **Member order:** constants, fields, constructors, properties, public methods, private methods.
- **No `#region`.** Why: regions hide code size problems instead of fixing them.

## Type design

| Need | Use |
|---|---|
| DTO / API contract / message | `sealed record` |
| Small value object (2–3 fields) | `readonly record struct` |
| Service with dependencies | `sealed class` with a primary constructor |
| Must-set properties | `required` (+ `init`) |
| Property with validation / lazy logic | `field` keyword (C# 14) |
| Multi-line SQL / JSON / text | raw string literal `"""` |
| Add members to external types | extension members (C# 14) / extension methods |

- **`sealed` by default** on classes not designed for inheritance. Why: communicates intent and lets the JIT devirtualize.
- **`internal` by default**, `public` only when another assembly needs it. Why: small public surface = freedom to refactor.
- **Immutability by default** — `record`, `readonly`, `init`. Make illegal states unrepresentable.
- **Primary constructors for DI.** Don't write `_field = field` ceremony.

```csharp
// DO
public sealed class OrderService(AppDbContext db, TimeProvider clock) { }

// DON'T
public class OrderService
{
    private readonly AppDbContext _db;
    public OrderService(AppDbContext db) { _db = db; }
}
```

- **Public APIs return read-only abstractions** (`IReadOnlyList<T>`, `IReadOnlyDictionary<K,V>`), not `List<T>`.

## Expressions

- **Collection expressions:** `List<int> ids = [1, 2, 3];`, spread with `[..a, ..b]`, empty with `[]`.
- **Pattern matching / switch expressions** over `if`-`else` chains. Keep patterns shallow — readability beats cleverness.
- **`is null` / `is not null`**, not `== null`.
- **Guard clauses** at boundaries: `ArgumentNullException.ThrowIfNull(x)`, `ArgumentException.ThrowIfNullOrWhiteSpace(s)`.
- **`var`** when the type is obvious from the right-hand side; explicit type when it is not.

## Naming

- PascalCase: types, methods, properties, constants, namespaces. camelCase: locals and parameters.
- Explicit private fields: `_camelCase`. Primary-constructor parameters: plain `camelCase` (no underscore).
- **`Async` suffix** on methods returning `Task`/`ValueTask` (except framework overrides such as `ExecuteAsync` already named, or endpoint handlers by project convention).
- Interfaces `I`-prefixed; no Hungarian notation; no abbreviations except well-known ones (`Id`, `Http`, `Dto`).

## Comments

- Comment **why**, never **what**. Delete commented-out code.
- XML docs (`///`) on public APIs of libraries; not required on internal app code.
