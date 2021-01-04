///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Interface for classes that support reference counting.
	/// </summary>
	public interface IReferenceManagement
	{
		/// <summary>
		/// Increments the reference counter of the object.
		/// </summary>
		/// <returns>The reference counter after incrementing.</returns>
		int AddRef();

		/// <summary>
		/// Decrements the reference counter of the object.
		/// </summary>
		/// <returns>The reference counter after decrementing.</returns>
		int Release();

		/// <summary>
		/// Gets the current value of the reference counter.
		/// </summary>
		int RefCount { get; }
	}

}
