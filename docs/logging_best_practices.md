# Logging Best Practices

This guide covers high-performance logging in the CHUMA codebase using .NET's LoggerMessage source generators.

---

## CA1873 and CA1848 Diagnostics

### CA1873 — Unnecessary Argument Evaluation

**Severity**: Warning

CA1873 fires when a logging call passes arguments that involve method calls or complex expressions. The problem is that these expressions are evaluated *before* the logger checks whether that log level is even enabled — wasting CPU and allocations on work that gets thrown away.

```csharp
// ❌ CA1873: EmailDomain() is called even when Warning is disabled
logger.LogWarning("Login failed for domain {EmailDomain}", EmailDomain(email));

// ❌ CA1873: string.Join() is called even when Warning is disabled
logger.LogWarning("Missing deps: {Deps}", string.Join(", ", missingSlugs));
```

### CA1848 — Use LoggerMessage Delegates

**Severity**: Informational (not a warning — appears in build output but doesn't affect warning counts)

CA1848 recommends replacing direct `ILogger` extension method calls (`LogDebug`, `LogInformation`, etc.) with LoggerMessage source-generated methods. The generated methods eliminate boxing, reduce allocations, and parse message templates once at compile time rather than on every call.

```csharp
// ❌ CA1848: direct extension method call
logger.LogDebug("GetSectors called with ParentIds={ParentIds}", query.ParentIds);
```

---

## LoggerMessage Source Generator Pattern

Define a `static partial class` with `[LoggerMessage]`-attributed partial methods. The compiler fills in the implementation.

```csharp
namespace CHUMA.Auth.Auth.Logging;

public static partial class AuthControllerLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Login failed for domain {EmailDomain}")]
    public static partial void LoginFailed(ILogger logger, string emailDomain);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Login succeeded for domain {EmailDomain}")]
    public static partial void LoginSucceeded(ILogger logger, string emailDomain);
}
```

Call site:

```csharp
// Helper method is called here, not inside the logging method
AuthControllerLog.LoginFailed(logger, EmailDomain(request.Email));
```

### Dynamic Log Level

When the level isn't known at compile time, omit `Level` from the attribute and add a `LogLevel` parameter:

```csharp
[LoggerMessage(
    EventId = 2601,
    Message = "Connector fetch completed with status {Status}")]
public static partial void ConnectorFetchCompleted(
    ILogger logger, LogLevel level, string status);

// Usage
var level = success ? LogLevel.Information : LogLevel.Warning;
IngestionLog.ConnectorFetchCompleted(logger, level, status);
```

---

## Before / After Examples

### Method Call in Argument

```csharp
// Before
logger.LogWarning(
    "Login failed for domain {EmailDomain}",
    EmailDomain(loginUserRequest.Email));

// After — define once
[LoggerMessage(EventId = 1001, Level = LogLevel.Warning,
    Message = "Login failed for domain {EmailDomain}")]
public static partial void LoginFailed(ILogger logger, string emailDomain);

// After — call site
AuthControllerLog.LoginFailed(logger, EmailDomain(loginUserRequest.Email));
```

### String.Join in Argument

```csharp
// Before
logger.LogWarning(
    "RegisterPlugin: slug {Slug} has missing dependencies: {MissingSlugs}",
    command.Manifest.Slug, string.Join(", ", missingSlugs));

// After — define once
[LoggerMessage(EventId = 4201, Level = LogLevel.Warning,
    Message = "RegisterPlugin: slug {Slug} has missing dependencies: {MissingSlugs}")]
public static partial void RegisterPluginMissingDependencies(
    ILogger logger, string slug, string missingSlugs);

// After — call site (String.Join stays at the call site)
PluginRegistrationLog.RegisterPluginMissingDependencies(
    logger, command.Manifest.Slug, string.Join(", ", missingSlugs));
```

### String Interpolation

```csharp
// Before
logger.LogInformation($"Tenant {tenantId} provisioned in {elapsed}ms");

// After — define once
[LoggerMessage(EventId = 3001, Level = LogLevel.Information,
    Message = "Tenant {TenantId} provisioned in {ElapsedMs}ms")]
public static partial void TenantProvisioned(
    ILogger logger, Guid tenantId, long elapsedMs);

// After — call site
SomeLog.TenantProvisioned(logger, tenantId, elapsed);
```

---

## EventId Conventions

EventIds are unique integers that identify a specific log event. They are grouped by non-test project (if applicable) and functional area.

Rules:
- EventIds must be **unique within the solution**.
- Pick the next available ID in the appropriate range when adding a new log event.
- Do not reuse IDs, even after removing a log event.

---

## Common Pitfalls

| Pitfall | Fix |
|---------|-----|
| Calling helper methods inside the `[LoggerMessage]` method body | Call them at the call site and pass the result as a parameter |
| Using `string.Join` or LINQ inside the logging call | Compute the string before calling the log method |
| Forgetting `partial` on the class or method | Both the containing class and the method must be `partial` |
| Duplicate EventIds in the same project | Check the EventId table above before assigning a new ID |
| Placeholder name mismatch between template and parameter | Parameter names must match `{Placeholder}` names (case-insensitive) |

---

## References

- [Compile-time logging source generation — Microsoft Docs](https://learn.microsoft.com/dotnet/core/extensions/logger-message-generator)
- [High-performance logging with LoggerMessage — Microsoft Docs](https://learn.microsoft.com/aspnet/core/fundamentals/logging/loggermessage)
