///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Some data used for unit tests.
/// </summary>
public static class TestData
{
	private static readonly LocalLogMessagePool sMessagePool = new();

	/// <summary>
	/// Four different local log messages using different log writers and tag sets in lists of different size.
	/// </summary>
	public static IEnumerable<IEnumerable<LocalLogMessage>> LocalLogMessageSet
	{
		get
		{
			string[][] tagSets = [null, ["Tag"], ["Tag1", "Tag2"]];

			foreach (string[] tags in tagSets)
			{
				LocalLogMessage message1 = sMessagePool.GetMessage(
					DateTimeOffset.Parse("2001-01-01 00:00:00Z"),
					123,
					LogWriter.Get("MyWriter1").WithTags(tags),
					LogLevel.Emergency,
					"MyApp1",
					"MyProcess1",
					42,
					"MyText1");

				LocalLogMessage message2 = sMessagePool.GetMessage(
					DateTimeOffset.Parse("2002-01-01 00:00:00Z"),
					456,
					LogWriter.Get("MyWriter2").WithTags(tags),
					LogLevel.Alert,
					"MyApp2",
					"MyProcess2",
					43,
					"MyText2");

				LocalLogMessage message3 = sMessagePool.GetMessage(
					DateTimeOffset.Parse("2003-01-01 00:00:00Z"),
					789,
					LogWriter.Get("MyWriter3").WithTags(tags),
					LogLevel.Critical,
					"MyApp3",
					"MyProcess3",
					44,
					"MyText3");

				LocalLogMessage message4 = sMessagePool.GetMessage(
					DateTimeOffset.Parse("2004-01-01 00:00:00Z"),
					1230,
					LogWriter.Get("MyWriter4").WithTags(tags),
					LogLevel.Error,
					"MyApp4",
					"MyProcess4",
					44,
					"MyText4");

				LocalLogMessage message5 = sMessagePool.GetMessage(
					DateTimeOffset.Parse("2005-01-01 00:00:00Z"),
					4560,
					LogWriter.Get("MyWriter5").WithTags(tags),
					LogLevel.Warning,
					"MyApp5",
					"MyProcess5",
					55,
					"MyText5");

				LocalLogMessage message6 = sMessagePool.GetMessage(
					DateTimeOffset.Parse("2006-01-01 00:00:00Z"),
					7890,
					LogWriter.Get("MyWriter6").WithTags(tags),
					LogLevel.Notice,
					"MyApp6",
					"MyProcess6",
					66,
					"MyText6");

				yield return
				[
					new LocalLogMessage(message1)
				];

				yield return
				[
					new LocalLogMessage(message1),
					new LocalLogMessage(message2)
				];

				yield return
				[
					new LocalLogMessage(message1),
					new LocalLogMessage(message2),
					new LocalLogMessage(message3)
				];

				yield return
				[
					new LocalLogMessage(message1),
					new LocalLogMessage(message2),
					new LocalLogMessage(message3),
					new LocalLogMessage(message4)
				];

				yield return
				[
					new LocalLogMessage(message1),
					new LocalLogMessage(message2),
					new LocalLogMessage(message3),
					new LocalLogMessage(message4),
					new LocalLogMessage(message5)
				];

				yield return
				[
					new LocalLogMessage(message1),
					new LocalLogMessage(message2),
					new LocalLogMessage(message3),
					new LocalLogMessage(message4),
					new LocalLogMessage(message5),
					new LocalLogMessage(message6)
				];
			}
		}
	}
}
