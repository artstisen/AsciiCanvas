# Anti-patterns (read on demand)

Code Claude must not generate, with the correct alternative. Use this when reviewing code or when unsure about a pattern. Each entry: **why it's wrong → what to do instead.**

## async void

Exceptions crash the process or vanish; callers can't await it.

```csharp
// BAD
public async void ProcessOrder(Order order) => await db.SaveChangesAsync();
// GOOD
public async Task ProcessOrderAsync(Order order, CancellationToken ct) => await db.SaveChangesAsync(ct);
```

## Sync over async

Deadlocks (UI/legacy contexts) and thread-pool starvation.

```csharp
// BAD
var order = service.GetOrderAsync(id).Result;
service.GetOrderAsync(id).Wait();
var order2 = service.GetOrderAsync(id).GetAwaiter().GetResult();
// GOOD
var order = await service.GetOrderAsync(id, ct);
```

## Missing CancellationToken

Work continues after the client disconnects.

```csharp
// BAD
public Task<List<Order>> GetOrdersAsync() => db.Orders.ToListAsync();
// GOOD
public Task<List<Order>> GetOrdersAsync(CancellationToken ct) => db.Orders.ToListAsync(ct);
```

## new HttpClient()

Socket exhaustion, stale DNS.

```csharp
// BAD
using var client = new HttpClient();
// GOOD
builder.Services.AddHttpClient<PaymentsClient>(c => c.BaseAddress = new("https://payments.example.com"))
    .AddStandardResilienceHandler();
public sealed class PaymentsClient(HttpClient http) { }
```

## DateTime.Now / UtcNow

Untestable, time-zone bugs.

```csharp
// BAD
CreatedAt = DateTime.Now;
// GOOD
public sealed class OrderFactory(TimeProvider clock) { public DateTimeOffset Now() => clock.GetUtcNow(); }
// test: new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
```

## Exceptions for expected outcomes

Expensive, hides control flow.

```csharp
// BAD
var order = await db.Orders.FindAsync([id], ct) ?? throw new NotFoundException();
// GOOD
var order = await db.Orders.FindAsync([id], ct);
if (order is null) return Error.NotFound("Order", id);
```

## Catching System.Exception mid-stack

Swallows bugs.

```csharp
// BAD
try { await ProcessAsync(order, ct); }
catch (Exception ex) { logger.LogError(ex, "Something went wrong"); return Results.Problem(); }
// GOOD — catch what you can handle; the global handler gets the rest
try { await ProcessAsync(order, ct); }
catch (PaymentDeclinedException ex)
{
    logger.LogWarning(ex, "Payment declined for {OrderId}", order.Id);
    return Error.Validation("Payment.Declined", "Payment was declined.");
}
```

## throw ex;

Destroys the original stack trace.

```csharp
// BAD
catch (IOException ex) { Cleanup(); throw ex; }
// GOOD
catch (IOException) { Cleanup(); throw; }
```

## Service locator

Hidden dependencies, no compile-time checking.

```csharp
// BAD
public sealed class OrderService(IServiceProvider sp)
{
    public Task RunAsync() => sp.GetRequiredService<AppDbContext>().SaveChangesAsync();
}
// GOOD
public sealed class OrderService(AppDbContext db, ILogger<OrderService> logger) { }
```

## Captive dependency (scoped in singleton)

`DbContext` lives forever: stale data, concurrency exceptions, leaks.

```csharp
// BAD
builder.Services.AddSingleton<OrderCache>(); // OrderCache(AppDbContext db)
// GOOD
public sealed class OrderCache(IServiceScopeFactory scopes)
{
    public async Task<Order?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.FindAsync([id], ct);
    }
}
```

## Undisposed scope

```csharp
// BAD
var scope = provider.CreateScope();
// GOOD
await using var scope = provider.CreateAsyncScope();
```

## Interpolated log messages

Loses structure; allocates even when disabled.

```csharp
// BAD
logger.LogInformation($"Processing order {orderId}");
// GOOD
logger.LogInformation("Processing order {OrderId}", orderId);
```

