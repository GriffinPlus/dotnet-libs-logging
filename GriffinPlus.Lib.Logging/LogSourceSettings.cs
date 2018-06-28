using System;
using System.Collections.Generic;
using System.Text;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Settings configuring the source part of the logging subsystem.
	/// </summary>
	public class LogSourceSettings
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LogSourceSettings"/> class.
		/// </summary>
		public LogSourceSettings()
		{

		}

		//
		// TODO: Add setting properties here...
		//

		/// <summary>
		/// Gets a value indicating whether the settings are frozen (immutable) or whether they can be modified.
		/// </summary>
		public bool IsFrozen
		{
			get;
			private set;
		}

		/// <summary>
		/// Freezes the settings making the object immutable.
		/// </summary>
		public void Freeze()
		{
			IsFrozen = true;
		}
	}
}
