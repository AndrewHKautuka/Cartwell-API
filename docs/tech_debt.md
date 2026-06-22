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
