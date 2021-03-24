///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogService.Controllers
{

	/// <summary>
	/// The controller for registering and unregistering log sources.
	/// </summary>
	[ApiController]
	[Route("api/[controller]")]
	public class LogSourceController : ControllerBase
	{
		private readonly ILogger<LogSourceController> mLogger;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogSourceController"/> class.
		/// </summary>
		/// <param name="logger">The logger the controller should use.</param>
		public LogSourceController(ILogger<LogSourceController> logger)
		{
			mLogger = logger;
		}

		/// <summary>
		/// Registers a log source.
		/// </summary>
		/// <param name="request">Information about the log source to register.</param>
		/// <returns>The response.</returns>
		[HttpPost]
		public ActionResult<LogSourceModel> Post([FromBody] RegisterLogSourceRequest request)
		{
			LogSourceModel response = new LogSourceModel();
			return CreatedAtAction(nameof(GetLogSource), new { id = "" }, response);
		}

		/// <summary>
		/// Gets information about the log source with the specified id.
		/// </summary>
		/// <param name="id">A log source id (as retrieved when registering the log source).</param>
		/// <returns>The response.</returns>
		[HttpGet("{id}")]
		public ActionResult<LogSourceModel> GetLogSource([FromRoute] string id)
		{
			LogSourceModel response = new LogSourceModel();
			return Ok(response);
		}


		//[HttpGet]
		//public IEnumerable<RegisterLogSourceRequest> Get()
		//{
		//	var rng = new Random();
		//	return Enumerable.Range(1, 5)
		//		.Select(
		//			index => new RegisterLogSourceRequest
		//			{
		//				Date = DateTime.Now.AddDays(index),
		//				TemperatureC = rng.Next(-20, 55),
		//				Summary = Summaries[rng.Next(Summaries.Length)]
		//			})
		//		.ToArray();
		//}
	}

}
