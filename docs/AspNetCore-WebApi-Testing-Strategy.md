# ASP.NET Core Web API — Testing Strategy

## Solution Layout

Flat solution, one test project per source project, no `src/` or `tests/` wrapper directories:

```
MyApp.sln
├── MyApp-Api/               ← entry point
├── MyApp-Api.Tests/
├── MyApp-Core/              ← shared library (models, validators, services, EF Core)
├── MyApp-Core.Tests/
├── MyApp-[Module]/          ← additional modules as the project grows
└── MyApp-[Module].Tests/
```

Each `.Tests` project mirrors its source project's folder structure. Test-only infrastructure (fixtures, factories, custom generators) goes in an `Infrastructure/` folder at the test project root — no corresponding source folder needed.

---

## NuGet Packages

All packages are versioned centrally in `Directory.Packages.props` at the solution root. Each test project pulls in only what it needs.

### `xunit` and `xunit.runner.visualstudio`

`xunit` is the core testing framework — attributes, assertions, test runners. `xunit.runner.visualstudio` is the VSTest adapter that allows `dotnet test` (and CI pipelines) to discover and execute xUnit tests. Despite the name, **this package is needed in Rider too** — Rider has its own native xUnit runner for interactive use, but `xunit.runner.visualstudio` is what `dotnet test` on the command line (and therefore your CI pipeline) uses. Both are always present together.

### `Microsoft.NET.Test.Sdk`

Required scaffolding for the `dotnet test` runner. Without it, `dotnet test` cannot locate your test project as a test target at all. Always present alongside `xunit.runner.visualstudio`.

### `NSubstitute` — and why not Moq

NSubstitute is preferred over Moq for two independent reasons.

The first is ergonomics. With Moq you work through a `Mock<T>` wrapper object; you call `.Setup(x => x.Method())` to configure it, and you must call `.Object` to obtain the actual instance to inject. With NSubstitute the substitute *is* the instance — you configure it by calling methods directly on it:

```csharp
// Moq
var mock = new Mock<IOrderRepository>();
mock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(order);
var service = new OrderService(mock.Object);  // .Object needed

// NSubstitute
var repo = Substitute.For<IOrderRepository>();
repo.GetByIdAsync(1).Returns(order);
var service = new OrderService(repo);          // no wrapper
```

This removes a persistent mental layer-shift and makes test code read more like production code.

The second reason is historical trust. In 2023, Moq introduced SponsorLink, a package that silently scanned developer environments during build and transmitted email addresses to a third-party service. The feature was removed after significant community backlash, but the episode demonstrated that maintainer priorities could diverge from user interests in ways that are difficult to audit. NSubstitute has no equivalent history.

### `AwesomeAssertions`

AwesomeAssertions is a community-controlled fork of FluentAssertions, recommended over FluentAssertions itself for any new project. Three meaningful options exist, each with a different trade-off, and the choice is worth making deliberately.

**FluentAssertions v7** stays on Apache 2.0 permanently and will receive bug fixes indefinitely. However, it receives no new features — it is a maintained snapshot. Choosing it means accepting permanent feature freeze relative to where the library stood at v7.

**AwesomeAssertions** is the living continuation. It forked legally from the Apache 2.0 codebase before the FluentAssertions v8 licence change, is now at v9.x, has six named maintainers, commits to Apache 2.0 permanently, and has 5.4 million downloads and growing. Migration from FluentAssertions v7 is a single package reference change with no code changes whatsoever, because AwesomeAssertions retains the `FluentAssertions` namespace by right under the Apache 2.0 licence. All `.Should()` syntax in code examples throughout this document is equally valid for both packages.

**Shouldly** is a different philosophy. Instead of chaining off `.Should()`, it puts assertions directly on the value: `value.ShouldBe(expected)`, `Should.Throw<ArgumentException>(() => act())`. It is BSD-3-Clause, has 111 million downloads, and is actively maintained with no licence history of concern. Its meaningful weakness is a shallower `ShouldBeEquivalentTo` — no member exclusion rules, no structural comparison configuration — which makes it less convenient when asserting against complex domain or response objects. It also has no Roslyn analyser package.

