---
description: Rules for reusable class libraries and NuGet packages.
---

# Class libraries and NuGet packages

## Public API

- **`internal` by default.** Only deliberate API is `public`; don't leak implementation or infrastructure types.
- **Semantic versioning is a contract.** Removing/renaming public members, changing signatures, or adding members to a public interface = breaking change (major bump). Consider `Microsoft.CodeAnalysis.PublicApiAnalyzers` to track the surface.
- **Return immutable/read-only types** (`IReadOnlyList<T>`, `IReadOnlyDictionary<K,V>`, `ImmutableArray<T>`), accept the most general input type that works (`IEnumerable<T>`, `ReadOnlySpan<T>`).
- **XML docs on every public member**; `<GenerateDocumentationFile>true</GenerateDocumentationFile>`.
- **Nullable annotations are part of the API** — keep them accurate.
- **`[Obsolete]` with a message and replacement** for one release before removing anything.

## Dependencies

- **Minimal dependencies** — each one is forced on every consumer. Prefer BCL types.
- **Depend on abstractions** (`Microsoft.Extensions.*.Abstractions`), not ASP.NET Core, unless the library is ASP.NET-specific.
- Offer an `IServiceCollection` extension (`AddMyLibrary(options => ...)`) for DI integration, using the options pattern.

## Behavior

- **Thread-safe; no static mutable state.**
- **`ConfigureAwait(false)`** on awaits in general-purpose libraries (no app context to resume on).
- **Accept `CancellationToken`** on every async public method; accept `TimeProvider`, `ILogger<T>` and `HttpClient`/`IHttpClientFactory` from the consumer rather than creating them.
- **No `Console.Write`, no global exception handling, no reading environment/config directly** — that's the host's job.
- **Don't throw from constructors for normal flow**; use factory methods or Result types for operations that can fail.
- Catch only specific exceptions; let the consumer's handler deal with the rest.

## Packaging

- Multi-target only when needed (e.g. `net8.0;net10.0`); test every target.
- Package metadata in the csproj: `PackageId`, `Description`, `Authors`, `PackageLicenseExpression`, `RepositoryUrl`, `PackageReadmeFile`. Enable SourceLink and deterministic builds; publish symbols (`.snupkg`).
- `IsTrimmable` / `IsAotCompatible` when the library supports it — and keep it that way (no unguarded reflection).
