///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Text.Json;

namespace GriffinPlus.Lib.Logging.Elasticsearch
{

	partial class BulkResponse
	{
		/// <summary>
		/// A container for a result in a bulk request.
		/// </summary>
		[DebuggerDisplay(
			"type = {" + nameof(Type) + "}, " +
			"reason = {" + nameof(Reason) + "}, " +
			"index_uuid = {" + nameof(IndexUuid) + "}, " +
			"shard = {" + nameof(Shards) + "}, " +
			"index = {" + nameof(Index) + "}")]
		public sealed class Item_Create_Error
		{
			private readonly BulkResponsePool    mPool;
			private          StringPropertyProxy mTypeProxy;
			private          StringPropertyProxy mReasonProxy;
			private          StringPropertyProxy mIndexUuidProxy;
			private          StringPropertyProxy mShardsProxy;
			private          StringPropertyProxy mIndexProxy;

			/// <summary>
			/// Initializes a new instance of the <see cref="Item_Create_Error"/> class.
			/// </summary>
			/// <param name="pool">The pool managing the response and its resources.</param>
			internal Item_Create_Error(BulkResponsePool pool)
			{
				mPool = pool;
				Reset(); // init pool references in proxies
			}

			/// <summary>
			/// Gets or sets the error type for the operation.
			/// </summary>
			public string Type => mTypeProxy.Value; // JSON field: 'type'

			/// <summary>
			/// Gets or sets the reason for the failed operation.
			/// </summary>
			public string Reason => mReasonProxy.Value; // JSON field: 'reason'

			/// <summary>
			/// Gets or sets the universally unique identifier (UUID) of the index associated with the failed operation.
			/// </summary>
			public string IndexUuid => mIndexUuidProxy.Value; // JSON field: 'index_uuid'

			/// <summary>
			/// Gets or sets the ID of the shard associated with the failed operation.
			/// </summary>
			public string Shards => mShardsProxy.Value; // JSON field: 'shards'

			/// <summary>
			/// Gets or sets the name of the index associated with the failed operation.
			/// If the operation targeted a data stream, this is the backing index into which the document was attempted to be written.
			/// </summary>
			public string Index => mIndexProxy.Value; // JSON field: 'index'

			/// <summary>
			/// Resets the item for re-use.
			/// </summary>
			public void Reset()
			{
				mTypeProxy = new StringPropertyProxy(mPool);
				mReasonProxy = new StringPropertyProxy(mPool);
				mIndexUuidProxy = new StringPropertyProxy(mPool);
				mShardsProxy = new StringPropertyProxy(mPool);
				mIndexProxy = new StringPropertyProxy(mPool);
			}

			/// <summary>
			/// Gets the string representation of the error descriptor.
			/// </summary>
			/// <returns>String representation of the error descriptor.</returns>
			public override string ToString()
			{
				return $"Type = {Type}, Reason = {Reason}, Index UUID = {IndexUuid}, Shard = {Shards}, Index = {Index}";
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

						case JsonTokenType.String:
						{
							switch (propertyName)
							{
								case "type":
									mTypeProxy.Update(data, (int)reader.TokenStartIndex + 1, reader.ValueSpan.Length, true);
									break;

								case "reason":
									mReasonProxy.Update(data, (int)reader.TokenStartIndex + 1, reader.ValueSpan.Length, true);
									break;

								case "index_uuid":
									mIndexUuidProxy.Update(data, (int)reader.TokenStartIndex + 1, reader.ValueSpan.Length, true);
									break;

								case "shard":
									mShardsProxy.Update(data, (int)reader.TokenStartIndex + 1, reader.ValueSpan.Length, true);
									break;

								case "index":
									mIndexProxy.Update(data, (int)reader.TokenStartIndex + 1, reader.ValueSpan.Length, true);
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
						case JsonTokenType.Number:
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

}
