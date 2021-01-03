///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Specialized;
using System.ComponentModel;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Extension methods for the <see cref="ILogMessageCollection{TMessage}" /> interface.
	/// </summary>
	public static class LogMessageCollectionEventWatcherExtensions
	{
		/// <summary>
		/// Creates an event watcher for the <see cref="INotifyCollectionChanged.CollectionChanged" /> and the
		/// <see cref="INotifyPropertyChanged.PropertyChanged" /> event that assists with checking whether these
		/// events were called or not.
		/// </summary>
		/// <param name="collection">Collection to attach the watcher to.</param>
		/// <returns>The registered event watcher.</returns>
		public static LogMessageCollectionEventWatcher AttachEventWatcher(this ILogMessageCollection<LogMessage> collection)
		{
			return new LogMessageCollectionEventWatcher(collection);
		}
	}

}
