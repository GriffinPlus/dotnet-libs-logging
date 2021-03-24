///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogService
{

	/// <summary>
	/// The log service worker.
	/// </summary>
	public class LogServiceWorker : BackgroundService
	{
		private readonly ILogger<LogServiceWorker> mLogger;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceWorker"/> class.
		/// </summary>
		/// <param name="logger">The logger the worker should use.</param>
		public LogServiceWorker(ILogger<LogServiceWorker> logger)
		{
			mLogger = logger;
		}

		/// <summary>
		/// Disposes the log service.
		/// </summary>
		public override void Dispose()
		{
		}

		/// <summary>
		/// Starts the log service.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			await base.StartAsync(cancellationToken);
		}

		/// <summary>
		/// Stops the log service.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token that can be signaled to cancel the operation.</param>
		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await base.StopAsync(cancellationToken);
		}

		/// <summary>
		/// Executes the actual work the log service does when running.
		/// </summary>
		/// <param name="stoppingToken">Cancellation token that is signaled when the log service should stop.</param>
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				mLogger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
				await Task.Delay(1000, stoppingToken);
			}
		}
	}

}
