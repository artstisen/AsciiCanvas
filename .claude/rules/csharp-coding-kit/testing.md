---
description: Testing strategy, structure, naming, fixtures and mocking for .NET (xUnit v3, WebApplicationFactory, Testcontainers).
paths:
  - "**/*Test*/**"
  - "**/*Tests*/**"
  - "**/tests/**"
  - "**/test/**"
  - "**/*Tests.cs"
  - "**/*Test.cs"
---

# Testing

> Follow the project's existing framework (xUnit/NUnit/MSTest) and assertion style. Defaults below are for new test projects.

## Strategy

- **Integration tests first for APIs:** `WebApplicationFactory<Program>` exercising the real HTTP pipeline (routing, binding, validation, DI, serialization, persistence) in one test.
- **Unit tests for pure logic** (domain rules, calculations, parsers) — fast, no infrastructure.
- **Real databases via Testcontainers.** **Never `UseInMemoryDatabase`** — different semantics (no constraints, transactions or SQL translation) hide real bugs. SQLite in-memory is also not a substitute for SQL Server/PostgreSQL.
- **Bug fix = regression test first** that fails before the fix.

## Structure and naming

- **AAA** — Arrange / Act / Assert, separated by blank lines (comments optional).
- **One behavior per test.** Multiple asserts on the same result are fine.
- **Name: `Method_Scenario_ExpectedResult`** — e.g. `GetOrder_OrderDoesNotExist_ReturnsNotFound`.
- Mirror the production folder structure in the test project.
- `[Theory]` + `[InlineData]` / `[MemberData]` for input variations instead of copy-pasted tests.
- Tests are independent and order-agnostic; no shared mutable state between tests.

```csharp
[Fact]
public async Task CreateOrder_ValidRequest_ReturnsCreated()
{
    var client = factory.CreateClient();
    var request = new CreateOrderRequest("SKU-1", Quantity: 2);

    var response = await client.PostAsJsonAsync("/api/orders", request, TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

## Fixtures

- **Share expensive setup** (containers, `WebApplicationFactory`) with `IClassFixture<T>` / `ICollectionFixture<T>` + `IAsyncLifetime`. Never start a container per test.
- Reset database state between tests (Respawn, transactions rolled back, or unique data per test).
- Build test data with small builders/factory methods, not giant shared object graphs.

## Doubles

- **Don't mock what you own.** Use the real implementation or a simple fake. Why: mocks of your own interfaces couple tests to implementation and break on refactors.
- **Mock/stub at boundaries you don't control:** external HTTP (WireMock.Net or a stub `HttpMessageHandler`), time (`FakeTimeProvider`), email/SMS gateways.
- Prefer NSubstitute for the mocks you do need. **Moq** versions 4.20.0–4.20.1 shipped SponsorLink telemetry — check before adding.

## Assertions

- Default: built-in `Assert`. Fluent alternatives: Shouldly or AwesomeAssertions.
- **FluentAssertions v8+ is commercially licensed** — don't add to commercial projects without approval (v7 is still Apache 2.0).
- Snapshot testing (Verify) for large/complex responses.

## Test behavior, not implementation

- Assert observable outcomes — HTTP response, persisted state, published message — not which private methods were called.
- Don't make members `public` or add `InternalsVisibleTo` hooks just for tests unless there is no reasonable alternative.

## Packages for new projects

`xunit.v3`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.<Engine>`, `Microsoft.Extensions.TimeProvider.Testing`, optionally `WireMock.Net`, `Verify.XunitV3`, `Respawn`.
