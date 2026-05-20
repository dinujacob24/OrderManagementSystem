# LoggingService Integration Guide

This guide explains, step by step, how to wire any service in the **OrderManagementSystem** solution into the centralized **LoggingService** so that all of its `ILogger<T>` calls (and any manual logs) are forwarded over HTTP and persisted in the `LoggingDb.Logs` SQL Server table.

The reference integration lives in **CustomerService** — follow this guide to replicate it in any other microservice (e.g. `OrderService`, `Shared.Messages` consumers, future services).

---

## 1. How it works (1-minute mental model)

```
┌──────────────────────┐     POST /logs (JSON)      ┌──────────────────────┐
│   YourService        │ ─────────────────────────► │   LoggingService     │
│   ILogger<T>.Log...  │                            │   /logs controller   │
│   ILogSender         │                            │   EF Core → SQL      │
└──────────────────────┘                            └──────────────────────┘
                                                              │
                                                              ▼
                                                     ┌──────────────────┐
                                                     │ LoggingDb.Logs   │
                                                     │  (LocalDB / SQL) │
                                                     └──────────────────┘
```

- The **Shared** project provides the client-side plumbing: `ILogSender`, `CentralLogSender`, `CentralLogger`, and the `AddCentralLogging` extension method.
- Calling `builder.AddCentralLogging("YourServiceName")` in `Program.cs` plugs a custom `ILoggerProvider` into ASP.NET's logging pipeline. Every `_logger.LogInformation(...)` is then fire-and-forget POSTed to `LoggingService` at `http://localhost:5017/logs`.
- A `CorrelationIdMiddleware` attaches an `X-Correlation-Id` header to every request so you can trace a single user action across all services in the `Logs` table.

---

## 2. Prerequisites

Before integrating, make sure:

1. **SQL Server LocalDB** is installed (ships with Visual Studio).
2. **LoggingService** is runnable: from `LoggingService/` run
   ```powershell
   dotnet ef database update
   dotnet watch run --launch-profile https
   ```
   It must be listening on `http://localhost:5017` (HTTP) before your service starts producing logs. Logs sent while LoggingService is down are silently dropped (fire-and-forget by design).
3. Your service is an **ASP.NET Core 8+ web app** that uses `WebApplication.CreateBuilder(args)` in `Program.cs`.

---

## 3. Step-by-step integration

### Step 1 — Add a project reference to `Shared`

In your service's `.csproj`, add:

```xml
<ItemGroup>
  <ProjectReference Include="..\Shared\Shared.csproj" />
</ItemGroup>
```

`Shared` already brings in:
- `Microsoft.Extensions.Logging.Abstractions` → so you can inject `ILogger<T>`
- `Microsoft.Extensions.Http` → for the typed `HttpClient` used to post logs
- `System.Text.Json` → for serialization

You do **not** need to add these packages manually in your service.

---

### Step 2 — Add `LoggingServiceUrl` to `appsettings.json`

Open `YourService/appsettings.json` and add the top-level key:

```json
{
  "ConnectionStrings": { /* ... */ },

  "LoggingServiceUrl": "http://localhost:5017",

  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

Notes:
- **Use the HTTP endpoint (`5017`), not HTTPS.** The LoggingService dev cert is not trusted across processes by default, and the LogSender's fire-and-forget catch block will swallow the TLS exception — meaning logs disappear silently.
- If `LoggingServiceUrl` is missing, the extension falls back to `http://localhost:5017` — same default. But set it explicitly so each environment can override it (e.g., a staging URL).
- For per-environment overrides, set the same key in `appsettings.Development.json`, `appsettings.Production.json`, etc.

---

### Step 3 — Wire `AddCentralLogging` in `Program.cs`

This is the single most important line. Right after `var builder = WebApplication.CreateBuilder(args);`, add:

```csharp
using Shared.Logging;
using Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Central Logging — sends logs to LoggingService via HTTP.
// Must be called BEFORE builder.Build() and ideally as the first registration
// so every other component that resolves ILogger<T> picks up the central provider.
builder.AddCentralLogging("YourServiceName");
```

Replace `"YourServiceName"` with your service identifier — e.g. `"OrderService"`, `"InventoryService"`, `"NotificationService"`. This value lands in the `ServiceName` column of `LoggingDb.Logs`, so use a stable, human-readable name.

**What this one call does internally** (from [Shared/Logging/LoggingExtensions.cs](Shared/Logging/LoggingExtensions.cs)):

1. Registers `IHttpContextAccessor` so the logger can read the current `X-Correlation-Id` header and JWT user info.
2. Registers a named `HttpClient` (`"CentralInternal"`) pointing at the `LoggingServiceUrl`.
3. Registers `ILogSender` (the manual logger) as `Scoped`.
4. Plugs `CentralLoggerProvider` into `builder.Logging` — this is what makes every `ILogger<T>` call automatic.

---

### Step 4 — Add the CorrelationId middleware

After `var app = builder.Build();`, wire the middleware **before** authentication/authorization:

