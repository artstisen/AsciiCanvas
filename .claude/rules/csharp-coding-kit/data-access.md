---
description: EF Core usage, query patterns, migrations and raw SQL safety.
---

# Data Access (EF Core)

## Defaults

- **EF Core is the default ORM.** Dapper / raw SQL for complex reporting or measured hot paths where EF's SQL is poor.
- **Use `DbContext` directly** in handlers/services (scoped lifetime). No generic repository wrapper (see `architecture.md`).
- **Every query takes the `CancellationToken`.**

## Queries

- **Project to DTOs with `.Select()`** for reads. Why: fetches only needed columns, no tracking, no N+1 lazy loads.
- **`AsNoTracking()`** for read-only entity queries.
- **Never return entities from APIs** — map to response DTOs.
- **Avoid N+1:** no queries inside loops; use projections or `Include`. For multiple collection `Include`s, consider `AsSplitQuery()` to avoid cartesian explosion.
- **Lazy loading stays off.**
- **Paginate unbounded lists** (`Skip/Take` with a stable `OrderBy`, or keyset pagination for large tables).
- **Filter in the database, not in memory** — no `ToListAsync()` followed by `Where`.

```csharp
// DO
var orders = await db.Orders
    .Where(o => o.CustomerId == customerId)
    .OrderByDescending(o => o.CreatedAt)
    .Select(o => new OrderSummary(o.Id, o.Total, o.Status))
    .Take(50)
    .ToListAsync(ct);

// DON'T — tracked, over-fetched, filtered in memory
var orders = (await db.Orders.Include(o => o.Items).ToListAsync())
    .Where(o => o.CustomerId == customerId);
```

## Writes

- **One `SaveChangesAsync` per unit of work** (per request/command), not per entity.
- **Bulk updates/deletes:** `ExecuteUpdateAsync` / `ExecuteDeleteAsync` instead of load-modify-save loops.
- **Concurrency:** use a concurrency token (`[Timestamp]` / `IsRowVersion()` or `xmin` on PostgreSQL) where lost updates matter; handle `DbUpdateConcurrencyException` → Conflict.
- **Cross-cutting persistence concerns** (audit fields, soft delete, domain-event dispatch) belong in `SaveChangesInterceptor`s and global query filters, not copy-pasted into handlers.

## Model configuration

- **Fluent configuration in `IEntityTypeConfiguration<T>` classes**, applied via `ApplyConfigurationsFromAssembly`. Keep entities free of persistence attributes in CA/DDD.
- Set explicit max lengths, precision for `decimal`, and required/optional for every string column.
- Strongly-typed IDs via value converters for aggregate roots that cross boundaries.

## Migrations

- **Migrations are code** — review the generated migration before committing.
- **Never auto-migrate on startup in production** (`Database.Migrate()`). Use idempotent scripts (`dotnet ef migrations script --idempotent`) or migration bundles in the deployment pipeline.
- Never edit a migration that has been applied to a shared environment; add a new one.

## Raw SQL

- **Always parameterized.** `FromSql($"... {id}")` / `SqlQuery<T>($"...")` / `ExecuteSqlAsync($"...")` interpolation is parameterized and safe. `FromSqlRaw` / `ExecuteSqlRaw` with string concatenation is **SQL injection**.
- Dapper: always pass parameters (`new { id }`), never concatenate.

## Testing

- **No `UseInMemoryDatabase`** for tests — use Testcontainers with the real engine (see `testing.md`).