For Web API projects where you will frequently compare domain object graphs and HTTP response bodies, AwesomeAssertions is the better fit. If your assertion needs are straightforward and licence conservatism is the priority, Shouldly is a completely sound choice.

**What AwesomeAssertions gives you** is a natural-language assertion chain that replaces xUnit's `Assert.*` calls. The practical benefit is failure messages: it produces verbose, structured output that immediately shows what was expected and what was received:

```csharp
order.Total.Should().BeGreaterThan(0);
response.Should().BeEquivalentTo(expected, o => o.Excluding(x => x.CreatedAt));
act.Should().ThrowAsync<DomainException>().WithMessage("*out of stock*");
```

### `AwesomeAssertions.Analyzers`

Roslyn analyzers that enforce correct AwesomeAssertions usage at compile time, with auto-fixes. They catch anti-patterns such as `collection.Count().Should().Be(3)` (should be `HaveCount(3)`) and `result.Should().Be(true)` (should be `BeTrue()`). Add this to every test project — it enforces best practices without requiring the full API surface to be memorised. It ships under MIT and carries `PrivateAssets="all"` so it does not leak into production output:

```xml
<PackageReference Include="AwesomeAssertions.Analyzers">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

### `AwesomeAssertions.Web`

Provides assertion methods directly on `HttpResponseMessage`, including typed response body deserialisation and rich failure output containing the full HTTP request and response detail. Reduces boilerplate in integration test projects that make many HTTP-level assertions. Uses `System.Text.Json` by default. Add to integration test projects only.

### `AwesomeAssertions.AspNetCore.Mvc`

Adds assertions on `IActionResult` and `ActionResult<T>` return types for testing controllers as plain classes via direct method calls, without going through the HTTP pipeline. Only relevant if you write controller unit tests in addition to integration tests; it has no value if controller coverage is handled entirely through `WebApplicationFactory`. Pull in only when you have a concrete need for it.

### `Bogus`

A realistic fake data generator. Rather than scattering `"test"`, `"foo@example.com"`, and `123` throughout test setup, Bogus generates contextually appropriate values (`faker.Internet.Email()`, `faker.Address.City()`, `faker.Commerce.ProductName()`). This matters because some bugs only appear with inputs that approximate real-world shape — e.g. validation logic that accidentally passes `"test"` but would correctly reject a real name containing an accent character.

### `NodaTime.Testing` / `Microsoft.Extensions.TimeProvider.Testing`

Any code that reads the current time from a static source (`SystemClock.Instance`, `DateTime.UtcNow`, `DateTimeOffset.UtcNow`) is non-deterministic. Tests depending on the real clock can fail at day or month boundaries, behave differently under DST offsets, and cannot deliberately test time-sensitive scenarios such as token expiry, scheduling windows, or audit timestamp ordering. The fix is the clock injection pattern: services depend on an injected abstraction rather than a static call, and tests supply a controlled fake. Which package you need depends on which time abstraction your project uses:

- **NodaTime projects** — `NodaTime.Testing` provides `FakeClock`, which implements NodaTime's `IClock` interface. Inject `IClock` into services, resolve `SystemClock.Instance` from DI in production, and supply `new FakeClock(Instant.FromUtc(...))` in tests.
- **.NET 8+ projects not using NodaTime** — `Microsoft.Extensions.TimeProvider.Testing` provides `FakeTimeProvider`, which implements the built-in `TimeProvider` abstract class. This is the correct reach rather than a custom `IDateTimeProvider` interface. ASP.NET Core's own infrastructure — including JWT bearer validation — consumes `TimeProvider` from DI as of .NET 8, so a registered `FakeTimeProvider` also controls token expiry behaviour in integration tests without any additional wiring.

Both apply at two levels: unit tests inject the fake directly into the constructor, and integration tests register it in `ConfigureTestServices` so the entire application uses it for the duration of the run. See the unit testing and integration testing sections for usage examples.

### `FsCheck` and `FsCheck.Xunit`

A property-based testing library. Covered in depth in the FsCheck section below. `FsCheck.Xunit` provides the `[Property]` attribute that integrates property tests into the xUnit runner.

### `Microsoft.AspNetCore.Mvc.Testing`

Provides `WebApplicationFactory<T>`, which boots your actual `Program.cs` in-process against a test HTTP server. This is the foundation of all integration testing — covered in the integration testing section.

### `Testcontainers.PostgreSql`

Spins up a real PostgreSQL instance in Docker during test execution. Essential for anything that touches EF Core with a PostgreSQL-specific provider, because behaviours that only exist at the database level (triggers, generated columns, certain constraint types, full-text search, geospatial functions) are invisible to EF Core's in-memory and SQLite providers. The in-memory provider in particular gives false green tests that hide real bugs.

As of Testcontainers 4.10.0, the parameterless `PostgreSqlBuilder()` constructor is obsolete. You must now pass the image string directly to the constructor — see the integration testing section for the correct usage. This change was intentional: the old approach bundled a default image version that went stale over time, but updating it was a breaking change for consumers. The new API makes version pinning explicit and mandatory, which aligns with Testcontainers' own best-practice guidance to always pin the image version.

### `Testcontainers.Xunit` / `Testcontainers.XunitV3`

Optional packages that provide `ContainerTest<TBuilderEntity, TContainerEntity>` and `ContainerFixture<TBuilderEntity, TContainerEntity>` base classes, reducing the boilerplate of managing container lifecycle in xUnit tests. Instead of implementing `IAsyncLifetime` manually and wiring up start/stop in `InitializeAsync`/`DisposeAsync`, you inherit from one of these classes and override a `Configure()` method to provide the builder. Use `Testcontainers.Xunit` for xUnit v2 and `Testcontainers.XunitV3` for xUnit v3.

These are not needed when using `WebApplicationFactory<T>` — the factory already manages its own lifecycle via `IAsyncLifetime`. They are most useful for simpler test projects that use Testcontainers directly without a full `WebApplicationFactory` wrapper, such as data-layer tests that want a real database but don't need the HTTP pipeline.

### `Respawn`

Efficiently resets database state between tests by truncating only the tables that were written to, rather than recreating the schema or the container. This is what makes it practical to run integration tests at any useful volume without multi-minute startup times.

---

## Unit Testing

Unit tests cover a single class in complete isolation: all external dependencies (repositories, HTTP clients, clocks, email services) are substituted with NSubstitute fakes. No database, no HTTP, no file system.

**What unit tests cover:**

- Service classes — particularly branching logic and error paths
- Domain logic and validation
- Mapping configuration (verify your mapper config compiles without errors and spot-check non-trivial mappings)
- Utility and helper functions
- Factory classes

**Naming convention** — descriptive enough that a failing test name tells you what broke without reading the test body:

```
Method_Scenario_ExpectedResult
CreateOrder_WhenItemOutOfStock_ThrowsDomainException
ApplyDiscount_WithNegativePercentage_ThrowsArgumentException
```

**`[Fact]` vs `[Theory]`** — use `[Fact]` for single-case tests and `[Theory]` with `[InlineData]` or `[MemberData]` for parameterised inputs:

```csharp
[Theory]
[InlineData(-1)]
[InlineData(0)]
public void SetQuantity_WhenNotPositive_ThrowsArgumentException(int quantity)
{
    var order = new Order();
    var act = () => order.SetQuantity(quantity);
    act.Should().Throw<ArgumentException>();
}
```

**Shared setup** — use xUnit's constructor for per-test setup (xUnit creates a new instance per test method), `IClassFixture<T>` for setup shared across all tests in one class, and `[Collection]` when multiple test classes need the same shared state.

### Testing time-dependent code

Services that depend on the current time must receive it through an injected abstraction. Never call `SystemClock.Instance`, `DateTime.UtcNow`, or `DateTimeOffset.UtcNow` directly inside a class under test — there is no way to control or assert against those values from a test.

**NodaTime projects** — inject `IClock` and provide `FakeClock` in tests:

```csharp
// Production registration (in Program.cs or a service extension)
services.AddSingleton<IClock>(SystemClock.Instance);

