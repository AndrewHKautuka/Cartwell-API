using NodaTime;

namespace Cartwell.Common.Types.Models;

public interface ICreateTimestampEntity
{
	Instant CreatedAt { get; init; }
}
