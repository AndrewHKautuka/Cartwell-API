using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Cartwell.Tests.Infrastructure;

namespace Cartwell.Tests.App;

[Collection(nameof(ApiCollection))]
public class OpenApiDocumentTests(ApiFactory apiFactory)
{
	[Fact]
	public async Task OpenApiDocument_IsWellFormed()
	{
		var client = apiFactory.CreateClient();
		var response = await client.GetAsync("/openapi/v1.json");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var json = await response.Content.ReadAsStringAsync();
		var act = () => JsonDocument.Parse(json);
		act.Should().NotThrow();
	}
}
