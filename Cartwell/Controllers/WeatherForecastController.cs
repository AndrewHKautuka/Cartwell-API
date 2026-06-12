using Cartwell.Weather.Types;
using Microsoft.AspNetCore.Mvc;
using NodaTime;

namespace Cartwell.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController(IClock clock) : ControllerBase
{
	private static readonly string[] Summaries =
	[
		"Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot",
		"Sweltering", "Scorching"
	];

	[HttpGet(Name = "GetWeatherForecast")]
	public ActionResult<IEnumerable<WeatherForecast>> Get()
	{
		var now = clock.GetCurrentInstant();
		return Enumerable.Range(1, 5)
						 .Select(index => new WeatherForecast
										  {
											  Date = (now + Duration.FromDays(index)).InUtc().Date,
											  TemperatureC = Random.Shared.Next(-20, 55),
											  Summary = Summaries[Random.Shared.Next(Summaries.Length)]
										  })
						 .ToArray();
	}
}
