using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NodaTime;

namespace Cartwell.Common.DocumentTransformers;

public sealed class NodaTimeExamplesSchemaTransformer : IOpenApiSchemaTransformer
{
	public Task TransformAsync(OpenApiSchema schema,
		OpenApiSchemaTransformerContext context,
		CancellationToken cancellationToken)
	{
		var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;

		if (type == typeof(LocalDate))
			schema.Example = "2026-06-12";
		else if (type == typeof(LocalTime))
			schema.Example = "13:45:30";
		else if (type == typeof(LocalDateTime))
			schema.Example = "2026-06-12T13:45:30";
		else if (type == typeof(Instant)
				 || type == typeof(ZonedDateTime)
				 || type == typeof(OffsetDateTime))
			schema.Example = "2026-06-12T13:45:30Z";
		else if (type == typeof(OffsetTime))
			schema.Example = "13:45:30+00:00";
		else if (type == typeof(OffsetDate))
			schema.Example = "2026-06-12+00:00";
		else if (type == typeof(Period))
			schema.Example = "P1Y2M10DT2H30M";
		else if (type == typeof(DateInterval) && schema.Properties is { } dp)
		{
			((OpenApiSchema)dp["Start"]).Example = "2026-06-12";
			((OpenApiSchema)dp["End"]).Example = "2026-06-20";
		}
		else if (type == typeof(Interval) && schema.Properties is { } ip)
		{
			((OpenApiSchema)ip["Start"]).Example = "2026-06-12T13:45:30Z";
			((OpenApiSchema)ip["End"]).Example = "2026-06-20T16:16:31Z";
		}

		return Task.CompletedTask;
	}
}
