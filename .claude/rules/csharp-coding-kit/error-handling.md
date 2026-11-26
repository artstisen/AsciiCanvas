---
description: Result pattern for expected failures, ProblemDetails for HTTP, exception handling only at boundaries.
---

# Error handling

## Expected and unexpected failures

| Failure | Handle with |
|---|---|
| Not found, validation, conflict, business-rule violation | **Result** (`Result` / `Result<T>` with typed errors) |
| Invalid input at an API boundary | Validation (FluentValidation / built-in) → 400 ValidationProblem |
| External service failure you can recover from | Catch the **specific** exception → `Result.Failure` |
| Bugs, lost DB connection, unexpected states | Let it **propagate** to the global handler |

- **DO** use a Result pattern for expected failures. Why: exceptions are expensive and hide control flow; a Result makes failure part of the signature.
- **DO** use typed error kinds (`NotFound`, `Validation`, `Conflict`, `Unauthorized`, `Forbidden`) so mapping to HTTP status codes and logs is consistent.
- **DON'T** use `try`/`catch` for flow control. If you can predict the failure, return a Result.
- If the project already uses a Result library (ErrorOr, FluentResults, Ardalis.Result, OneOf) or exceptions-for-flow, **follow the project**. Don't introduce a second style.

```csharp
// DO
public async Task<Result<OrderResponse>> Handle(GetOrder query, CancellationToken ct)
{
    var order = await db.Orders.AsNoTracking()
        .Where(o => o.Id == query.Id)
        .Select(o => new OrderResponse(o.Id, o.Total))
        .FirstOrDefaultAsync(ct);

    return order is null ? Error.NotFound("Order", query.Id) : order;
}

// DON'T — exception for an expected outcome
var order = await db.Orders.FindAsync([id], ct)
    ?? throw new NotFoundException($"Order {id} not found");
```

## Exceptions

- **DON'T** catch bare `Exception` except at the outermost boundary (global handler, `BackgroundService` loop, `Main`). Why: broad catches swallow bugs.
- **DON'T** catch-and-rethrow without adding value. Use `throw;` (never `throw ex;`) when you must rethrow. Wrap only to add context: `throw new X("context", ex)`.
- **DON'T** swallow exceptions silently. If you catch, log with the exception object or return a failure.
- **DON'T** catch `OperationCanceledException` as an error — let it propagate when the token was cancelled.
- Throw `ArgumentException` & friends for **programmer errors** (contract violations) — that is what exceptions are for.

## HTTP errors

- **Every HTTP error response is ProblemDetails (RFC 9457).** No bare strings or ad-hoc JSON error shapes.
- **Every web project has a global handler:** `builder.Services.AddProblemDetails()`, `AddExceptionHandler<GlobalExceptionHandler>()`, `app.UseExceptionHandler()`. If missing, add it.
- **Never leak stack traces or exception messages outside Development.**
- Map Result errors to status codes in one place (an extension like `result.ToProblem()`), not per endpoint.

## Validation

- **Validate at system boundaries** — API input, message consumers, external responses, config (`ValidateOnStart`).
- **Don't re-validate inside internal code.** Why: trusted-zone double validation adds noise, not safety.

Templates for `Result`, `Error`, `ToProblem()` and `GlobalExceptionHandler`: see `.claude/csharp-coding-kit/reference/snippets.md`.
