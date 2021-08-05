///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace GriffinPlus.Lib.Logging.Elasticsearch
{

	/// <summary>
	/// A container for a response returned by the Elasticsearch bulk endpoint.
	/// </summary>
	[DebuggerDisplay("took = {Took}, errors = {Errors}, items => ({Items})")]
	class BulkResponse
	{
		/// <summary>
		/// Gets or sets how long, in milliseconds, it took to process the bulk request.
		/// </summary>
		[JsonPropertyName("took")]
		public int Took { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether one or more of the operations in the bulk request did
		/// not complete successfully.
		/// </summary>
		[JsonPropertyName("errors")]
		public bool Errors { get; set; }

		/// <summary>
		/// Gets or sets the result of each operation in the bulk request, in the order they were submitted.
		/// </summary>
		[JsonPropertyName("items")]
		public BulkResponse_Item[] Items { get; set; }
	}

	/// <summary>
	/// A container for a result in a bulk request.
	/// </summary>
	[DebuggerDisplay("index => ({Index})")]
	class BulkResponse_Item
	{
		/// <summary>
		/// Gets or sets the result of an 'index' operation.
		/// </summary>
		[JsonPropertyName("index")]
		public BulkResponse_Item_Index Index { get; set; }
	}

	/// <summary>
	/// A container for a result in a bulk request.
	/// </summary>
	[DebuggerDisplay(
		"_index = {Index}, _type = {Type}, _id = {Id}, _version = {Version}, result = {Result}, _shards => ({Shards}), " +
		"_seq_no = {SequenceNumber}, _primary_term = {PrimaryTerm}, status = {Status}, error => ({Error})")]
	class BulkResponse_Item_Index
	{
		/// <summary>
		/// Gets or sets the name of the index associated with the operation.
		/// If the operation targeted a data stream, this is the backing index into which the document was written.
		/// </summary>
		[JsonPropertyName("_index")]
		public string Index { get; set; }

		/// <summary>
		/// Gets or sets the document type associated with the operation.
		/// Elasticsearch indices now support a single document type: _doc.
		/// See Removal of mapping types (https://www.elastic.co/guide/en/elasticsearch/reference/current/removal-of-types.html).
		/// </summary>
		[JsonPropertyName("_type")]
		public string Type { get; set; }

		/// <summary>
		/// Gets or sets the document ID associated with the operation.
		/// </summary>
		[JsonPropertyName("_id")]
		public string Id { get; set; }

		/// <summary>
		/// Gets or sets the document version associated with the operation.
		/// The document version is incremented each time the document is updated.
		/// This parameter is only returned for successful actions.
		/// </summary>
		[JsonPropertyName("_version")]
		public int Version { get; set; }

		/// <summary>
		/// Gets or sets the result of the operation.
		/// Successful values are 'created', 'deleted' and 'updated'.
		/// This parameter is only returned for successful operations.
		/// </summary>
		[JsonPropertyName("result")]
		public string Result { get; set; }

		/// <summary>
		/// Gets or sets shard information for the operation.
		/// This parameter is only returned for successful operations.
		/// </summary>
		[JsonPropertyName("_shards")]
		public BulkResponse_Item_Index_Shards Shards { get; set; }

		/// <summary>
		/// Gets or sets the sequence number assigned to the document for the operation.
		/// Sequence numbers are used to ensure an older version of a document doesn't overwrite a newer version.
		/// See Optimistic concurrency control
		/// (https://www.elastic.co/guide/en/elasticsearch/reference/current/docs-index_.html#optimistic-concurrency-control-index).
		/// This parameter is only returned for successful operations.
		/// </summary>
		[JsonPropertyName("_seq_no")]
		public int SequenceNumber { get; set; }

		/// <summary>
		/// Gets or sets the primary term assigned to the document for the operation.
		/// See Optimistic concurrency control
		/// (https://www.elastic.co/guide/en/elasticsearch/reference/current/docs-index_.html#optimistic-concurrency-control-index).
		/// This parameter is only returned for successful operations.
		/// </summary>
		[JsonPropertyName("_primary_term")]
		public int PrimaryTerm { get; set; }

		/// <summary>
		/// Gets or sets the HTTP status code returned for the operation.
		/// </summary>
		[JsonPropertyName("status")]
		public int Status { get; set; }

		/// <summary>
		/// Gets or sets additional information about the failed operation.
		/// The parameter is only returned for failed operations.
		/// </summary>
		[JsonPropertyName("error")]
		public BulkResponse_Item_Index_Error Error { get; set; }
	}

	/// <summary>
	/// A container for a result in a bulk request.
	/// </summary>
	[DebuggerDisplay("failed = {Failed}, successful = {Successful}, total = {Total}")]
	class BulkResponse_Item_Index_Shards
	{
		/// <summary>
		/// Gets or sets the number of shards the operation attempted to execute on.
		/// </summary>
		[JsonPropertyName("failed")]
		public int Failed { get; set; }

		/// <summary>
		/// Gets or sets the number of shards the operation succeeded on.
		/// </summary>
		[JsonPropertyName("successful")]
		public int Successful { get; set; }

		/// <summary>
		/// Gets or sets the number of shards the operation attempted to execute on but failed.
		/// </summary>
		[JsonPropertyName("total")]
		public int Total { get; set; }
	}

	/// <summary>
	/// A container for a result in a bulk request.
	/// </summary>
	[DebuggerDisplay("type = {Type}, reason = {Reason}, index_uuid = {IndexUuid}, shard = {Shard}, index = {Index}")]
	class BulkResponse_Item_Index_Error
	{
		/// <summary>
		/// Gets or sets the error type for the operation.
		/// </summary>
		[JsonPropertyName("type")]
		public string Type { get; set; }

		/// <summary>
		/// Gets or sets the reason for the failed operation.
		/// </summary>
		[JsonPropertyName("reason")]
		public string Reason { get; set; }

		/// <summary>
		/// Gets or sets the universally unique identifier (UUID) of the index associated with the failed operation.
		/// </summary>
		[JsonPropertyName("index_uuid")]
		public string IndexUuid { get; set; }

		/// <summary>
		/// Gets or sets the ID of the shard associated with the failed operation.
		/// </summary>
		[JsonPropertyName("shard")]
		public string Shard { get; set; }

		/// <summary>
		/// Gets or sets the name of the index associated with the failed operation.
		/// If the operation targeted a data stream, this is the backing index into which the document was attempted to be written.
		/// </summary>
		[JsonPropertyName("index")]
		public string Index { get; set; }

		/// <summary>
		/// Gets the string representation of the error descriptor.
		/// </summary>
		/// <returns>String representation of the error descriptor.</returns>
		public override string ToString()
		{
			return $"Type = {Type}, Reason = {Reason}, Index UUID = {IndexUuid}, Shard = {Shard}, Index = {Index}";
		}
	}

}
