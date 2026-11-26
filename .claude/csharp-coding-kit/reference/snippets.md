# Code snippets (read on demand)

Copy-ready building blocks the rules refer to. Adapt namespaces; if the project already has an equivalent, use the project's version.

## Result and Error

```csharp
public enum ErrorType { Validation, NotFound, Conflict, Unauthorized, Forbidden, Failure }

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error NotFound(string resource, object id) =>
        new($"{resource}.NotFound", $"{resource} '{id}' was not found.", ErrorType.NotFound);
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}

public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;
    private Result(Error error) : base(error) { }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public static Result<T> Success(T value) => new(value);
    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
```

## Result to ProblemDetails

```csharp
public static class ResultHttpExtensions
{
    public static IResult ToProblem(this Result result)
    {
        if (result.IsSuccess)
            throw new InvalidOperationException("Cannot convert a successful result to a problem.");

        var error = result.Error!;
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        return TypedResults.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: status);
    }
}
```

## Global exception handler

```csharp
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetails,
    IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails =
            {
                Title = "An unexpected error occurred",
                Status = StatusCodes.Status500InternalServerError,
                Detail = env.IsDevelopment() ? exception.Message : null
            }
        });
    }
}

// Program.cs
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// ...
app.UseExceptionHandler();
```

## IEndpointGroup and discovery of endpoints

```csharp
public interface IEndpointGroup
{
    void Map(IEndpointRouteBuilder app);
}

public static class EndpointExtensions
{
    // Reflection-based: fine for JIT apps. For Native AOT, register groups explicitly.
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var groups = typeof(Program).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IEndpointGroup)))
            .Select(Activator.CreateInstance)
            .Cast<IEndpointGroup>();

        foreach (var group in groups)
            group.Map(app);

        return app;
    }
}

// Program.cs — never changes when endpoints are added
app.MapEndpoints();
```

## Vertical slice (one file, no mediator)

```csharp
namespace MyApp.Features.Orders;

public sealed record CreateOrderRequest(string Sku, int Quantity);
public sealed record CreateOrderResponse(Guid Id);

public sealed class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public sealed class CreateOrderHandler(AppDbContext db, TimeProvider clock)
{
    public async Task<Result<CreateOrderResponse>> HandleAsync(CreateOrderRequest request, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Sku == request.Sku, ct))
            return Error.NotFound("Product", request.Sku);

        var order = Order.Create(request.Sku, request.Quantity, clock.GetUtcNow());
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        return new CreateOrderResponse(order.Id);
    }
}
```

## Options with validation

```csharp
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required] public required string Host { get; init; }
    [Range(1, 65535)] public int Port { get; init; } = 587;
}

builder.Services.AddOptions<SmtpOptions>()
    .BindConfiguration(SmtpOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

## Resilient HTTP client

```csharp
builder.Services.AddHttpClient<PaymentsClient>(c => c.BaseAddress = new Uri("https://payments.example.com"))
    .AddStandardResilienceHandler();
```

## Integration test fixture (xUnit v3, Testcontainers, PostgreSQL)

```csharp
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().Build();

    // Override the connection string the app already uses, instead of re-registering the DbContext.
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
}

public sealed class OrderTests(ApiFactory factory) : IClassFixture<ApiFactory> { /* tests */ }
```

(`Program` must be visible to the test project: add `public partial class Program;` at the end of `Program.cs`, or rely on the .NET 10 source generator that makes it public automatically.)
