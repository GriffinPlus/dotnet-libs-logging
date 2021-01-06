///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.IO;
using System.Runtime.InteropServices;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A binary reader that is capable of reading plain structs.
	/// </summary>
	class MemoryReader : BinaryReader
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MemoryReader"/> class.
		/// </summary>
		/// <param name="stream">Stream to read from.</param>
		public MemoryReader(Stream stream) : base(stream)
		{
		}

		/// <summary>
		/// Reads a plain struct from the underlying stream (does not do any marshalling!).
		/// </summary>
		/// <typeparam name="T">Type of the struct to read.</typeparam>
		/// <returns>The read struct.</returns>
		public T ReadStruct<T>()
		{
			int byteLength = Marshal.SizeOf(typeof(T));
			byte[] bytes = ReadBytes(byteLength);
			var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);

			try
			{
				return (T)Marshal.PtrToStructure(pinned.AddrOfPinnedObject(), typeof(T));
			}
			finally
			{
				pinned.Free();
			}
		}
	}

}
