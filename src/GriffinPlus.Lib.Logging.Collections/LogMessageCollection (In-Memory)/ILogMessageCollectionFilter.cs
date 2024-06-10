///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// Interface for log message filters that plug into <see cref="LogMessageCollection{TMessage}"/> filter messages
/// provided by the collection.
/// </summary>
/// <typeparam name="TMessage">The type of the log message.</typeparam>
/// <remarks>
/// This interface exists only to ensure that in-memory filters can be used in <see cref="LogMessageCollection{TMessage}"/> only.
/// </remarks>
public interface ILogMessageCollectionFilter<TMessage> : ILogMessageCollectionFilterBase<TMessage>
	where TMessage : class, ILogMessage;
