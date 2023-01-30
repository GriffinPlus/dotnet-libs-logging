///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

namespace GriffinPlus.Lib.Logging.Elasticsearch
{

	/// <summary>
	/// A container for a response returned by the Elasticsearch bulk endpoint.
	/// </summary>
	[DebuggerDisplay("took = {" + nameof(Took) + "}, errors = {" + nameof(Errors) + "}, items => ({" + nameof(Items) + "})")]
	sealed partial class BulkResponse
	{
		private static readonly JsonReaderOptions sJsonReaderOptions = new JsonReaderOptions
		{
			AllowTrailingCommas = true,
			CommentHandling = JsonCommentHandling.Skip
		};

		private readonly BulkResponsePool     mPool;
		private          Int32PropertyProxy   mTookProxy;
		private          BooleanPropertyProxy mErrorsProxy;

		/// <summary>
		/// Initializes a new instance of the <see cref="BulkResponse"/> class.
		/// </summary>
		/// <param name="pool">The pool managing the response and its resources.</param>
		internal BulkResponse(BulkResponsePool pool)
		{
			mPool = pool;
			Reset(); // init pool references in proxies
		}

		/// <summary>
		/// Gets or sets how long, in milliseconds, it took to process the bulk request.
		/// </summary>
		public int Took => mTookProxy.Value; // JSON field: 'took'

		/// <summary>
		/// Gets or sets a value indicating whether one or more of the operations in the bulk request did not complete
		/// successfully.
		/// </summary>
		public bool Errors => mErrorsProxy.Value; // JSON field: 'errors'

		/// <summary>
		/// Gets or sets the result of each operation in the bulk request, in the order they were submitted.
		/// </summary>
		public List<Item> Items { get; set; } // JSON field: 'items'

		/// <summary>
		/// Returns the response to the pool.
		/// The response must not be used any more afterwards.
		/// </summary>
		public void ReturnToPool()
		{
			mPool.Return(this);
		}

		/// <summary>
		/// Resets the bulk response for re-use.
		/// </summary>
		public void Reset()
		{
			mTookProxy = default;
			mErrorsProxy = default;

			if (Items != null)
			{
				mPool.Return(Items);
				Items = null;
			}
		}

		/// <summary>
		/// Reads the response from specified UTF-8 JSON document and initializes the specified response.
		/// Called by <see cref="BulkResponsePool"/> to initialize the response.
		/// </summary>
		/// <param name="data">JSON document to read.</param>
		/// <returns>
		/// The created response.
		/// Call <see cref="ReturnToPool"/> to release it when you're done.
		/// </returns>
		internal void InitFromJson(byte[] data)
		{
			var reader = new Utf8JsonReader(data, sJsonReaderOptions);
			string propertyName = null;

			// skip the first curly brace that starts all json documents
			reader.Read();
			Debug.Assert(reader.TokenType == JsonTokenType.StartObject);

			while (reader.Read())
			{
				switch (reader.TokenType)
				{
					case JsonTokenType.PropertyName:
					{
						propertyName = mPool.GetStringFromUtf8(reader.ValueSpan);
						break;
					}

					case JsonTokenType.True:
					case JsonTokenType.False:
					{
						switch (propertyName)
						{
							case "errors":
								mErrorsProxy = new BooleanPropertyProxy(data, (int)reader.TokenStartIndex, reader.ValueSpan.Length);
								break;
						}

						break;
					}

					case JsonTokenType.Number:
					{
						switch (propertyName)
						{
							case "took":
								mTookProxy = new Int32PropertyProxy(data, (int)reader.TokenStartIndex, reader.ValueSpan.Length);
								break;
						}

						break;
					}

					case JsonTokenType.StartArray:
					{
						switch (propertyName)
						{
							case "items":
								Items = DeserializeItems(data, ref reader);
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
					case JsonTokenType.StartObject:
					case JsonTokenType.EndArray:
					case JsonTokenType.Comment:
					case JsonTokenType.String:
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

		private List<Item> DeserializeItems(byte[] data, ref Utf8JsonReader reader)
		{
			List<Item> items = mPool.GetListOfBulkResponseItems();

			while (reader.Read())
			{
				switch (reader.TokenType)
				{
					case JsonTokenType.StartObject:
					{
						Item item = mPool.GetBulkResponseItem();
						item.InitFromJson(data, ref reader);
						items.Add(item);
						break;
					}

					case JsonTokenType.EndArray:
					{
						return items;
					}

					case JsonTokenType.None:
					case JsonTokenType.EndObject:
					case JsonTokenType.StartArray:
					case JsonTokenType.PropertyName:
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

			throw new ArgumentException("The reader did not deliver a closing 'EndArray'.");
		}
	}

}