// Service constructor
public class TokenService(IClock clock, ITokenRepository repo)
{
    public bool IsExpired(Token token) =>
        clock.GetCurrentInstant() > token.ExpiresAt;
}

// Unit test
[Fact]
public void IsExpired_WhenPastExpiry_ReturnsTrue()
{
    var expiry = Instant.FromUtc(2024, 6, 1, 12, 0, 0);
    var clock  = new FakeClock(expiry + Duration.FromMinutes(1));
    var repo   = Substitute.For<ITokenRepository>();
    var svc    = new TokenService(clock, repo);

    svc.IsExpired(new Token { ExpiresAt = expiry }).Should().BeTrue();
}

// Test that advances time explicitly
[Fact]
public void IsExpired_BeforeAndAfterExpiry_ChangesState()
{
    var expiry = Instant.FromUtc(2024, 6, 1, 12, 0, 0);
    var clock  = new FakeClock(expiry - Duration.FromSeconds(1));
    var svc    = new TokenService(clock, Substitute.For<ITokenRepository>());

    svc.IsExpired(new Token { ExpiresAt = expiry }).Should().BeFalse();

    clock.Advance(Duration.FromSeconds(2));

    svc.IsExpired(new Token { ExpiresAt = expiry }).Should().BeTrue();
}
```

**.NET 8+ projects without NodaTime** — inject `TimeProvider` and provide `FakeTimeProvider` in tests:

```csharp
// Production registration
services.AddSingleton(TimeProvider.System);

