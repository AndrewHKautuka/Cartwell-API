using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Cartwell.Common.Configs;

public static class DbContextConfig
{
	public static DbContextOptionsBuilder<TContext> UseConfiguredDbContext<TContext>(
		DbContextOptionsBuilder<TContext> builder,
		IConfiguration configuration) where TContext : DbContext
	{
		return builder.UseNpgsql(configuration.GetConnectionString("Primary"),
								 o => o.UseNpgsqlConfig());
	}

	public static DbContextOptionsBuilder UseConfiguredDbContext(
		this DbContextOptionsBuilder builder,
		IConfiguration configuration)
	{
		return builder.UseNpgsql(configuration.GetConnectionString("Primary"),
								 o => o.UseNpgsqlConfig());
	}

	private static NpgsqlDbContextOptionsBuilder UseNpgsqlConfig(this NpgsqlDbContextOptionsBuilder builder)
	{
		return builder.UseNodaTime();
	}
}
