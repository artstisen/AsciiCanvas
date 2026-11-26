---
description: Dependency injection lifetimes, options/configuration pattern, HttpClient factory and resilience.
---

# Dependency injection, configuration and outbound HTTP

## Dependency injection

- **Constructor injection only** (primary constructors). No service locator (`IServiceProvider.GetService` inside business code), no property injection, no static `App.Services`.
- **Lifetimes must not be captive:** a singleton never depends on a scoped/transient service (e.g. `DbContext`). Why: the scoped instance lives forever → stale data, thread-safety bugs, leaks. Enable `ValidateScopes` / `ValidateOnBuild` (on by default in Development).
- **Singletons that need scoped services** (and every `BackgroundService`) create a scope per unit of work via `IServiceScopeFactory.CreateAsyncScope()` with `await using`.
- **Interfaces where they earn their place** — boundaries, multiple implementations, infrastructure the domain must not see. Don't create a one-implementation interface just to mock it.
- **Keyed services** (`AddKeyedScoped<T>("key")` + `[FromKeyedServices]`) for strategy selection instead of hand-written factories.
- Group registrations in `Add<Feature>()` extension methods on `IServiceCollection`; keep `Program.cs` short.

| Service | Lifetime |
|---|---|
| Stateless service / handler | Scoped (default) or Transient |
| `DbContext` | Scoped (`AddDbContext` / `AddDbContextPool`) |
| Config, caches, `TimeProvider`, thread-safe clients | Singleton |
| Message consumers / handlers | Scoped or Transient — never Singleton |

## Configuration

- **Options pattern always.** Bind sections to classes; never read `IConfiguration["X:Y"]` inside services.
- **Validate on startup** so misconfiguration fails at boot, not at the first request:

```csharp
builder.Services.AddOptions<SmtpOptions>()
    .BindConfiguration(SmtpOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

- `IOptions<T>` for static config · `IOptionsSnapshot<T>` for per-request reload · `IOptionsMonitor<T>` in singletons/background services that react to changes.
- Layering: `appsettings.json` → `appsettings.{Environment}.json` → user secrets (dev) → environment variables → vault. Later wins.
- **Secrets never in `appsettings*.json` committed to git** — `dotnet user-secrets` locally, Key Vault / environment variables / secret store in deployed environments.

## Outbound HTTP

- **`IHttpClientFactory` for every `HttpClient`** — typed, named or keyed (`.AddAsKeyed()`) clients. Never `new HttpClient()` per call.
- **Don't capture a typed client in a singleton** — it pins one handler forever and ignores DNS changes. Use a named/keyed client resolved per use, or `SocketsHttpHandler` with `PooledConnectionLifetime`.
- **Resilience is not optional for external calls:** `AddStandardResilienceHandler()` (retry, circuit breaker, timeouts) on every external client. Disable retries for non-idempotent methods unless an idempotency key is used.
- **Polly v8 only** (`ResiliencePipeline`, `Microsoft.Extensions.Http.Resilience`). Never v7 `Policy.Handle<>()` / `PolicyBuilder`.
- **Every external call has a timeout.**
- Cross-cutting concerns (auth tokens, correlation IDs) go in `DelegatingHandler`s registered with `AddHttpMessageHandler`, not in each method.
- Typed API clients: Refit (`AddRefitClient<T>()`) or Kiota-generated clients are fine; hand-written clients are fine too.
