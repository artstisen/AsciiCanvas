---
description: Structured logging, log levels, PII, tracing, metrics and health checks.
---

# Logging and observability

## Structured logging

- **Message templates, never interpolation or concatenation.** Why: interpolation destroys structured properties and allocates even when the level is disabled.

```csharp
// DO
logger.LogInformation("Processing order {OrderId} for {CustomerId}", orderId, customerId);

// DON'T
logger.LogInformation($"Processing order {orderId} for {customerId}");
```

- **PascalCase placeholder names**, stable across the codebase (`{OrderId}` everywhere, not `{id}` here and `{orderId}` there).
- **Log the exception object** as the first argument: `logger.LogError(ex, "Payment failed for {OrderId}", id)` — never just `ex.Message`.
- **Hot paths / high-volume logs:** `[LoggerMessage]` source-generated methods.
- Inject `ILogger<T>`; don't create loggers manually or use static loggers in app code.
- Log once, where the error is handled — not at every layer it passes through.

## Levels

| Level | Use for |
|---|---|
| Trace / Debug | Diagnostic detail, off in production |
| Information | Significant business/lifecycle events (order placed, service started) |
| Warning | Unexpected but handled (retry, fallback, validation spike) |
| Error | Operation failed, needs attention |
| Critical | App/host is going down or data integrity is at risk |

Production default: `Warning` for `Microsoft.*` / `System.*`, `Information` for the app.

## PII and secrets

- **Never log secrets, tokens, passwords, connection strings or full request bodies.**
- **No PII (emails, names, IPs, national IDs) at Information or above.** Why: production log stores are broadly accessible — PII there is a compliance liability. Use IDs instead of personal data.

## Setup

- **Serilog** is the common choice (`AddSerilog()` with two-stage bootstrap so startup failures are logged); built-in `Microsoft.Extensions.Logging` is fine for small tools. Follow the project.
- Configure sinks/levels in `appsettings`, not hard-coded.
- **OpenTelemetry** for traces and metrics (`OpenTelemetry.Extensions.Hosting` + ASP.NET Core/HttpClient instrumentation + OTLP exporter). Custom metrics via `IMeterFactory`; custom spans via `ActivitySource`.
- **Correlation:** every request has a trace/correlation ID that flows into logs and outbound calls (`Activity.Current` / W3C trace context handles this with OTel).
- Aspire projects: use the generated ServiceDefaults rather than wiring telemetry by hand.

## Health checks

- Separate **liveness** (`/health/live` — process is up, no dependency checks) and **readiness** (`/health/ready` — DB, cache, broker reachable).
- Health endpoints don't expose internal details publicly.
