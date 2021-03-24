///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

namespace LogService
{

	/// <summary>
	/// The service's main program.
	/// </summary>
	public class Program
	{
		/// <summary>
		/// The service's entry point.
		/// </summary>
		/// <param name="args"></param>
		public static void Main(string[] args)
		{
			CreateHostBuilder(args).Build().Run();
		}

		/// <summary>
		/// Creates the host builder.
		/// </summary>
		/// <param name="args">Command line arguments.</param>
		/// <returns></returns>
		public static IHostBuilder CreateHostBuilder(string[] args)
		{
			return Host.CreateDefaultBuilder(args)
				.UseWindowsService()
				.ConfigureLogging(
					(context, logging) =>
					{
						logging.ClearProviders();
						logging.AddConsole();
						logging.AddEventLog(
							new EventLogSettings
							{
								SourceName = "LogService"
							});
					})
				.ConfigureServices(
					(hostContext, services) =>
					{
						services.AddHostedService<LogServiceWorker>();
					})
				.ConfigureWebHostDefaults(
					webBuilder =>
					{
						webBuilder.UseStartup<WebServiceStartup>();
					});
		}
	}

}
