///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.Elasticsearch;

/// <summary>
/// Authentication Types supported by the <see cref="ElasticsearchPipelineStage"/>.
/// </summary>
[Flags]
public enum AuthenticationScheme
{
	/// <summary>
	/// No authentication.
	/// </summary>
	None = 0,

	/// <summary>
	/// HTTP Basic Authentication (needs username and password).
	/// </summary>
	Basic = 0x0001,

	/// <summary>
	/// HTTP Digest Authentication (needs username, password and domain).
	/// </summary>
	Digest = 0x0002,

	/// <summary>
	/// NTLM Authentication (needs username, password and domain).
	/// </summary>
	Ntlm = 0x0004,

	/// <summary>
	/// Kerberos Authentication (needs username, password and domain).
	/// </summary>
	Kerberos = 0x0008,

	/// <summary>
	/// Negotiate Authentication (needs username, password and domain).
	/// </summary>
	Negotiate = 0x0010,

	/// <summary>
	/// All password based authentication schemes.
	/// </summary>
	PasswordBased = Basic | Digest | Ntlm | Kerberos | Negotiate
}
