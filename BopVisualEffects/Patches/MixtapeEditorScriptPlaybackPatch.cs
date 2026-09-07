using System.Collections.Generic;
using System.Reflection;
using BopVisualEffects.Core;
using HarmonyLib;

namespace BopVisualEffects.Patches;

/// <summary>
/// Stops all active visual effect runners when editor playback is stopped.
/// </summary>
[HarmonyPatch]
public static class MixtapeEditorScriptPlaybackPatch
{
	/// <summary>
	/// Targets editor playback control methods where active preview effects should be cleared.
	/// </summary>
	public static IEnumerable<MethodBase> TargetMethods()
	{
		MethodBase? playOrStop = AccessTools.Method(typeof(MixtapeEditorScript), "PlayOrStopMixtape");
		if (playOrStop is not null)
			yield return playOrStop;

		MethodBase? filePause = AccessTools.Method(typeof(MixtapeEditorScript), "FilePause");
		if (filePause is not null)
			yield return filePause;
	}

	/// <summary>
	/// Clears active runners after playback-control actions.
	/// </summary>
	public static void Postfix()
	{
		if (TempoSceneManager.GetActiveSceneKey() != SceneKey.MixtapeEditor)
			return;

		EffectRuntimeController.Instance.StopAllEffects();
	}
}
