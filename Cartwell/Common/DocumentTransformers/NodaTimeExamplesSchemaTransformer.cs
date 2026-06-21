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

		return Task.CompletedTask;
	}
}
