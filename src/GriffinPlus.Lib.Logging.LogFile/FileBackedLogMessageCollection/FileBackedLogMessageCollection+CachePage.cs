///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging.Collections;

public partial class FileBackedLogMessageCollection
{
	/// <summary>
	/// A page in the log message cache.
	/// </summary>
	internal class CachePage
	{
		/// <summary>
		/// Id of the first message in the cache page.
		/// </summary>
		public long FirstMessageId;

		/// <summary>
		/// Messages in the cache page.
		/// </summary>
		public readonly List<LogFileMessage> Messages;

		/// <summary>
		/// Initializes a new instance of the <see cref="CachePage"/> class.
		/// </summary>
		/// <param name="firstMessageId">Id of the first message in the cache page.</param>
		/// <param name="capacity">Capacity of the cache page.</param>
		public CachePage(long firstMessageId, int capacity)
		{
			FirstMessageId = firstMessageId;
			Messages = new List<LogFileMessage>(capacity);
		}
	}
}