```csharp
var app = builder.Build();

// ... your exception handler, swagger, https redirect ...

// Attach/propagate X-Correlation-Id header on every request.
// Must be early in the pipeline so downstream middleware / handlers see the same ID.
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
// ... rest of pipeline ...
```

Why this matters: with the middleware in place, every log row for a single inbound HTTP request shares the same `CorrelationId` value. If your service then calls another service via `HttpClient`, you can forward this header so logs from that second service tie back to the original request. (Pattern: read `HttpContext.Request.Headers["X-Correlation-Id"]` in an outgoing `DelegatingHandler` and re-attach it on the outbound request.)

---

### Step 5 — Inject `ILogger<T>` into your handlers/services

This is the everyday API. **You do not need to know that the logger is "central" — just use `ILogger<T>` like normal.** The custom provider transparently forwards.

#### Example: a MediatR handler

```csharp
using MediatR;
using Microsoft.Extensions.Logging;

public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, CreateOrderResponse>
{
    private readonly OrderDbContext _db;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(OrderDbContext db, ILogger<CreateOrderHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CreateOrderResponse> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        if (await _db.Orders.AnyAsync(o => o.OrderId == cmd.OrderId, ct))
        {
            _logger.LogWarning("Create rejected — order {OrderId} already exists", cmd.OrderId);
            throw new ConflictException($"Order '{cmd.OrderId}' already exists.");
        }

        // ... business logic ...
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created order {OrderId} for customer {CustomerId}", cmd.OrderId, cmd.CustomerId);

        return new CreateOrderResponse(cmd.OrderId);
    }
}
```

#### Best practices for log messages

- **Always use structured placeholders** (`{OrderId}`, `{CustomerId}`) instead of string interpolation (`$"Created order {cmd.OrderId}"`). The placeholders survive into searchable columns and don't get baked into the message string.
- **One log line per business outcome**, not per code branch. Aim for: 1 success line + 1 line per distinct failure reason (not-found, conflict, validation).
- **Use the level that matches the operational meaning**:
  - `LogInformation` → normal success (created/updated/fetched).
  - `LogWarning` → expected business failure (not-found, conflict, validation).
  - `LogError` → unexpected failure / exception you intend to alert on.
- **Include identifiers** (the entity ID, customer ID, correlation hints) — without these, a row in `Logs` is useless.

---

### Step 6 — (Optional) Use `ILogSender` for manual / off-thread logs

When you cannot easily inject `ILogger<T>` (e.g. inside a background `IHostedService`, a Quartz job, or a place that already takes a different logger), inject `ILogSender` directly:

```csharp
using Shared.DTOs;
using Shared.Logging;

public class NightlyReportJob
{
    private readonly ILogSender _log;

    public NightlyReportJob(ILogSender log) { _log = log; }

    public async Task RunAsync()
    {
        await _log.SendLogAsync("Nightly report started", logLevel: "Information");

        try
        {
            // ... work ...
            await _log.SendLogAsync("Nightly report completed", logLevel: "Information");
        }
        catch (Exception ex)
        {
            await _log.SendLogAsync(
                message: "Nightly report failed",
                logLevel: "Error",
                exception: ex.ToString());
            throw;
        }
    }
}
```

`SendLogAsync` is fire-and-forget — it returns `Task.CompletedTask` immediately while the HTTP POST runs in the background. You will never see an exception from it propagate.

---

### Step 7 — (If your service uses the test pattern) update tests

If you instantiate handlers directly in unit tests, the new constructor parameter will break them. Two options:

#### Option A — Pass `NullLogger<T>.Instance` (recommended for unit tests)

```csharp
using Microsoft.Extensions.Logging.Abstractions;

var sut = new CreateOrderHandler(
    db,
    NullLogger<CreateOrderHandler>.Instance);
```

`NullLogger<T>.Instance` is a no-op logger that satisfies the contract without producing output or HTTP calls.

#### Option B — Use Moq if you want to assert on log calls

```csharp
using Microsoft.Extensions.Logging;
using Moq;

var loggerMock = new Mock<ILogger<CreateOrderHandler>>();
var sut = new CreateOrderHandler(db, loggerMock.Object);
// ... act ...
loggerMock.Verify(
    l => l.Log(
        LogLevel.Information,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created order")),
        null,
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    Times.Once);
```

---

## 4. Verifying the integration

After completing Steps 1–5:

1. Make sure **LoggingService is running** on `http://localhost:5017`.
2. Apply your service's own EF migrations if needed.
3. Start your service: `dotnet watch run --launch-profile https`.
4. Trigger any endpoint that should produce a log (e.g. `GET /api/orders`).
5. Query the `Logs` table:
   ```sql
   SELECT TOP 20 LogId, Timestamp, ServiceName, LogLevel, Message, CorrelationId, UserName
   FROM LoggingDb.dbo.Logs
   WHERE ServiceName = 'YourServiceName'
   ORDER BY Timestamp DESC;
   ```

You should see one row per `_logger.Log*` call inside your handlers.

You can also call `GET http://localhost:5017/logs?serviceName=YourServiceName` to retrieve them via the LoggingService API directly.

---

