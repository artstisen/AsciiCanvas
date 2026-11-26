---
description: Async correctness, time, resource management, caching and hot-path performance rules.
---

# Async and performance

## Async

- **Propagate `CancellationToken` through the whole call chain** — accept it as the last parameter and pass it to every async call. Why: dropped tokens keep cancelled requests burning resources.

```csharp
// DO
public Task<Order?> GetOrderAsync(Guid id, CancellationToken ct) =>
    db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

// DON'T — token accepted but silently ignored
public Task<Order?> GetOrderAsync(Guid id, CancellationToken ct) =>
    db.Orders.FirstOrDefaultAsync(o => o.Id == id);
```

- **Async all the way. Never `.Result`, `.Wait()` or `.GetAwaiter().GetResult()`.** Why: thread-pool starvation and deadlocks. Only acceptable in `Main`/top-level statements when there is truly no async entry point.
- **Never `async void`** — except event handlers that require it. Why: exceptions crash the process and the call cannot be awaited.
- **No unobserved fire-and-forget** (`_ = DoWorkAsync();`). Await it, or hand it to a `BackgroundService` / `Channel<T>` queue.
- **`Task.Delay(ct)`, never `Thread.Sleep`** in async code.
- **Don't wrap async I/O in `Task.Run`.** Use `Task.Run` only to offload CPU-bound work from a UI thread.
- Run independent awaits concurrently with `Task.WhenAll`; bound parallelism with `Parallel.ForEachAsync` + `MaxDegreeOfParallelism`.
- **`ValueTask<T>`** only on hot paths that usually complete synchronously; `Task<T>` everywhere else. Never await a `ValueTask` twice.

## Time

- **Inject `TimeProvider`; never `DateTime.Now` / `DateTime.UtcNow` / `DateTimeOffset.Now` in logic.** Why: untestable static dependency and time-zone bugs. Use `FakeTimeProvider` in tests.
- Store and transmit UTC (`DateTimeOffset` or UTC `DateTime`); convert to local time only for display.

```csharp
public sealed class AuditService(TimeProvider clock)
{
    public DateTimeOffset Now => clock.GetUtcNow();
}
```

## Resources

- **`IHttpClientFactory`, never `new HttpClient()` per call.** Why: socket exhaustion and stale DNS.
- **`using` / `await using`** for every `IDisposable` / `IAsyncDisposable` you create, including `IServiceScope`.
- **`ArrayPool<T>` / `MemoryPool<T>` / `Span<T>`** for buffer-heavy or parsing hot paths to avoid GC pressure.
- **`StringBuilder`** (or `string.Create`) for strings built in loops.

## Caching

- **`HybridCache` is the default** over raw `IMemoryCache` / `IDistributedCache`. Why: stampede protection, L1+L2, tag invalidation built in.
- **Every cache entry has an explicit expiration.** No unbounded caches.
- Cache reads; invalidate on writes. Use output caching (`.CacheOutput()`) for whole public HTTP responses.

```csharp
var order = await cache.GetOrCreateAsync(
    $"order:{id}",
    async token => await db.Orders.FindAsync([id], token),
    cancellationToken: ct);
```

## Hot paths

- **Measure before optimizing** (BenchmarkDotNet, `dotnet-counters`, profiler). Don't trade readability for unmeasured gains.
- EF Core hot reads: projections + `AsNoTracking()`; compiled queries (`EF.CompileAsyncQuery`) when profiling shows translation cost.
- Avoid LINQ in tight inner loops on hot paths; avoid boxing and closures there.
- Prefer `FrozenDictionary` / `FrozenSet` for read-only lookups built once at startup.
- Use `[LoggerMessage]` source-generated logging on hot paths.
