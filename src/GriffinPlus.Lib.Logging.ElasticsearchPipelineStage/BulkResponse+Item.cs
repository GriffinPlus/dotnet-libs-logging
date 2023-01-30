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
		[DebuggerDisplay("create => ({" + nameof(Create) + "})")]
		public sealed class Item
		{
			private readonly BulkResponsePool mPool;

			/// <summary>
			/// Initializes a new instance of the <see cref="BulkResponse.Item"/> class.
			/// </summary>
			/// <param name="pool">The pool managing the response and its resources.</param>
			internal Item(BulkResponsePool pool)
			{
				mPool = pool;
				Reset(); // init pool references in proxies
			}

			/// <summary>
			/// Gets or sets the result of an 'create' operation.
			/// </summary>
			public Item_Create Create; // JSON field: 'create'

			/// <summary>
			/// Resets the item for re-use.
			/// </summary>
			public void Reset()
			{
				mPool.Return(Create);
				Create = null;
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

						case JsonTokenType.StartObject:
						{
							switch (propertyName)
							{
								case "create":
									Create = mPool.GetBulkResponseItemCreate();
									Create.InitFromJson(data, ref reader);
									break;

								default:
									// unexpected property, skip all children!
									reader.Skip();
									break;
							}

							break;
						}

						case JsonTokenType.EndObject:
						{
							return;
						}

						case JsonTokenType.None:
						case JsonTokenType.StartArray:
						case JsonTokenType.EndArray:
						case JsonTokenType.Comment:
						case JsonTokenType.String:
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
