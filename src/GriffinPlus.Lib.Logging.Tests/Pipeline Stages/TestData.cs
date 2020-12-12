///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
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
				var tagSets = new[] { null, new[] { "Tag" }, new[] { "Tag1", "Tag2" } };

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
