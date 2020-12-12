///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.IO;
using System.Runtime.InteropServices;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A binary writer that is capable of writing plain structs.
	/// </summary>
	class MemoryWriter : BinaryWriter
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MemoryWriter" /> class.
		/// </summary>
		/// <param name="stream">Stream to write to.</param>
		public MemoryWriter(Stream stream)
			: base(stream)
		{
		}

		/// <summary>
		/// Writes a plain struct to the underlying stream (does not do any marshalling!).
		/// </summary>
		/// <typeparam name="T">Type of the struct to read.</typeparam>
		/// <returns>The read struct.</returns>
		public void WriteStruct<T>(T @struct)
		{
			int sizeOfT = Marshal.SizeOf(typeof(T));
			var ptr = Marshal.AllocHGlobal(sizeOfT);
			try
			{
				Marshal.StructureToPtr(@struct, ptr, false);
				var bytes = new byte[sizeOfT];
				Marshal.Copy(ptr, bytes, 0, bytes.Length);
				Write(bytes);
			}
			finally
			{
				Marshal.FreeHGlobal(ptr);
			}
		}
	}

}
