using Cartwell.Common.Types.Models;
using Microsoft.EntityFrameworkCore;

namespace Cartwell.Common.Configs;

public static class EntityConfig
{
	public static void ApplyEntityTimestamps(this ModelBuilder modelBuilder)
	{
		modelBuilder.Model.GetEntityTypes()
					.Where(e => typeof(ICreateTimestampEntity).IsAssignableFrom(e.ClrType))
					.Select(entityType => entityType.ClrType)
					.ToList()
					.ForEach(clrType =>
					{
						modelBuilder.Entity(clrType)
									.Property(nameof(ICreateTimestampEntity.CreatedAt))
									.HasDefaultValueSql("CURRENT_TIMESTAMP")
									.ValueGeneratedOnAdd();
					});

		modelBuilder.Model.GetEntityTypes()
					.Where(e => typeof(IUpdateTimestampEntity).IsAssignableFrom(e.ClrType))
					.Select(entityType => entityType.ClrType)
					.ToList()
					.ForEach(clrType =>
					{
						modelBuilder.Entity(clrType)
									.Property(nameof(IUpdateTimestampEntity.UpdatedAt))
									.HasDefaultValueSql("CURRENT_TIMESTAMP")
									.ValueGeneratedOnAddOrUpdate();
					});
	}
}
