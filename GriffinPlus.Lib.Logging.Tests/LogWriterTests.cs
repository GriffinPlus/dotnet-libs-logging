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

using System.Linq;
using System;
using Xunit;
using GriffinPlus.Lib.Logging;

namespace UnitTests
{
	/// <summary>
	/// Tests around the <see cref="LogWriter"/> class.
	/// </summary>
	public class LogWriterTests
	{
		#region Test Data

		private static readonly string LogWriterName = typeof(LogWriterTests).FullName;
		private const string TestMessage = "the quick brown fox jumps over the lazy dog";

		#endregion

		/// <summary>
		/// Checks whether the name of a log writer instance matches the name specified when retrieving it.
		/// </summary>
		[Fact]
		public void CreateNewByName()
		{
			string name = Guid.NewGuid().ToString("D");
			LogWriter writer = Log.GetWriter(name);
			Assert.Equal(name, writer.Name);
		}

		/// <summary>
		/// Checks whether the name of a log writer instance matches the type's full name when retrieving it.
		/// </summary>
		[Fact]
		public void CreateNewByTypeParameter()
		{
			LogWriter writer = Log.GetWriter(typeof(LogWriterTests));
			Assert.Equal(typeof(LogWriterTests).FullName, writer.Name);
		}

		/// <summary>
		/// Checks whether the name of a log writer instance matches the type's full name when retrieving it.
		/// </summary>
		[Fact]
		public void CreateNewByGenericTypeParameter()
		{
			LogWriter writer = Log.GetWriter<LogWriterTests>();
			Assert.Equal(typeof(LogWriterTests).FullName, writer.Name);
		}

		/// <summary>
		/// Checks whether getting a log writer with the same name twice returns the same instance.
		/// </summary>
		[Fact]
		public void EnsureSingletonInstances()
		{
			string name = Guid.NewGuid().ToString("D");
			LogWriter writer1 = Log.GetWriter(name);
			LogWriter writer2 = Log.GetWriter(name);
			Assert.Same(writer1, writer2);
		}

		/// <summary>
		/// Checks whether writing messages without arguments works as expected.
		/// </summary>
		[Fact]
		public void Write()
		{
			LogWriter writer = Log.GetWriter(LogWriterName);
			writer.Write(LogLevel.Note, TestMessage);
		}

	}
}


