///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="VolatileLogConfiguration" /> class.
	/// </summary>
	public class VolatileLogConfigurationTests : LogConfigurationTests_Base<VolatileLogConfiguration>
	{
		[Fact]
		public override void Saving_Default_Configuration()
		{
			// saving is a no-operation in the LogConfiguration class (no persistence).
			// nevertheless, it should not throw any exception
		}
	}

}
