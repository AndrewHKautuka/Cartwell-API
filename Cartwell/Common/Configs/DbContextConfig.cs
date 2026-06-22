using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Cartwell.Common.Configs;

public static class DbContextConfig
{
	public static DbContextOptionsBuilder<TContext> UseConfiguredDbContext<TContext>(
		DbContextOptionsBuilder<TContext> builder,
		string? connectionString) where TContext : DbContext
	{
		return builder.UseNpgsql(connectionString, o => o.UseNpgsqlConfig());
	}

	public static DbContextOptionsBuilder UseConfiguredDbContext(
		this DbContextOptionsBuilder builder,
		string? connectionString)
	{
		return builder.UseNpgsql(connectionString, o => o.UseNpgsqlConfig());
	}

	private static void UseNpgsqlConfig(this NpgsqlDbContextOptionsBuilder builder)
	{
		builder.UseNodaTime();
	}
}
