///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


// ReSharper disable PossibleInterfaceMemberAmbiguity

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Interface of log message collections applying a filter to the message set.
	/// </summary>
	/// <typeparam name="TMessage">The log message type.</typeparam>
	public interface IFilteredLogMessageCollection<TMessage> : ILogMessageCollectionCommon<TMessage>
		where TMessage : class, ILogMessage
	{
		/// <summary>
		/// Gets the unfiltered message set.
		/// </summary>
		ILogMessageCollection<TMessage> Unfiltered { get; }
	}

}
