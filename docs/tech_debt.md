# Potential Technical Debt

Documentation of the potential sources and aspects of Technical Debt that may be incurred throughout the development of the project.

## 1. NodaTime Usage

Verify that all usages of NodaTime types are correct within the domain of discourse (i.e. is it always the expected output?):

- Instant vs LocalDateTime vs ZonedDateTime vs OffsetDateTime
- Sensible conversions between default C# time types and NodaTime types where necessary (the default ones should preferably be avoided altogether)

**Concerns/Considerations**:
- Accidental use of `DateTime`/`DateTimeOffset` in new models or controllers
- Serialization quirks with NodaTime types in JSON responses
- Missing unit tests for time-handling logic in expanding domain
- Clock dependency patterns spreading to new components without verification

It is probably worthwhile to create unit tests concerning the above (or ensure that integration and end-to-end tests verify correct handling of them).

## 2. Two Separate JSON Options Registrations

There are two distinct `JsonOptions` registrations in `Program.cs`:
- `AddControllers().AddJsonOptions(...)` — NodaTime serialization for MVC controller responses
- `Configure<JsonOptions>(...)` — naming policy and enum converters, also covering `[FromQuery]` binding

These serve different purposes and must be kept consistent as new types are introduced.

**Conventions to maintain**:
- Any new NodaTime type introduced to the domain should be verified to serialize/deserialize correctly under both registrations
- The `Configure<JsonOptions>` registration is not a duplicate — it intentionally covers query binding scenarios that `AddControllers().AddJsonOptions` does not

## 3. Logging — LoggerMessage Source Generator Adoption

The codebase uses .NET's LoggerMessage source generator pattern for high-performance logging. Drift from this pattern introduces CA1848 and CA1873 diagnostics and degrades runtime performance.

**Concerns/Considerations**:
- Direct `ILogger` extension method calls (`LogDebug`, `LogInformation`, etc.) that have not been migrated to `[LoggerMessage]`-attributed partial methods — each such call boxes value types, re-parses the message template, and allocates on every invocation (CA1848)
- Method calls or complex expressions (e.g. `string.Join`, LINQ, helper methods) passed directly as logging arguments are evaluated unconditionally, even when the log level is disabled (CA1873)
- String interpolation in log messages bypasses structured logging entirely and should be replaced with named placeholders
- EventId collisions: EventIds must be unique across the solution. As new log events are added, duplicate IDs may be introduced if the convention is not enforced — consider a central EventId registry or a CI check
- `[LoggerMessage]` partial classes or methods missing the `partial` keyword will fail to compile with a non-obvious error
- Dynamic log level usage (omitting `Level` from the attribute) requires extra care to ensure the `LogLevel` parameter is always passed correctly at call sites

See `docs/logging_best_practices.md` for the full pattern, before/after examples, and EventId conventions.

## 4. Timestamp Fields — Must Never Be Set Manually

`CreatedAt` and `UpdatedAt` are infrastructure-managed fields and must never be assigned by application code.

- `CreatedAt` is set by the database via `DEFAULT CURRENT_TIMESTAMP` on insert (`ValueGeneratedOnAdd`). It is `init`-only on the interface to prevent post-construction mutation. A Laraue `BeforeUpdate` trigger that raises an exception if `created_at` changes is the enforcement mechanism and should be added to `EntityConfig` once the first entity table exists.
- `UpdatedAt` is stamped by `StampTimestamps.StampUpdateTimestamps` in the `SaveChanges`/`SaveChangesAsync` overrides. Do not assign it directly in entity code or command handlers — doing so will be silently overwritten on the next save anyway, but is misleading and error-prone.

**Conventions to maintain**:
- Never set `CreatedAt` in object initialisers, constructors, or command handlers
- Never set `UpdatedAt` outside of `StampTimestamps`
- Any bulk-operation path (e.g. `ExecuteUpdateAsync`, raw SQL, EF's `BulkExtensions` if added later) bypasses `SaveChanges` overrides and therefore bypasses `UpdatedAt` stamping — handle explicitly in those paths

## 5. HealthChecks UI — In-Memory Storage

`AddInMemoryStorage()` is used for the HealthChecks UI backing store, meaning history is lost on every restart. This is intentional for the MVP. Replace with a PostgreSQL-backed store (`AddPostgreSqlStorage`) once the schema is stable and operational visibility becomes a requirement.

## 6. Configuration — What Goes Where

The project uses several configuration mechanisms that serve different purposes and must not be conflated.

### The mechanisms

**`appsettings.json`** — committed to VCS. Contains non-sensitive defaults that are the same everywhere: log levels, health check evaluation intervals, OpenAPI document settings. Never put secrets or environment-specific values here.

**`appsettings.Development.json`** — committed to VCS. Overrides for the `Development` environment only. Can hold non-sensitive dev-specific values (e.g. relaxed log levels, feature flags). Still no secrets.

**User Secrets (`secrets.json`)** — stored outside the repo at `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`. The `UserSecretsId` in `Cartwell.csproj` links the project to this file. Used for local developer secrets (connection strings, API keys) that must never be committed. Only active in `Development` environment. Managed via `dotnet user-secrets set "Key" "Value"` or by editing the file directly. `secrets.example.json` in the repo documents the expected shape without real values.

**`.env`** — used by Docker Compose only. Not read by the ASP.NET Core configuration system at all. Provides values for `${VAR}` substitutions in `compose.yaml`. Should be gitignored; a `.env.example` with placeholder values should be committed instead. Currently only `POSTGRES_PASSWORD` is required (`:?` syntax); `POSTGRES_USER` and `POSTGRES_DB` have defaults and are optional.

**Environment variables** — the ASP.NET Core configuration system reads environment variables at runtime with higher priority than `appsettings.json`. Used in production and CI where secrets managers or orchestrators (Docker, Kubernetes) inject values directly. Connection strings follow the double-underscore convention: `ConnectionStrings__Primary` maps to `ConnectionStrings:Primary`.

### Priority order (highest to lowest, Development)

1. Environment variables
2. User Secrets
3. `appsettings.Development.json`
4. `appsettings.json`

### What goes where — quick reference

| Value | Mechanism |
|---|---|
| Log levels, health check config, OpenAPI settings | `appsettings.json` |
| Dev-only feature flags, relaxed log levels | `appsettings.Development.json` |
| Local DB connection string, local API keys | User Secrets |
| Docker Compose DB credentials | `.env` (gitignored) |
| Production secrets, CI secrets | Environment variables / secrets manager |

### Concerns to watch

- The connection string lives in User Secrets locally but must be injected as an environment variable in production. Ensure `Program.cs` always reads it via `IConfiguration` and never has a hardcoded fallback.
- `.env` and User Secrets are entirely separate — the connection string in User Secrets is for the app process; the password in `.env` is for the Docker Compose database container. They must be kept consistent manually. If the password in `.env` changes, the User Secrets connection string must also be updated. The same applies to `POSTGRES_PORT` — if the host port is changed in `.env`, the port in the User Secrets connection string must match.
- As the project grows (external APIs, email providers, payment gateways), each new secret needs a `secrets.example.json` entry and a corresponding User Secrets entry for local dev — establish this as the convention from the first addition.
