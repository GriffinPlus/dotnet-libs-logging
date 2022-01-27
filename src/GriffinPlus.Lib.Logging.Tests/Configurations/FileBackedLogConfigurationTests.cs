///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="FileBackedLogConfiguration"/> class.
	/// </summary>
	public class FileBackedLogConfigurationTests : LogConfigurationTests_Base<FileBackedLogConfiguration>
	{
		[Fact]
		public override void Saving_Default_Configuration()
		{
			// create a new configuration file containing default settings
			string fileName = $"{Guid.NewGuid():N}.gplogconf";
			using (var configuration = new FileBackedLogConfiguration())
			{
				configuration.Path = fileName;
				Assert.Equal(AppDomain.CurrentDomain.FriendlyName, configuration.ApplicationName);
				configuration.Save();
				Assert.True(File.Exists(configuration.FullPath));
			}

			// delete the create configuration
			File.Delete(fileName);
		}
	}

}
