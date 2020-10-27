///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Some data used for unit tests.
	/// </summary>
	public static class TestData
	{
		private static readonly LocalLogMessagePool sMessagePool = new LocalLogMessagePool();

		/// <summary>
		/// Four different local log messages using different log writers and tag sets in lists of different size.
		/// </summary>
		public static IEnumerable<IEnumerable<LocalLogMessage>> LocalLogMessageSet
		{
			get
			{
				var tagSets = new[] { null, new[] { "Tag" }, new [] { "Tag1", "Tag2" } };

				foreach (var tags in tagSets)
				{
					var message1 = sMessagePool.GetMessage(
						DateTimeOffset.Parse("2001-01-01 00:00:00Z"),
						123,
						Log.GetWriter("MyWriter1").WithTags(tags),
						LogLevel.Failure,
						"MyApp1",
						"MyProcess1",
						42,
						"MyText1");

					var message2 = sMessagePool.GetMessage(
						DateTimeOffset.Parse("2002-01-01 00:00:00Z"),
						456,
						Log.GetWriter("MyWriter2").WithTags(tags),
						LogLevel.Error,
						"MyApp2",
						"MyProcess2",
						43,
						"MyText2");

					var message3 = sMessagePool.GetMessage(
						DateTimeOffset.Parse("2003-01-01 00:00:00Z"),
						789,
						Log.GetWriter("MyWriter3").WithTags(tags),
						LogLevel.Warning,
						"MyApp3",
						"MyProcess3",
						44,
						"MyText3");

					var message4 = sMessagePool.GetMessage(
						DateTimeOffset.Parse("2004-01-01 00:00:00Z"),
						789,
						Log.GetWriter("MyWriter4").WithTags(tags),
						LogLevel.Note,
						"MyApp4",
						"MyProcess4",
						44,
						"MyText4");

					yield return new[] { message1 };
					yield return new[] { message1, message2 };
					yield return new[] { message1, message2, message3 };
					yield return new[] { message1, message2, message3, message4 };
				}
			}
		}
	}
}
