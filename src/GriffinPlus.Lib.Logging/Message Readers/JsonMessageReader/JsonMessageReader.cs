///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Reads a log message written by <see cref="JsonMessageFormatter" /> returning <see cref="LogMessage" />.
	/// </summary>
	public class JsonMessageReader
	{
		private enum State
		{
			Start,
			ReadingObjectKey,
			ExpectingColon,
			ExpectingCommaOrEndOfObject,
			ReadingTimestampValue,
			ReadingHighPrecisionTimestampValue,
			ReadingLogWriterValue,
			ReadingLogLevelValue,
			ReadingTagsValue,
			ReadingTagsValueFirstElement,
			ReadingTagsValueSubsequentElement,
			ReadingTagsValueElementDelimiter,
			ReadingApplicationNameValue,
			ReadingProcessNameValue,
			ReadingProcessIdValue,
			ReadingTextValue
		}

		private readonly JsonTokenizer         mTokenizer  = new JsonTokenizer();
		private readonly Stack<State>          mStateStack = new Stack<State>();
		private          State                 mState      = State.Start;
		private          State                 mReadingValueState;
		private readonly List<ILogMessage>     mCompletedLogMessages = new List<ILogMessage>();
		private          LogMessage            mLogMessage;
		private readonly JsonMessageFieldNames mFieldNames;

		/// <summary>
		/// Initializes a new instance of the <see cref="JsonMessageReader" /> class.
		/// </summary>
		public JsonMessageReader()
		{
			mFieldNames = new JsonMessageFieldNames();
		}

		/// <summary>
		/// Gets the field names defining how to map JSON keys to fields in <see cref="ILogMessage" />.
		/// </summary>
		// ReSharper disable once ConvertToAutoPropertyWhenPossible
		public JsonMessageFieldNames FieldNames => mFieldNames;

		/// <summary>
		/// Gets or sets the format of the timestamp.
		/// </summary>
		public string TimestampFormat { get; set; } = "u";

		/// <summary>
		/// Processes the specified JSON string and returns the corresponding log messages
		/// (the passed data may contain incomplete JSON documents that are completed over multiple calls).
		/// </summary>
		/// <param name="data">JSON string to process.</param>
		/// <returns>The log messages read from the JSON stream.</returns>
		/// <exception cref="JsonMessageReaderException">Reading the log message failed due to a tokenization, parsing or format error.</exception>
		public ILogMessage[] Process(string data)
		{
			mCompletedLogMessages.Clear();

			int remainingTokenCount;
			try
			{
				remainingTokenCount = mTokenizer.Process(data, false);
			}
			catch (TokenizingException ex)
			{
				// transform exception to ease the interface
				throw new JsonMessageReaderException(
					ex.LineNumber,
					ex.Position,
					ex.Message,
					ex);
			}

			while (remainingTokenCount-- > 0)
			{
				var token = mTokenizer.Tokens.Dequeue();

				switch (mState)
				{
					case State.Start:
					{
						if (token.Type != JsonTokenType.LBracket) ThrowUnexpectedTokenException(ref token);
						mStateStack.Push(mState);
						mState = State.ReadingObjectKey;
						mLogMessage = new LogMessage();
						break;
					}

					case State.ReadingObjectKey:
					{
						if (token.Type == JsonTokenType.RBracket)
						{
							// end of object
							mState = mStateStack.Pop();
							if (mState == State.Start) mCompletedLogMessages.Add(mLogMessage);
							break;
						}

						if (token.Type == JsonTokenType.String)
						{
							// a JSON key
							if (token.Token == mFieldNames.Timestamp) mReadingValueState = State.ReadingTimestampValue;
							else if (token.Token == mFieldNames.HighPrecisionTimestamp) mReadingValueState = State.ReadingHighPrecisionTimestampValue;
							else if (token.Token == mFieldNames.LogWriter) mReadingValueState = State.ReadingLogWriterValue;
							else if (token.Token == mFieldNames.LogLevel) mReadingValueState = State.ReadingLogLevelValue;
							else if (token.Token == mFieldNames.Tags) mReadingValueState = State.ReadingTagsValue;
							else if (token.Token == mFieldNames.ApplicationName) mReadingValueState = State.ReadingApplicationNameValue;
							else if (token.Token == mFieldNames.ProcessName) mReadingValueState = State.ReadingProcessNameValue;
							else if (token.Token == mFieldNames.ProcessId) mReadingValueState = State.ReadingProcessIdValue;
							else if (token.Token == mFieldNames.Text) mReadingValueState = State.ReadingTextValue;
							else
							{
								throw new JsonMessageReaderException(
									token.LineNumber,
									token.Position,
									$"'{token.Token}' at ({token.LineNumber},{token.Position}) is not a valid field name.");
							}

							mState = State.ExpectingColon;
							break;
						}

						ThrowUnexpectedTokenException(ref token);
						break;
					}

					case State.ExpectingColon:
					{
						if (token.Type != JsonTokenType.Colon) ThrowUnexpectedTokenException(ref token);
						mState = mReadingValueState;
						break;
					}

					case State.ExpectingCommaOrEndOfObject:
					{
						if (token.Type == JsonTokenType.Comma)
						{
							mState = State.ReadingObjectKey;
							break;
						}

						if (token.Type == JsonTokenType.RBracket)
						{
							mState = mStateStack.Pop();
							if (mState == State.Start) mCompletedLogMessages.Add(mLogMessage);
							break;
						}

						ThrowUnexpectedTokenException(ref token);
						break;
					}

					case State.ReadingTimestampValue:
					{
						if (token.Type != JsonTokenType.String) ThrowUnexpectedTokenException(ref token);
						if (!DateTimeOffset.TryParseExact(token.Token, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var timestamp))
							throw new JsonMessageReaderException(token.LineNumber, token.Position, $"The timestamp ({token.Token}) does not have the expected format.");
						mLogMessage.Timestamp = timestamp;
						mState = State.ExpectingCommaOrEndOfObject;
						break;
					}

					case State.ReadingHighPrecisionTimestampValue:
					{
						if (token.Type != JsonTokenType.Number) ThrowUnexpectedTokenException(ref token);
						if (!long.TryParse(token.Token, out long timestamp))
						{
							throw new JsonMessageReaderException(
								token.LineNumber,
								token.Position,
								$"The high precision timestamp ({token.Token}) does not have the expected format.");
						}

						mLogMessage.HighPrecisionTimestamp = timestamp;
						mState = State.ExpectingCommaOrEndOfObject;
						break;
					}

					case State.ReadingLogWriterValue:
					{
						if (token.Type != JsonTokenType.String) ThrowUnexpectedTokenException(ref token);
						mLogMessage.LogWriterName = token.Token;
						mState = State.ExpectingCommaOrEndOfObject;
						break;
					}

					case State.ReadingLogLevelValue:
					{
						if (token.Type != JsonTokenType.String) ThrowUnexpectedTokenException(ref token);
						mLogMessage.LogLevelName = token.Token;
						mState = State.ExpectingCommaOrEndOfObject;
						break;
					}

					case State.ReadingTagsValue:
					{
						if (token.Type != JsonTokenType.LSquareBracket) ThrowUnexpectedTokenException(ref token);
						mLogMessage.Tags = new TagSet();
						mState = State.ReadingTagsValueFirstElement;
						break;
					}

					case State.ReadingTagsValueFirstElement:
					{
						if (token.Type == JsonTokenType.RSquareBracket)
						{
							// end of tags array
							mState = State.ExpectingCommaOrEndOfObject;
							break;
						}

						if (token.Type == JsonTokenType.String)
						{
							// a tag
							mLogMessage.Tags += token.Token;
							mState = State.ReadingTagsValueElementDelimiter;
							break;
						}

						ThrowUnexpectedTokenException(ref token);
						break;
					}

					case State.ReadingTagsValueElementDelimiter:
					{
						if (token.Type == JsonTokenType.RSquareBracket)
						{
							// end of tags array
							mState = State.ExpectingCommaOrEndOfObject;
							break;
						}

						if (token.Type == JsonTokenType.Comma)
						{
							mState = State.ReadingTagsValueSubsequentElement;
							break;
						}

						ThrowUnexpectedTokenException(ref token);
						break;
					}

					case State.ReadingTagsValueSubsequentElement:
					{
						if (token.Type == JsonTokenType.String)
						{
							// a tag
							mLogMessage.Tags += token.Token;
							mState = State.ReadingTagsValueElementDelimiter;
							break;
						}

						ThrowUnexpectedTokenException(ref token);
						break;
					}

					case State.ReadingApplicationNameValue:
					{
						if (token.Type != JsonTokenType.String) ThrowUnexpectedTokenException(ref token);
						mLogMessage.ApplicationName = token.Token;
						mState = State.ExpectingCommaOrEndOfObject;
						break;
					}

					case State.ReadingProcessNameValue:
					{
						if (token.Type != JsonTokenType.String) ThrowUnexpectedTokenException(ref token);
						mLogMessage.ProcessName = token.Token;
						mState = State.ExpectingCommaOrEndOfObject;
						break;
					}

					case State.ReadingProcessIdValue:
					{
						if (token.Type != JsonTokenType.Number) ThrowUnexpectedTokenException(ref token);
						if (!int.TryParse(token.Token, out int id))
							throw new JsonMessageReaderException(token.LineNumber, token.Position, $"The process id ({token.Token}) does not have the expected format.");
						mLogMessage.ProcessId = id;
						mState = State.ExpectingCommaOrEndOfObject;
						break;
					}

					case State.ReadingTextValue:
					{
						if (token.Type != JsonTokenType.String) ThrowUnexpectedTokenException(ref token);
						mLogMessage.Text = token.Token;
						mState = State.ExpectingCommaOrEndOfObject;
						break;
					}

					default:
						throw new NotImplementedException();
				}
			}

			return mCompletedLogMessages.ToArray();
		}

		/// <summary>
		/// Resets the reader.
		/// </summary>
		public void Reset()
		{
			mTokenizer.Reset();
			mStateStack.Clear();
			mState = State.Start;
			mReadingValueState = State.Start;
			mCompletedLogMessages.Clear();
			mLogMessage = null;
		}

		/// <summary>
		/// Throws a <see cref="JsonMessageReaderException" /> indicating that an unexpected token was found.
		/// </summary>
		/// <param name="token">The unexpected token.</param>
		private static void ThrowUnexpectedTokenException(ref JsonToken token)
		{
			throw new JsonMessageReaderException(
				token.LineNumber,
				token.Position,
				$"Unexpected token '{token.Token}' at ({token.LineNumber},{token.Position}).");
		}
	}

}
