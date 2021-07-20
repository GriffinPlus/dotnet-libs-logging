// ReSharper disable UnusedMemberInSuper.Global

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Interface for a setting in the configuration of a pipeline specific stage.
	/// A raw setting is the setting in its string representation.
	/// </summary>
	public interface IProcessingPipelineStageRawSetting
	{
		/// <summary>
		/// Gets the configuration the setting belongs to.
		/// </summary>
		IProcessingPipelineStageConfiguration Configuration { get; }

		/// <summary>
		/// Gets the name of the setting.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Gets a value indicating whether the setting has valid value (true) or just its default value (false).
		/// </summary>
		bool HasValue { get; }

		/// <summary>
		/// Gets the default value of the setting.
		/// </summary>
		string DefaultValue { get; }

		/// <summary>
		/// Gets or sets the value of the setting.
		/// </summary>
		string Value { get; set; }
	}

}