## 5. Common pitfalls

### "I don't see any logs at all"

| Symptom | Likely cause | Fix |
|---|---|---|
| No rows in `Logs` table | LoggingService not running | Start it on `:5017`. Logs fail silently when it's down. |
| No rows for a successful 200 response | Framework request logs are filtered to `Warning` | Add an explicit `_logger.LogInformation(...)` line inside your handler. |
| 404 from LoggingService when posting | Wrong base URL (missing trailing slash) | Check `LoggingServiceUrl` in config. The extension auto-appends `/`. |
| TLS errors (silently swallowed) | Pointing at `https://localhost:7158` from another process | Use the HTTP URL (`http://localhost:5017`). |

### "Framework noise floods the Logs table"

The `CentralLogger.IsEnabled` method in `Shared/Logging/CentralLogger.cs` already filters `Microsoft.*` and `System.*` categories to `Warning+`. Your own categories pass at `Information+`. If you want to silence specific categories further, configure standard `Microsoft.Extensions.Logging` filters in `appsettings.json`:

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "Microsoft.EntityFrameworkCore": "Warning"
  }
}
```

These filters are applied by the standard `LoggerFactory` *before* the message reaches `CentralLoggerProvider`, so they reduce HTTP traffic too.

### "Tests fail to build after I added `ILogger<T>` to a handler"

Every place that constructs the handler directly (unit tests, manual DI, factory methods) needs to pass a logger. Use `NullLogger<T>.Instance` from `Microsoft.Extensions.Logging.Abstractions`.

### "Integration tests blow up with 'Relational-specific methods can only be used...'"

If your `Program.cs` calls `db.Database.Migrate()` on startup, guard it so it doesn't run under the InMemory provider used by integration tests:

```csharp
var isTesting = builder.Configuration.GetValue<bool>("Testing:UseInMemoryDatabase");

// ... later, after var app = builder.Build(); ...

if (!isTesting)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<YourDbContext>();
    db.Database.Migrate();
}
```

### "Logs from one user action are scattered across services with different correlation IDs"

You added the middleware in your service but did **not** forward the `X-Correlation-Id` header on outgoing HTTP calls. Either:
1. Use a `DelegatingHandler` on your `HttpClient` that reads `IHttpContextAccessor.HttpContext.Request.Headers["X-Correlation-Id"]` and re-attaches it on outbound requests, **or**
2. Forward it manually before each `HttpClient.SendAsync` call.

---

## 6. End-to-end summary checklist

Copy this checklist into your PR description when integrating LoggingService into a new service:

- [ ] Added `<ProjectReference Include="..\Shared\Shared.csproj" />` to the service's `.csproj`
- [ ] Added `"LoggingServiceUrl": "http://localhost:5017"` to `appsettings.json` (and any environment-specific files)
- [ ] Added `using Shared.Logging;` and `builder.AddCentralLogging("MyServiceName");` to `Program.cs`
- [ ] Added `using Shared.Middleware;` and `app.UseMiddleware<CorrelationIdMiddleware>();` to `Program.cs` (before `UseAuthentication`)
- [ ] Injected `ILogger<T>` into every handler/service that has business outcomes worth recording
- [ ] Added `_logger.LogInformation(...)` for success paths and `_logger.LogWarning(...)` for expected failures
- [ ] Used structured placeholders (`{Id}`), never `$"..."` interpolation
- [ ] Updated unit-test handler constructions to pass `NullLogger<T>.Instance`
- [ ] Guarded any `db.Database.Migrate()` on startup with the test-mode check
- [ ] Verified by triggering an endpoint and seeing the corresponding row in `LoggingDb.Logs`

---

## 7. Reference: file map

If you need to debug the plumbing, here is where everything lives:

| Concern | File |
|---|---|
| `ILogger<T>` → HTTP plumbing | [Shared/Logging/CentralLogger.cs](Shared/Logging/CentralLogger.cs) |
| Manual `ILogSender` implementation | [Shared/Logging/CentralLogSender.cs](Shared/Logging/CentralLogSender.cs) |
| `AddCentralLogging(...)` extension | [Shared/Logging/LoggingExtensions.cs](Shared/Logging/LoggingExtensions.cs) |
| `ILogSender` interface | [Shared/Logging/ILogSender.cs](Shared/Logging/ILogSender.cs) |
| Wire-format DTO | [Shared/DTOs/LogEntryDto.cs](Shared/DTOs/LogEntryDto.cs) |
| Correlation-id middleware | [Shared/Middleware/CorrelationIdMiddleware.cs](Shared/Middleware/CorrelationIdMiddleware.cs) |
| Receive endpoint (`POST /logs`) | [LoggingService/Controllers/LogsController.cs](LoggingService/Controllers/LogsController.cs) |
| Storage entity | [LoggingService/Models/LogEntry.cs](LoggingService/Models/LogEntry.cs) |
| EF DbContext | [LoggingService/Data/LoggingDbContext.cs](LoggingService/Data/LoggingDbContext.cs) |
| Reference integration | [CustomerService/Program.cs](CustomerService/Program.cs) |
