using BopVisualEffects.Core;
using HarmonyLib;

namespace BopVisualEffects.Patches;

/// <summary>
/// Ensures the generic category row sees the plugin category before it is formatted.
/// </summary>
[HarmonyPatch(typeof(MixtapeEditorScript), "FormatLevels")]
public static class MixtapeEditorScriptFormatLevelsPatch
{
	/// <summary>
	/// Refreshes the backing category/template dictionaries before the 1.13 layout reads them.
	/// </summary>
	public static void Prefix()
	{
		EffectTemplateManager.RefreshTemplates(
			MyPluginInfo.PLUGIN_GUID,
			ClassLogger.GetForClass(typeof(MixtapeEditorScriptFormatLevelsPatch)));
	}
}
