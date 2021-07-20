///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Event arguments for events indicating that a setting in a configuration has changed.
	/// </summary>
	public class SettingChangedEventArgs : EventArgs
	{
		// NOTE:
		// Do not transport the value of the setting with the event arguments as fast changes to the setting
		// result in firing the event multiple times - each invocation using a separate worker thread.
		// This can cause a race condition as events can arrive out of order!
		// => Always get the current value from the setting.

		/// <summary>
		/// The default instance of the <see cref="SettingChangedEventArgs"/> class.
		/// </summary>
		public static readonly SettingChangedEventArgs Default = new SettingChangedEventArgs();
	}

}
