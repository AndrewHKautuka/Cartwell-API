using NodaTime;

namespace Cartwell.Common.Types.Models;

public interface IUpdateTimestampEntity
{
	Instant UpdatedAt { get; set; }
}
