///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.IO;
using System.Runtime.InteropServices;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A binary reader that is capable of reading plain structs.
	/// </summary>
	internal class MemoryReader : BinaryReader
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
			var byteLength = Marshal.SizeOf(typeof(T));
			var bytes = ReadBytes(byteLength);
			var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);

			try
			{
				return (T) Marshal.PtrToStructure(pinned.AddrOfPinnedObject(), typeof(T));
			}
			finally
			{
				pinned.Free();
			}
		}
	}
}
