using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cartwell.Common;
using Cartwell.Common.Configs;
using Cartwell.Common.DocumentTransformers;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using OpenApi.NodaTime.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var isBuildTimeOpenApiGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

var primaryConnectionString = builder.Configuration.GetConnectionString("Primary");

// Add services to the container.
// Singletons
builder.Services.AddSingleton<IClock>(SystemClock.Instance);

builder.Services.AddControllers()
	   .AddJsonOptions(options =>
	   {
		   options.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
	   });

var healthChecksBuilder = builder.Services.AddHealthChecks();

if (!isBuildTimeOpenApiGeneration)
{
	healthChecksBuilder.AddNpgSql(primaryConnectionString!,
								  name: "postgresql",
								  tags: ["db", "postgres", "ready"])
					   .AddDbContextCheck<CartwellDbContext>("cartwell-dbcontext",
															 tags: ["db", "ef", "ready"]);

	builder.Services.AddDbContext<CartwellDbContext>(options =>
	{
		options.UseConfiguredDbContext(primaryConnectionString);
	});

	builder.Services.AddHealthChecksUI(setup =>
		   {
			   setup.SetEvaluationTimeInSeconds(300); // how often the UI polls
			   setup.MaximumHistoryEntriesPerEndpoint(50);
			   setup.AddHealthCheckEndpoint("API", "/health/ready"); // the endpoint it polls
		   })
		   .AddInMemoryStorage();
}

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi("v1",
							options =>
							{
								options.ConfigureNodaTime();
								options.AddSchemaTransformer<NodaTimeExamplesSchemaTransformer>();
							});

builder.Services.Configure<JsonOptions>(options =>
{
	options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
	options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.MapScalarApiReference(options =>
	{
		options.Title = "Cartwell API - Scalar";
	});
	app.UseSwaggerUI(options =>
	{
		options.SwaggerEndpoint("/openapi/v1.json", "v1");
		options.DocumentTitle = "Cartwell API - Swagger UI";
	});
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health",
					new HealthCheckOptions
					{
						Predicate = _ => false // no dependency checks, just "is the process up"
					});

app.MapHealthChecks("/health/ready",
					new HealthCheckOptions
					{
						Predicate = check => check.Tags.Contains("ready"),
						ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
					});

if (!isBuildTimeOpenApiGeneration)
{
	app.MapHealthChecksUI(options => options.UIPath = "/health-ui");
}

await app.RunAsync();
