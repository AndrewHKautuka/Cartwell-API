using Cartwell.Common.Configs;
using Cartwell.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace Cartwell.Common;

public class CartwellDbContext(DbContextOptions<CartwellDbContext> options)
	: DbContext(options)
{
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.HasDefaultSchema(DbContextConstants.DatabaseSchema);
		modelBuilder.ApplyEntityTimestamps();

		base.OnModelCreating(modelBuilder);
	}
}
