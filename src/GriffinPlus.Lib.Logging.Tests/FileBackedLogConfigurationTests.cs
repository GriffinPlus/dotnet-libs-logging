///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018-2019 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targetting the <see cref="FileBackedLogConfiguration"/> class.
	/// </summary>
	public class FileBackedLogConfigurationTests : LogConfigurationTests_Base<FileBackedLogConfiguration>
	{
		[Fact]
		public override void Saving_Default_Configuration()
		{
			FileBackedLogConfiguration configuration = new FileBackedLogConfiguration();
			Assert.Equal(AppDomain.CurrentDomain.FriendlyName, configuration.ApplicationName);
			configuration.Save();
			Assert.True(File.Exists(configuration.FullPath));
		}
	}
}
