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
			"_index = {" + nameof(Index) + "}, " +
			"_type = {" + nameof(Type) + "}, " +
			"_id = {" + nameof(Id) + "}, " +
			"_version = {" + nameof(Version) + "}, " +
			"result = {" + nameof(Result) + "}, " +
			"_shards => ({" + nameof(Shards) + "}), " +
			"_seq_no = {" + nameof(SequenceNumber) + "}, " +
			"_primary_term = {" + nameof(PrimaryTerm) + "}, " +
			"status = {" + nameof(Status) + "}, " +
			"error => ({" + nameof(Error) + "})")]
		public sealed class Item_Create
		{
			private readonly BulkResponsePool    mPool;
			private          StringPropertyProxy mIndexProxy;
			private          StringPropertyProxy mTypeProxy;
			private          StringPropertyProxy mIdProxy;
			private          Int32PropertyProxy  mVersionProxy;
			private          StringPropertyProxy mResultProxy;
			private          Int32PropertyProxy  mSequenceNumberProxy;
			private          Int32PropertyProxy  mPrimaryTermProxy;
			private          Int32PropertyProxy  mStatusProxy;

			/// <summary>
			/// Initializes a new instance of the <see cref="Item_Create"/> class.
			/// </summary>
			/// <param name="pool">The pool managing the response and its resources.</param>
			internal Item_Create(BulkResponsePool pool)
			{
				mPool = pool;
				Reset(); // init pool references in proxies
			}

			/// <summary>
			/// Gets or sets the name of the index associated with the operation.
			/// If the operation targeted a data stream, this is the backing index into which the document was written.
			/// </summary>
			public string Index => mIndexProxy.Value; // JSON field: '_index'

			/// <summary>
			/// Gets or sets the document type associated with the operation.
			/// Elasticsearch indices now support a single document type: _doc.
			/// See Removal of mapping types (https://www.elastic.co/guide/en/elasticsearch/reference/current/removal-of-types.html).
			/// </summary>
			public string Type => mTypeProxy.Value; // JSON field: '_type'

			/// <summary>
			/// Gets or sets the document ID associated with the operation.
			/// </summary>
			public string Id => mIdProxy.Value; // JSON field: '_id'

			/// <summary>
			/// Gets or sets the document version associated with the operation.
			/// The document version is incremented each time the document is updated.
			/// This parameter is only returned for successful actions.
			/// </summary>
			public int Version => mVersionProxy.Value; // JSON field: '_version'

			/// <summary>
			/// Gets or sets the result of the operation.
			/// Successful values are 'created', 'deleted' and 'updated'.
			/// This parameter is only returned for successful operations.
			/// </summary>
			public string Result => mResultProxy.Value; // JSON field: 'result'

			/// <summary>
			/// Gets or sets shard information for the operation.
			/// This parameter is only returned for successful operations.
			/// </summary>
			public Item_Create_Shards Shards; // JSON field: '_shards'

			/// <summary>
			/// Gets or sets the sequence number assigned to the document for the operation.
			/// Sequence numbers are used to ensure an older version of a document doesn't overwrite a newer version.
			/// See Optimistic concurrency control
			/// (https://www.elastic.co/guide/en/elasticsearch/reference/current/docs-index_.html#optimistic-concurrency-control-index).
			/// This parameter is only returned for successful operations.
			/// </summary>
			public int SequenceNumber => mSequenceNumberProxy.Value; // JSON field: '_seq_no'

			/// <summary>
			/// Gets or sets the primary term assigned to the document for the operation.
			/// See Optimistic concurrency control
			/// (https://www.elastic.co/guide/en/elasticsearch/reference/current/docs-index_.html#optimistic-concurrency-control-index).
			/// This parameter is only returned for successful operations.
			/// </summary>
			public int PrimaryTerm => mPrimaryTermProxy.Value; // JSON field: '_primary_term'

			/// <summary>
			/// Gets or sets the HTTP status code returned for the operation.
			/// </summary>
			public int Status => mStatusProxy.Value; // JSON field: 'status'

			/// <summary>
			/// Gets or sets additional information about the failed operation.
			/// The parameter is only returned for failed operations.
			/// </summary>
			public Item_Create_Error Error; // JSON field: 'error'

			/// <summary>
			/// Resets the item for re-use.
			/// </summary>
			public void Reset()
			{
				mIndexProxy = new StringPropertyProxy(mPool);
				mTypeProxy = new StringPropertyProxy(mPool);
				mIdProxy = new StringPropertyProxy(mPool);
				mVersionProxy = default;
				mResultProxy = new StringPropertyProxy(mPool);
				mPool.Return(Shards);
				Shards = null;
				mSequenceNumberProxy = default;
				mPrimaryTermProxy = default;
				mStatusProxy = default;
				mPool.Return(Error);
				Error = null;
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
								case "_index":
									mIndexProxy.Update(data, (int)reader.TokenStartIndex + 1, reader.ValueSpan.Length, true);
									break;

								case "_type":
									mTypeProxy.Update(data, (int)reader.TokenStartIndex + 1, reader.ValueSpan.Length, true);
									break;

								case "_id":
									// changes with every document, do not cache!
									mIdProxy.Update(data, (int)reader.TokenStartIndex + 1, reader.ValueSpan.Length, false);
									break;

								case "result":
									mResultProxy.Update(data, (int)reader.TokenStartIndex + 1, reader.ValueSpan.Length, true);
									break;
							}

							break;
						}

						case JsonTokenType.Number:
						{
							switch (propertyName)
							{
								case "_version":
									mVersionProxy = new Int32PropertyProxy(data, (int)reader.TokenStartIndex, reader.ValueSpan.Length);
									break;

								case "_seq_no":
									mSequenceNumberProxy = new Int32PropertyProxy(data, (int)reader.TokenStartIndex, reader.ValueSpan.Length);
									break;

								case "_primary_term":
									mPrimaryTermProxy = new Int32PropertyProxy(data, (int)reader.TokenStartIndex, reader.ValueSpan.Length);
									break;

								case "status":
									mStatusProxy = new Int32PropertyProxy(data, (int)reader.TokenStartIndex, reader.ValueSpan.Length);
									break;
							}

							break;
						}

						case JsonTokenType.StartObject:
						{
							switch (propertyName)
							{
								case "_shards":
								{
									var model = mPool.GetBulkResponseItemIndexShards();
									model.InitFromJson(data, ref reader);
									Shards = model;
									break;
								}

								case "error":
								{
									var model = mPool.GetBulkResponseItemIndexError();
									model.InitFromJson(data, ref reader);
									Error = model;
									break;
								}

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
