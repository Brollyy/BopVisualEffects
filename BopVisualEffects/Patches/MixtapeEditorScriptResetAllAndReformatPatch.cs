using BopVisualEffects.Core;
using HarmonyLib;

namespace BopVisualEffects.Patches;

/// <summary>
/// Keeps BVE event templates visible after editor reset/reformat actions.
/// </summary>
[HarmonyPatch(typeof(MixtapeEditorScript), "ResetAllAndReformat")]
public static class MixtapeEditorScriptResetAllAndReformatPatch
{
	/// <summary>
	/// Rebuilds plugin-owned templates in the mixtape editor template list.
	/// </summary>
	public static void Postfix()
	{
		var logger = ClassLogger.GetForClass(typeof(MixtapeEditorScriptResetAllAndReformatPatch));
		EffectTemplateManager.RefreshTemplates(MyPluginInfo.PLUGIN_GUID, logger);
	}
}
