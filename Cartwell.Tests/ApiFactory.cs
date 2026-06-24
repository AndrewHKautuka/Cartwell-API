using Cartwell.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Cartwell.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
	// private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
	// 										   .WithImage(
	// 											   "postgres:16") // use postgis/postgis:16-3.4 if you need PostGIS
	// 										   .Build();

	public NpgsqlConnection DbConnection { get; } = null!;

	public async Task InitializeAsync()
	{
		// await _db.StartAsync();
		// DbConnection = new NpgsqlConnection(_db.GetConnectionString());
		// await DbConnection.OpenAsync();

		using var scope = Services.CreateScope();
		await scope.ServiceProvider
				   .GetRequiredService<CartwellDbContext>()
				   .Database.MigrateAsync();
	}

	public new async Task DisposeAsync()
	{
		await DbConnection.DisposeAsync();
		// await _db.StopAsync();
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureTestServices(services =>
		{
			services.RemoveAll<DbContextOptions<CartwellDbContext>>();
			// services.AddDbContext<CartwellDbContext>(o => o.UseNpgsql(_db.GetConnectionString()));
		});
	}
}
