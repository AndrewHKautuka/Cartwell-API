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