## Tracked or over-fetching EF reads

```csharp
// BAD
var orders = await db.Orders.Include(o => o.Items).ToListAsync(ct);
// GOOD
var orders = await db.Orders
    .Select(o => new OrderSummary(o.Id, o.Total, o.Status))
    .ToListAsync(ct);
```

## N+1 queries

```csharp
// BAD
foreach (var order in orders)
    order.Customer = await db.Customers.FindAsync([order.CustomerId], ct);
// GOOD — one query with a join/projection
var rows = await db.Orders.Select(o => new { o.Id, CustomerName = o.Customer.Name }).ToListAsync(ct);
```

## Repository over EF Core

Duplicates `DbContext`, hides EF features (projections, `ExecuteUpdateAsync`, tracking control).

```csharp
// BAD
public interface IRepository<T> { Task<T?> GetByIdAsync(Guid id); Task AddAsync(T entity); }
// GOOD
public sealed class GetOrderHandler(AppDbContext db) { /* query db.Orders directly */ }
```

## In-memory database in tests

```csharp
// BAD
options.UseInMemoryDatabase("Test");
// GOOD — Testcontainers with the real engine (see snippets.md)
```

## SQL string concatenation

```csharp
// BAD
db.Database.ExecuteSqlRaw("DELETE FROM Orders WHERE Id = '" + id + "'");
// GOOD
await db.Database.ExecuteSqlAsync($"DELETE FROM Orders WHERE Id = {id}", ct);
```

## Endpoints in Program.cs, Results instead of TypedResults, entities in responses

```csharp
// BAD (Program.cs)
app.MapGet("/orders/{id}", async (Guid id, AppDbContext db) => Results.Ok(await db.Orders.FindAsync(id)));
// GOOD — OrderEndpoints : IEndpointGroup, TypedResults, DTO projection, CancellationToken, explicit auth
group.MapGet("/{id:guid}", GetOrder).WithName("GetOrder");
```

## Implicit auth

```csharp
// BAD — inherits whatever the global default is
app.MapGet("/admin/stats", GetStats);
// GOOD
group.MapGet("/stats", GetStats).RequireAuthorization("AdminOnly");
health.MapGet("/live", () => TypedResults.Ok()).AllowAnonymous();
```

## Wildcard CORS

```csharp
// BAD
p.AllowAnyOrigin();
// GOOD
p.WithOrigins("https://app.example.com");
```

## Reading IConfiguration in services

```csharp
// BAD
public sealed class Mailer(IConfiguration config) { string Host => config["Smtp:Host"]!; }
// GOOD
public sealed class Mailer(IOptions<SmtpOptions> options) { string Host => options.Value.Host; }
```

## Fire-and-forget

```csharp
// BAD
_ = SendWelcomeEmailAsync(user);
// GOOD — await it, or enqueue to a Channel<T>/background worker/outbox
await emailQueue.Writer.WriteAsync(new WelcomeEmail(user.Id), ct);
```

## Thread.Sleep and while (true) in workers

```csharp
// BAD
while (true) { DoWork(); Thread.Sleep(5000); }
// GOOD
using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
while (await timer.WaitForNextTickAsync(stoppingToken)) { await DoWorkAsync(stoppingToken); }
```

## Old libraries in new code

| Don't | Use |
|---|---|
| Swashbuckle | `Microsoft.AspNetCore.OpenApi` + Scalar |
| Polly v7 `Policy.Handle<>()` | Polly v8 `ResiliencePipeline` / `AddStandardResilienceHandler()` |
| `IMemoryCache` + `IDistributedCache` hand-rolled | `HybridCache` |
| Newtonsoft.Json (new code) | `System.Text.Json` (source-generated for AOT) |
| MediatR / AutoMapper / FluentAssertions v8 without a license | Mediator / manual mapping or Mapperly / Assert, Shouldly |
| `BinaryFormatter` | Never — removed and insecure |
| `WebClient`, `HttpWebRequest` | `IHttpClientFactory` |
