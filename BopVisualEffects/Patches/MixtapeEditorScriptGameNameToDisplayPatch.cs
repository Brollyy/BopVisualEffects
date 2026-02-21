using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace BopVisualEffects.Patches;

/// <summary>
/// Maps plugin GUID category names to a user-friendly display name in the mixtape editor.
/// </summary>
[HarmonyPatch(typeof(MixtapeEditorScript), "GameNameToDisplay")]
public static class MixtapeEditorScriptGameNameToDisplayPatch
{
	/// <summary>
	/// Replaces the plugin GUID with plugin display name and skips original for that case.
	/// </summary>
	public static bool Prefix(string name,
		[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Internal name must be exact")]
		ref string __result)
	{
		if (name != MyPluginInfo.PLUGIN_GUID)
			return true;

		__result = MyPluginInfo.PLUGIN_NAME;
		return false;
	}
}
