///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://griffin.plus)
//
// Copyright 2018 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using GriffinPlus.Lib.Logging;
using System;
using Xunit;

namespace UnitTests
{
	/// <summary>
	/// Unit tests targetting the <see cref="LogWriter"/> class.
	/// </summary>
	public class LogWriterTests
	{
		private static readonly string LogWriterName = typeof(LogWriterTests).FullName;
		private const string TestMessage = "the quick brown fox jumps over the lazy dog";

		[Fact]
		public void Creating_New_LogWriter_By_Name()
		{
			string name = Guid.NewGuid().ToString("D");
			LogWriter writer = Log.GetWriter(name);
			Assert.Equal(name, writer.Name);
		}

		[Fact]
		public void Creating_New_LogWriter_By_Type_Parameter()
		{
			LogWriter writer = Log.GetWriter(typeof(LogWriterTests));
			Assert.Equal(typeof(LogWriterTests).FullName, writer.Name);
		}

		[Fact]
		public void Creating_New_LogWriter_By_Generic_Type_Parameter()
		{
			LogWriter writer = Log.GetWriter<LogWriterTests>();
			Assert.Equal(typeof(LogWriterTests).FullName, writer.Name);
		}

		[Fact]
		public void LogWriters_Should_be_Singleton_Instances()
		{
			string name = Guid.NewGuid().ToString("D");
			LogWriter writer1 = Log.GetWriter(name);
			LogWriter writer2 = Log.GetWriter(name);
			Assert.Same(writer1, writer2);
		}

	}
}


