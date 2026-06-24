using Cartwell.Common.Types.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NodaTime;

namespace Cartwell.Common.Utils;

public static class StampTimestamps
{
	/// <summary>
	///     Sets <see cref="IUpdateTimestampEntity.UpdatedAt" /> to the current UTC instant on every
	///     tracked entity that is in the <see cref="EntityState.Modified" /> state before a save.
	///     Call this from <c>SaveChanges</c> / <c>SaveChangesAsync</c> overrides in each DbContext.
	/// </summary>
	public static void StampUpdateTimestamps(ChangeTracker changeTracker, IClock clock)
	{
		var now = clock.GetCurrentInstant();

		foreach (var entry in changeTracker.Entries<IUpdateTimestampEntity>()
										   .Where(e => e.State == EntityState.Modified))
		{
			entry.Entity.UpdatedAt = now;
		}
	}
}
