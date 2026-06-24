using Humanizer;

namespace Cartwell.Common.Configs;

/// <summary>
///     Laraue trigger configuration for Cartwell.
///     Call functions from Program.cs before building any DbContext.
/// </summary>
public static class LaraueTriggerConfig
{
	private static bool _isConfigured;

	/// <summary>
	///     Configures trigger naming (e.g. after_insert_user_profile).
	///     Idempotent - safe to call multiple times.
	/// </summary>
	public static void ConfigureNamingStrategy()
	{
		if (_isConfigured)
			return; // or throw if you want strict single configuration

		Laraue.Linq2Triggers.Core.Constants.AnnotationKey = "trigger_";
		Laraue.Linq2Triggers.Core.Constants.GetTriggerName = (triggerTime, triggerEvent, triggerEntityType) =>
			$"{triggerTime}_{triggerEvent}_{triggerEntityType.Name}".Underscore();

		_isConfigured = true;
	}
}
