namespace BopVisualEffects.Core;

/// <summary>
/// Contract for a single visual effect definition.
/// One implementation owns both editor template metadata and runtime scheduling behavior.
/// </summary>
public interface IVisualEffectDefinition
{
	/// <summary>
	/// Stable effect id used to build the full mixtape data model key.
	/// Example: "darken screen" => "&lt;PLUGIN_GUID&gt;/darken screen".
	/// </summary>
	string Id { get; }

	/// <summary>
	/// Human-friendly effect name shown in docs and logs.
	/// </summary>
	string DisplayName { get; }

	/// <summary>
	/// Stable PascalCase key used for config file entries (e.g. "CameraShake").
	/// Must be unique across all registered effects.
	/// </summary>
	string ConfigKey { get; }

	/// <summary>
	/// One-line description of what the effect does.
	/// </summary>
	string Description { get; }

	/// <summary>
	/// Builds this effect's editor template for the given plugin category.
	/// </summary>
	/// <param name="pluginGuid">Plugin GUID used as event namespace/category.</param>
	/// <returns>Mixtape event template for this effect.</returns>
	MixtapeEventTemplate CreateTemplate(string pluginGuid);

	/// <summary>
	/// Converts an authored entity instance into a runtime scheduled action.
	/// </summary>
	/// <param name="entity">Mixtape entity being processed.</param>
	/// <param name="loader">Current mixtape loader instance.</param>
	/// <returns>True when the entity was recognized and scheduled; otherwise false.</returns>
	bool TrySchedule(Entity entity, MixtapeLoaderCustom loader);
}
