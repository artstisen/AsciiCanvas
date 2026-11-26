---
description: NuGet package versions, central package management, licensing stances and vetted defaults.
---

# Packages and dependencies

## Versions

- **Never write package versions from memory.** Training data is outdated. Use `dotnet add package <Name>` without `--version` to get the latest stable, or check with `dotnet package search <Name>`.
- **`Microsoft.*` / `System.*` / EF Core packages match the target framework major** (10.x for `net10.0`).
- **Stable over preview/RC** unless the project explicitly targets previews.
- **Never downgrade** an existing package unless asked or a compatibility issue requires it.
- After adding/updating: restore, build, and run `dotnet list package --vulnerable --include-transitive`.

## Central package management

- Multi-project solutions use **`Directory.Packages.props`** (`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`). Then `.csproj` `<PackageReference>` has **no `Version`** attribute.
- Shared build settings (`TargetFramework`, `Nullable`, `TreatWarningsAsErrors`, analyzers) go in `Directory.Build.props`.

## Adding dependencies

- **Ask before adding a new third-party dependency** to an existing project. Prefer the BCL / `Microsoft.Extensions.*` when it does the job.
- Libraries: every dependency is a transitive burden on consumers — keep them minimal.

## Packages with commercial licenses

| Package | Status | Free alternative |
|---|---|---|
| MediatR v13+ | Commercial / RPL dual license | `Mediator` (source-gen, MIT), Wolverine, plain handlers |
| AutoMapper v15+ | Commercial / RPL dual license | Manual mapping (default), Mapperly |
| FluentAssertions v8+ | Commercial | Built-in `Assert`, Shouldly, AwesomeAssertions |
| MassTransit v9+ | Commercial | Wolverine |

Don't add these to new projects without license approval; existing licensed/older-version usage can stay.

## Default packages

| Concern | Default |
|---|---|
| Web | ASP.NET Core Minimal APIs (built-in) |
| OpenAPI / UI | `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` (no Swashbuckle) |
| ORM | EF Core (+ Dapper for reporting hot spots) |
| Validation | FluentValidation, or built-in Minimal API validation (.NET 10) |
| Mapping | Manual (`ToResponse()` extension / constructor); Mapperly if volume demands |
| Logging | Serilog or built-in logging; OpenTelemetry for traces/metrics |
| Caching | `HybridCache` (+ Redis L2 when distributed) |
| Resilience | `Microsoft.Extensions.Http.Resilience` (Polly v8) |
| Messaging | Wolverine |
| API versioning | `Asp.Versioning.Http` |
| DI scanning/decorators | Scrutor (only when registrations get large) |
| HTTP clients | `IHttpClientFactory`; Refit for typed clients |
| CLI | `System.CommandLine` 2.x |
| MVVM | CommunityToolkit.Mvvm |
| Testing | xUnit v3, Testcontainers, WireMock.Net, FakeTimeProvider, Verify |
| Containers | `dotnet publish /t:PublishContainer` (SDK built-in) |
| Orchestration (dev) | Aspire |
