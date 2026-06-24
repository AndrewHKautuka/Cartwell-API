using Cartwell.Common;
using Cartwell.Common.Configs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using NodaTime.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Cartwell.Tests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
	private readonly PostgreSqlContainer? _db = new PostgreSqlBuilder("postgis/postgis:16-3.4-alpine").Build();

	public NpgsqlConnection DbConnection { get; private set; } = null!;

	public async Task InitializeAsync()
	{
		ArgumentNullException.ThrowIfNull(_db);

		await _db.StartAsync();
		DbConnection = new NpgsqlConnection(_db.GetConnectionString());
		await DbConnection.OpenAsync();

		using var scope = Services.CreateScope();
		await scope.ServiceProvider
				   .GetRequiredService<CartwellDbContext>()
				   .Database.MigrateAsync();
	}

	public new async Task DisposeAsync()
	{
		await DbConnection.DisposeAsync();
		if (_db is not null)
		{
			await _db.StopAsync();
		}
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(_db);

		builder.ConfigureTestServices(services =>
		{
			services.RemoveAll<DbContextOptions<CartwellDbContext>>();
			services.AddDbContext<CartwellDbContext>(o =>
														 o.UseConfiguredDbContext(_db.GetConnectionString()));

			var fakeClock = new FakeClock(Instant.FromUtc(2026,
														  1,
														  1,
														  0,
														  0));

			services.RemoveAll<IClock>();
			services.AddSingleton<IClock>(fakeClock);
		});
	}
}
