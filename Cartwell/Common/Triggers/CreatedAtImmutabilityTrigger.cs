using Cartwell.Common.Types.Models;
using Laraue.EfCoreTriggers.Common.Extensions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartwell.Common.Triggers;

public sealed class CreatedAtImmutabilityTrigger : GenericTrigger<ICreateTimestampEntity>
{
	public override void ApplyTrigger<TImplEntity>(EntityTypeBuilder<TImplEntity> modelBuilder)
	{
		modelBuilder.BeforeUpdate(trigger => trigger
									  .Action(action => action
														.Condition(refs => refs.Old.CreatedAt != refs.New.CreatedAt)
														.ExecuteRawSql("RAISE EXCEPTION 'created_at is immutable and " +
																	   "cannot be modified';")));
	}
}
