///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{

	public class EventManagerEventArgs : EventArgs
	{
		public EventManagerEventArgs(string myString)
		{
			MyString = myString;
		}

		public string MyString { get; }
	}

}
