﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ project (https://griffin.plus)
//
// Copyright(C) 2018  Sascha Falk (ravenpride@griffin.plus)
//
// This program is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General
// Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option)
// any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied
// warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more
// details.
//
// You should have received a copy of the GNU Affero General Public License along with this program.
// If not, see <http://www.gnu.org/licenses/>.
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
			LogWriter writer = LogSource.GetWriter(name);
			Assert.Equal(name, writer.Name);
		}

		/// <summary>
		/// Checks whether the name of a log writer instance matches the type's full name when retrieving it.
		/// </summary>
		[Fact]
		public void CreateNewByTypeParameter()
		{
			LogWriter writer = LogSource.GetWriter(typeof(LogWriterTests));
			Assert.Equal(typeof(LogWriterTests).FullName, writer.Name);
		}

		/// <summary>
		/// Checks whether the name of a log writer instance matches the type's full name when retrieving it.
		/// </summary>
		[Fact]
		public void CreateNewByGenericTypeParameter()
		{
			LogWriter writer = LogSource.GetWriter<LogWriterTests>();
			Assert.Equal(typeof(LogWriterTests).FullName, writer.Name);
		}

		/// <summary>
		/// Checks whether getting a log writer with the same name twice returns the same instance.
		/// </summary>
		[Fact]
		public void EnsureSingletonInstances()
		{
			string name = Guid.NewGuid().ToString("D");
			LogWriter writer1 = LogSource.GetWriter(name);
			LogWriter writer2 = LogSource.GetWriter(name);
			Assert.Same(writer1, writer2);
		}

		/// <summary>
		/// Checks whether writing messages without arguments works as expected.
		/// </summary>
		[Fact]
		public void Write()
		{
			LogWriter writer = LogSource.GetWriter(LogWriterName);
			writer.Write(LogLevel.Note, TestMessage);
		}

	}
}