// Service constructor
public class TokenService(TimeProvider time, ITokenRepository repo)
{
    public bool IsExpired(Token token) =>
        time.GetUtcNow() > token.ExpiresAt;
}

// Unit test
[Fact]
public void IsExpired_WhenPastExpiry_ReturnsTrue()
{
    var expiry      = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
    var timeProvider = new FakeTimeProvider(expiry + TimeSpan.FromMinutes(1));
    var svc         = new TokenService(timeProvider, Substitute.For<ITokenRepository>());

    svc.IsExpired(new Token { ExpiresAt = expiry }).Should().BeTrue();
}
```

In integration tests, register the fake in `ConfigureTestServices` and expose it from the factory so individual tests can advance the clock and assert on time-sensitive behaviour:

```csharp
// Expose from ApiFactory
public FakeClock Clock { get; } = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0, 0));

// In ConfigureWebHost
services.AddSingleton<IClock>(factory.Clock);

// In a test
factory.Clock.Advance(Duration.FromDays(31));
var response = await _client.GetAsync("/subscriptions/status");
// now assert on what happens after a month has passed
```

---

## Integration Testing

Integration tests exercise the full HTTP pipeline — routing, middleware, model binding, filters, authentication, and actual application logic against a real database. They answer: *does the API behave correctly end-to-end within the process?*

### `WebApplicationFactory<T>`

This boots your real `Program.cs` in-process. You replace specific services (the real database connection, external HTTP clients, background scheduler) via `ConfigureTestServices`, while everything else runs exactly as it does in production:

```csharp
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17")
        .Build();

    public NpgsqlConnection DbConnection { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o =>
                o.UseNpgsql(_db.GetConnectionString()));

            // Replace any external HTTP clients with fakes
            services.AddHttpClient<IExternalServiceClient, ExternalServiceClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new FakeExternalHandler());
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        DbConnection = new NpgsqlConnection(_db.GetConnectionString());
        await DbConnection.OpenAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider
                   .GetRequiredService<AppDbContext>()
                   .Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await DbConnection.DisposeAsync();
        await _db.StopAsync();
    }
}
```

### Choosing a container image

Always pass the image string to the `PostgreSqlBuilder` constructor and pin a specific version. The `latest` tag is explicitly discouraged by Testcontainers' own best-practice documentation — it introduces flakiness as the tag drifts and removes reproducibility from your test runs.

**Plain PostgreSQL.** The `postgres:17-alpine` variant is a reasonable default for test containers. Alpine images are 50–80 MB compressed versus 150–200 MB for the Debian-based default, which meaningfully reduces cold-start time in clean CI environments that don't cache Docker layers between runs. If your CI does cache layers, the difference is negligible after the first pull. The Testcontainers wait strategy uses `pg_isready`, which is present in alpine variants, so there are no compatibility issues.

```csharp
// Debian (default, slightly larger)
private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17").Build();

