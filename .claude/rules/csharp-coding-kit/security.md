---
description: Secrets, input validation, SQL injection, authentication/authorization, transport, CORS and data protection.
---

# Security

## Secrets

- **Never hardcode secrets** (passwords, keys, tokens, connection strings with credentials). `dotnet user-secrets` locally; Key Vault / environment variables / platform secret stores when deployed. Why: once in git history, a secret is effectively public.
- **Never commit** `.env`, `appsettings.*.json` containing real credentials, `*.pfx`, `credentials.json`. Make sure `.gitignore` covers them.
- Prefer managed identity / workload identity over stored credentials where the platform supports it.

```csharp
// DO
var conn = builder.Configuration.GetConnectionString("Default");

// DON'T
var conn = "Server=prod;User Id=sa;Password=hunter2";
```

## Input and injection

- **Validate all external input at trust boundaries** (endpoints, message consumers, file uploads, webhooks).
- **Parameterized SQL only.** EF Core `FromSql($"...")` / `SqlQuery<T>($"...")` interpolation is parameterized; `FromSqlRaw` / `ExecuteSqlRaw` + string concatenation is injection. Same for Dapper/ADO.NET.
- **Never deserialize untrusted data into polymorphic/arbitrary types.** No `BinaryFormatter`; no `TypeNameHandling.All/Auto` in Newtonsoft.Json.
- **File uploads:** enforce size limits, validate content type/extension, generate server-side file names, never use the client's path.
- **Paths:** never `Path.Combine` user input into file system paths without validating it stays under the intended root.
- **Outbound URLs from user input** (webhooks, image fetch) need an allowlist — SSRF.
- **No `Process.Start` with user-controlled arguments.**

## Authentication and authorization

- **Every endpoint states the auth intent explicitly**: `.RequireAuthorization(...)` / `.AllowAnonymous()` or `[Authorize]` / `[AllowAnonymous]`. Consider a fallback policy requiring authenticated users so forgetting is safe.
- **Policy-based authorization** for permissions; check **resource ownership** (user can only read *their* order) in the handler — authentication alone is not authorization.
- **Never roll your own crypto or password hashing.** ASP.NET Identity / `PasswordHasher<T>`; Data Protection API for encrypting data at rest; `RandomNumberGenerator` for tokens (never `Random`).
- JWT: validate issuer, audience, lifetime and signing key; short-lived access tokens.

## Transport and browser

- **HTTPS everywhere:** `UseHttpsRedirection()` + `UseHsts()` outside Development.
- **CORS: explicit origins only in production.** Never `AllowAnyOrigin()` (and never with credentials).

```csharp
builder.Services.AddCors(o => o.AddPolicy("Web", p =>
    p.WithOrigins("https://app.example.com").AllowAnyMethod().AllowAnyHeader()));
```

- Antiforgery for cookie-authenticated form posts.
- Don't return stack traces / exception details outside Development.

## Logging

- No secrets or PII in logs (see `logging-observability.md`).

## Dependencies

- Check `dotnet list package --vulnerable --include-transitive` when adding or updating packages; fix high/critical findings.
