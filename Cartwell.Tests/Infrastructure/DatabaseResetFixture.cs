using Respawn;

namespace Cartwell.Tests.Infrastructure;

public sealed class DatabaseResetFixture(ApiFactory apiFactory) : IAsyncLifetime
{
	private Respawner _respawner = null!;

	public async Task InitializeAsync()
	{
		_respawner = await Respawner.CreateAsync(apiFactory.DbConnection,
												 new RespawnerOptions
												 {
													 DbAdapter = DbAdapter.Postgres,
													 SchemasToInclude = ["api"]
												 });
	}

	public Task DisposeAsync()
	{
		return Task.CompletedTask;
	}

	public Task ResetAsync()
	{
		return _respawner.ResetAsync(apiFactory.DbConnection);
	}
}
