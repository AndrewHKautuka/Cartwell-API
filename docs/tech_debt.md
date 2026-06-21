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
