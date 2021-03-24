///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LogService
{

	/// <summary>
	/// The startup class of the web service.
	/// </summary>
	public class WebServiceStartup
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="WebServiceStartup"/> class.
		/// </summary>
		/// <param name="configuration">The configuration the service should use.</param>
		public WebServiceStartup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		/// <summary>
		/// Gets the configuration the web service uses.
		/// </summary>
		public IConfiguration Configuration { get; }

		/// <summary>
		/// This method gets called by the runtime.
		/// Use this method to add services to the container.
		/// </summary>
		/// <param name="services">The service container.</param>
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddControllers();
			services.AddSwaggerGen(
				options =>
				{
					options.SwaggerDoc("v1", new OpenApiInfo { Title = "Griffin+ Logging Service", Version = "v1" });
				});
		}

		/// <summary>
		/// This method gets called by the runtime.
		/// Use this method to configure the HTTP request pipeline. 
		/// </summary>
		/// <param name="app">The application builder.</param>
		/// <param name="env">The web host environment.</param>
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
				app.UseSwagger();
				app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Griffin+ Logging Service v1"));
			}

			// app.UseHttpsRedirection();
			app.UseRouting();
			// app.UseAuthorization();
			app.UseEndpoints(
				endpoints =>
				{
					endpoints.MapControllers();
				});
		}
	}

}