// Alpine (smaller image, otherwise equivalent for test use)
private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();
```

**PostGIS.** The `postgis/postgis` image now provides both Debian and Alpine variants (e.g. `postgis/postgis:17-3.5` and `postgis/postgis:17-3.5-alpine`). For test containers the alpine variant works, but the Debian variant is more widely tested across PostGIS tooling and has a longer track record. Unless image pull time is a measurable pain point, prefer the Debian variant for PostGIS:

```csharp
private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgis/postgis:17-3.5").Build();
```

Note that when using the PostGIS image, you still need to call `UseNetTopologySuite()` on the Npgsql options in `ConfigureTestServices`, as that is a client-side configuration independent of which server image you use.

### Sharing the factory across test classes

Starting a Docker container per test class is too slow. Use `[CollectionDefinition]` and `[Collection]` to share a single factory instance across all integration test classes, so the container starts once per `dotnet test` run:

```csharp
[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { }

[Collection(nameof(ApiCollection))]
public class OrdersEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostOrder_WithValidPayload_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/orders", new { ProductId = 1, Quantity = 2 });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

### Database reset with Respawn

Rather than restarting the container between tests, use Respawn to reset only what was written:

```csharp
public sealed class DatabaseResetFixture(ApiFactory factory) : IAsyncLifetime
{
    private Respawner _respawner = null!;

    public async Task InitializeAsync()
    {
        _respawner = await Respawner.CreateAsync(factory.DbConnection, new RespawnerOptions
        {
            DbAdapter        = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore   = [new Table("__EFMigrationsHistory")]
        });
    }

    public Task ResetAsync() => _respawner.ResetAsync(factory.DbConnection);
    public Task DisposeAsync() => Task.CompletedTask;
}
```

Call `ResetAsync()` in each test class's `InitializeAsync` so every test starts from a clean slate.

### Authentication in integration tests

Never disable authentication middleware in integration tests — the auth pipeline is worth covering. Instead, generate real JWTs signed with a known test key that you configure in `ConfigureTestServices`:

```csharp
// In ConfigureTestServices
services.Configure<JwtOptions>(o =>
{
    o.SigningKey = "test-signing-key-32-chars-minimum";
    o.Issuer    = "test-issuer";
});

// In a test helper
public static string GenerateToken(string[] roles)
{
    var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-signing-key-32-chars-minimum"));
    var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
    var token  = new JwtSecurityToken(
        issuer: "test-issuer",
        claims: claims,
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials: creds
    );
    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

### A note on database-side logic

If your application uses database-level triggers, generated columns, or other server-side logic (Laraue.EfCoreTriggers, raw SQL migrations with trigger definitions, etc.), those features do not exist in EF Core's in-memory provider and will silently be skipped by SQLite. Any test that needs to observe their effects must run against a real PostgreSQL instance via Testcontainers. This is non-negotiable — false green tests from in-memory providers are worse than no tests.

### OpenAPI document smoke test

If you use the default Microsoft OpenAPI implementation, assert that the schema endpoint is reachable and well-formed. This catches misconfigured document transformers and missing schema registrations before consumers encounter them:

```csharp
[Fact]
public async Task OpenApiDocument_IsWellFormed()
{
    var response = await _client.GetAsync("/openapi/v1.json");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var json = await response.Content.ReadAsStringAsync();
    var act  = () => JsonDocument.Parse(json);
    act.Should().NotThrow();
}
```

---

## End-to-End Testing

E2E tests treat the API as a black box running as a real HTTP server, the same way an actual consumer would. They validate critical user journeys rather than individual endpoints. Keep them few — they are slow and brittle by nature; correctness details belong in unit and integration tests.

For a pure Web API project the most practical approach is HTTP-only E2E: an `HttpClient` driving a fully-wired running instance (started via `docker-compose` in CI, or via the same `WebApplicationFactory` wired to real external dependencies rather than fakes). These tests live in their own project and should not reference any source project internals — the API is treated as a third-party service.

A representative E2E test reads like a user journey:

```
POST /auth/login         → 200, receive token
POST /orders             → 201, receive order id
GET  /orders/{id}        → 200, status = "Pending"
POST /webhooks/payment   → 200  (simulate payment confirmation)
GET  /orders/{id}        → 200, status = "Confirmed"
```

If the API also serves a web frontend, Playwright can drive browser-level E2E. For pure Web API projects, Playwright adds complexity without benefit.

---

## Property-Based Testing with FsCheck

### The core idea

FsCheck shifts how you think about tests. Instead of providing specific example inputs and asserting specific outputs (`[Fact]`, `[Theory]`), you describe an *invariant* — a statement that must hold for **all** valid inputs — and FsCheck generates hundreds of random inputs to try to falsify it.

When FsCheck finds a failure, it **shrinks** the input: it automatically reduces it to the simplest possible value that still causes the failure. So rather than a failing test with a 200-character random string, you get a minimal reproduction like `""` or `-1`.

### The `[Property]` attribute

`FsCheck.Xunit` provides `[Property]`, which replaces `[Fact]`. FsCheck reads the parameter types of the method, generates random values for them, and runs the test body many times (100 by default, configurable). Returning `true` means the property held for that input; returning `false` or throwing is a failure:

```csharp
using FsCheck.Xunit;

public class MoneyProperties
{
    [Property]
    public bool Addition_IsCommutative(decimal a, decimal b)
    {
        var m1 = Money.Of(Math.Abs(a));
        var m2 = Money.Of(Math.Abs(b));
        return (m1 + m2) == (m2 + m1);
    }
}
```

### Custom generators (`Arbitrary<T>`)

FsCheck generates primitives (`int`, `string`, `bool`, `decimal`, etc.) out of the box. For domain types, or for third-party value types like NodaTime, you write a generator:

```csharp
public static class DomainGenerators
{
    private static readonly LocalDate Epoch = new(2000, 1, 1);

    public static Arbitrary<LocalDate> LocalDates() =>
        Gen.Choose(0, 36_524)
           .Select(d => Epoch.PlusDays(d))
           .ToArbitrary();

    public static Arbitrary<DateInterval> DateIntervals() =>
        (from start in LocalDates().Generator
         from length in Gen.Choose(1, 730)
         select new DateInterval(start, start.PlusDays(length)))
        .ToArbitrary();

    // Generates only values the constructor actually accepts —
    // avoids wasting runs on rejection-sampled invalid inputs
    public static Arbitrary<OrderQuantity> ValidQuantities() =>
        Gen.Choose(1, 10_000)
           .Select(OrderQuantity.Of)
           .ToArbitrary();
}
```

Register generators at the class level with `[Properties]` so you don't repeat yourself on every method:

```csharp
[Properties(Arbitrary = new[] { typeof(DomainGenerators) })]
public class OrderProperties
{
    [Property]
    public bool ApplyDiscount_NeverProducesNegativeTotal(OrderQuantity qty, decimal unitPrice)
    {
        var order = new Order(qty, Math.Abs(unitPrice));
        order.ApplyDiscount(0.5m);
        return order.Total >= 0;
    }

    [Property]
    public bool DateInterval_EndIsNeverBeforeStart(DateInterval interval) =>
        interval.End >= interval.Start;
}
```

### Identifying good properties

The hardest part of FsCheck is knowing what to write. Common patterns:

**Round-trip** — encode then decode returns the original:
```csharp
[Property]
public bool Serialize_ThenDeserialize_IsIdentity(MyDto dto)
{
    var json         = JsonSerializer.Serialize(dto, _options);
    var deserialized = JsonSerializer.Deserialize<MyDto>(json, _options);
    return dto == deserialized;
}
```

**Symmetry / commutativity** — `a op b == b op a` where the operation should be order-independent.

**Invariant preservation** — a transformation cannot violate a business rule. A discount application should never push a total negative. A status transition should never skip required states.

**Oracle** — a slow but obviously-correct reference implementation matches your optimised one.

**Idempotence** — applying the operation twice produces the same result as applying it once (useful for normalisation, deduplication, formatting).

### Where property tests live

Property tests sit alongside unit tests conceptually — they test pure logic without HTTP or database. Place them in the same test project as the unit tests for the logic they cover. Focus FsCheck on:

- Domain value objects and their constraints
- Validators (the boundary conditions are exactly what random generation hits naturally)
- Serialisation/deserialisation of DTOs and response types
- Parsing logic
- Any business rule with a large input space that is hard to cover with hand-picked examples

---

## Background Jobs (e.g. Quartz)

If your project uses a job scheduler, separate the job's business logic from its scheduling concerns and test them independently.

**Unit-test the job's logic** by mocking `IJobExecutionContext`. The question is: given the job fires, does it do the right thing?

```csharp
[Fact]
public async Task ImportJob_WhenServiceSucceeds_CompletesWithoutThrowing()
{
    var context = Substitute.For<IJobExecutionContext>();
    var service = Substitute.For<IImportService>();
    service.ProcessAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

    var job = new DataImportJob(service);
    var act = async () => await job.Execute(context);

    await act.Should().NotThrowAsync();
    await service.Received(1).ProcessAsync(Arg.Any<CancellationToken>());
}
```

**Integration-test side effects** by triggering the job through the scheduler in your `WebApplicationFactory` setup and asserting database state afterwards:

```csharp
var schedulerFactory = factory.Services.GetRequiredService<ISchedulerFactory>();
var scheduler        = await schedulerFactory.GetScheduler();
await scheduler.TriggerJob(new JobKey("DataImportJob"));
// allow the job to complete, then assert
```

Configure the scheduler to use an in-memory job store in `ConfigureTestServices` so tests don't interact with each other through persisted trigger state.

---

## Serialisation Round-Trip Tests

If you use `System.Text.Json`, add round-trip property tests for your request and response types. These catch issues with custom converters, third-party type serialisation (NodaTime via `NodaTime.Serialization.SystemTextJson`, NetTopologySuite geometry, etc.), and camelCase naming policy mismatches before they reach consumers:

```csharp
[Properties(Arbitrary = new[] { typeof(DomainGenerators) })]
public class ResponseSerializationProperties
{
    private static readonly JsonSerializerOptions Options = /* your app's configured options */;

    [Property]
    public bool OrderResponse_RoundTrips(OrderResponse response)
    {
        var json         = JsonSerializer.Serialize(response, Options);
        var deserialized = JsonSerializer.Deserialize<OrderResponse>(json, Options);
        return response == deserialized;
    }
}
```

Prioritise types that contain non-primitive fields — date/time types, domain value objects, enums serialised as strings, custom converters.

---

## Summary: What Belongs Where

| Concern | Layer |
|---|---|
| Service logic, pure functions, factories | Unit |
| Validators and their boundary conditions | Unit + FsCheck |
| Domain value objects and invariants | Unit + FsCheck |
| Mapper configuration validity | Unit |
| Serialisation round-trips | FsCheck |
| Full HTTP pipeline, routing, middleware, auth | Integration (`WebApplicationFactory`) |
| EF Core behaviour against real PostgreSQL | Integration (Testcontainers) |
| Database-side logic (triggers, generated columns) | Integration (Testcontainers — no substitute) |
| Background job logic | Unit (mock scheduler context) |
| Background job side-effects | Integration |
| OpenAPI document shape | Integration |
| Critical user journeys end-to-end | E2E |
