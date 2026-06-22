using Cartwell.Common.Configs;
using Cartwell.Common.Constants;
using Cartwell.Common.Utils;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Cartwell.Common;

public class CartwellDbContext(
	DbContextOptions<CartwellDbContext> options,
	IClock clock)
	: DbContext(options)
{
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.HasDefaultSchema(DbContextConstants.DatabaseSchema);
		modelBuilder.ApplyEntityTimestamps();

		base.OnModelCreating(modelBuilder);

		modelBuilder.ConfigureCartwellTriggers();
	}

	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		StampTimestamps.StampUpdateTimestamps(ChangeTracker, clock);
		return base.SaveChanges(acceptAllChangesOnSuccess);
	}

	public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
		CancellationToken cancellationToken = default)
	{
		StampTimestamps.StampUpdateTimestamps(ChangeTracker, clock);
		return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
	}
}
