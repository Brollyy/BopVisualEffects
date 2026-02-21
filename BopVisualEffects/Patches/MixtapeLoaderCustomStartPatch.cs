using System.Collections;
using HarmonyLib;
using BopVisualEffects.Core;

namespace BopVisualEffects.Patches;

/// <summary>
/// Injects BVE runtime event preparation into the mixtape loader startup flow.
/// </summary>
[HarmonyPatch(typeof(MixtapeLoaderCustom), "Start")]
public static class MixtapeLoaderCustomStartPatch
{
	private static readonly AccessTools.FieldRef<MixtapeLoaderCustom, int> TotalRef =
		AccessTools.FieldRefAccess<MixtapeLoaderCustom, int>("total");

	/// <summary>
	/// Captures the active loader instance for postfix enumerator processing.
	/// </summary>
	public static void Prefix(MixtapeLoaderCustom __instance, out MixtapeLoaderCustom __state)
	{
		__state = __instance;
	}

	/// <summary>
	/// Schedules BVE events once loader initialization passes the first internal stage.
	/// </summary>
	public static IEnumerator Postfix(IEnumerator __result, MixtapeLoaderCustom __state)
	{
		if (__result is null)
			yield break;

		var prepared = false;

		while (__result.MoveNext())
		{
			if (!prepared && TotalRef(__state) > 0)
			{
				EffectEventDispatcher.PrepareEvents(__state);
				prepared = true;
			}

			yield return __result.Current;
		}
	}
}
