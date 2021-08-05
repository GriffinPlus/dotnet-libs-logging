///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Text.Json.Serialization;

namespace GriffinPlus.Lib.Logging.Elasticsearch
{

	/// <summary>
	/// A container for an Elasticsearch action in a bulk request.
	/// See also: https://www.elastic.co/guide/en/elasticsearch/reference/current/docs-bulk.html#bulk-api-request-body
	/// </summary>
	class BulkRequestAction
	{
		/// <summary>
		/// Gets or sets metadata about an 'index' action.
		/// </summary>
		[JsonPropertyName("index")]
		public BulkRequestAction_Index Index { get; set; }
	}

	/// <summary>
	/// A container for an Elasticsearch 'index' action in a bulk request.
	/// </summary>
	class BulkRequestAction_Index
	{
		/// <summary>
		/// Optional:
		/// Gets or sets the name of the data stream, index, or index alias to perform the action on.
		/// This parameter is required if a &lt;target&gt; is not specified in the request path.
		/// </summary>
		[JsonPropertyName("_index")]
		public string Index { get; set; }

		/// <summary>
		/// Optional:
		/// Gets or sets the document ID.
		/// If no ID is specified, a document ID is automatically generated.
		/// </summary>
		[JsonPropertyName("_id")]
		public string Id { get; set; }

		/// <summary>
		/// Optional:
		/// Gets or sets a value indicating whether the action must target an index alias.
		/// Defaults to <c>false</c>.
		/// </summary>
		[JsonPropertyName("require_alias")]
		public bool? RequireAlias { get; set; }

		/// <summary>
		/// Optional:
		/// Gets or sets a map from the full name of fields to the name of &lt;&lt;dynamic-templates, dynamic templates&gt;.
		/// Defaults to an empty map.
		/// If a name matches a dynamic template, then that template will be applied regardless of other match predicates
		/// defined in the template. And if a field is already defined in the mapping, then this parameter won’t be used.
		/// </summary>
		[JsonPropertyName("dynamic_templates")]
		public object DynamicTemplates { get; set; }
	}

}
