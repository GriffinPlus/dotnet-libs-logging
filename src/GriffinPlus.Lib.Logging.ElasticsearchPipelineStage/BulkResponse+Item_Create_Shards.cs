///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Text.Json;

namespace GriffinPlus.Lib.Logging.Elasticsearch;

partial class BulkResponse
{
	/// <summary>
	/// A container for a result in a bulk request.
	/// </summary>
	[DebuggerDisplay("failed = {" + nameof(Failed) + "}, successful = {" + nameof(Successful) + "}, total = {" + nameof(Total) + "}")]
	public sealed class Item_Create_Shards
	{
		private readonly BulkResponsePool   mPool;
		private          Int32PropertyProxy mFailedProxy;
		private          Int32PropertyProxy mSuccessfulProxy;
		private          Int32PropertyProxy mTotalProxy;

		/// <summary>
		/// Initializes a new instance of the <see cref="Item_Create_Shards"/> class.
		/// </summary>
		/// <param name="pool">The pool managing the response and its resources.</param>
		internal Item_Create_Shards(BulkResponsePool pool)
		{
			mPool = pool;
			Reset(); // init pool references in proxies
		}

		/// <summary>
		/// Gets or sets the number of shards the operation attempted to execute on.
		/// </summary>
		public int Failed => mFailedProxy.Value; // JSON field: 'failed'

		/// <summary>
		/// Gets or sets the number of shards the operation succeeded on.
		/// </summary>
		public int Successful => mSuccessfulProxy.Value; // JSON field: 'successful'

		/// <summary>
		/// Gets or sets the number of shards the operation attempted to execute on but failed.
		/// </summary>
		public int Total => mTotalProxy.Value; // JSON field: 'total'

		/// <summary>
		/// Resets the item for re-use.
		/// </summary>
		public void Reset()
		{
			mFailedProxy = default;
			mSuccessfulProxy = default;
			mTotalProxy = default;
		}

		/// <summary>
		/// Initializes the current instance from a JSON document using the specified JSON reader.
		/// </summary>
		/// <param name="data">The UTF-8 encoded JSON document being deserialized.</param>
		/// <param name="reader">The JSON reader used during deserialization.</param>
		internal void InitFromJson(byte[] data, ref Utf8JsonReader reader)
		{
			Debug.Assert(reader.TokenType == JsonTokenType.StartObject);

			string propertyName = null;

			while (reader.Read())
			{
				switch (reader.TokenType)
				{
					case JsonTokenType.PropertyName:
					{
						propertyName = mPool.GetStringFromUtf8(reader.ValueSpan);
						break;
					}

					case JsonTokenType.Number:
					{
						switch (propertyName)
						{
							case "failed":
								mFailedProxy = new Int32PropertyProxy(data, (int)reader.TokenStartIndex, reader.ValueSpan.Length);
								break;

							case "successful":
								mSuccessfulProxy = new Int32PropertyProxy(data, (int)reader.TokenStartIndex, reader.ValueSpan.Length);
								break;

							case "total":
								mTotalProxy = new Int32PropertyProxy(data, (int)reader.TokenStartIndex, reader.ValueSpan.Length);
								break;
						}

						break;
					}

					case JsonTokenType.EndObject:
					{
						return;
					}

					case JsonTokenType.None:
					case JsonTokenType.StartObject:
					case JsonTokenType.StartArray:
					case JsonTokenType.EndArray:
					case JsonTokenType.Comment:
					case JsonTokenType.String:
					case JsonTokenType.True:
					case JsonTokenType.False:
					case JsonTokenType.Null:
					default:
					{
						// unexpected token, skip!
						reader.Skip();
						break;
					}
				}
			}

			throw new ArgumentException("The reader did not deliver a closing 'EndObject'.");
		}
	}
}
