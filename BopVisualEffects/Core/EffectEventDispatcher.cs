using System;
using HarmonyLib;

namespace BopVisualEffects.Core;

/// <summary>
/// Converts authored mixtape entities into runtime scheduled visual effect actions.
/// </summary>
public static class EffectEventDispatcher
{
	private static readonly AccessTools.FieldRef<MixtapeLoaderCustom, Entity[]> EntitiesRef =
		AccessTools.FieldRefAccess<MixtapeLoaderCustom, Entity[]>("entities");

	/// <summary>
	/// Parses and schedules all Bop Visual Effects entities from the current mixtape.
	/// </summary>
	public static void PrepareEvents(MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass(typeof(EffectEventDispatcher));
		var entities = EntitiesRef(loader);
		var scheduled = 0;

		var effectDefinitionRegistry = EffectDefinitionRegistry.Get();
		foreach (var entity in entities)
		{
			if (!entity.dataModel.StartsWith($"{MyPluginInfo.PLUGIN_GUID}/", StringComparison.Ordinal))
				continue;

			if (effectDefinitionRegistry.TrySchedule(entity, loader))
				scheduled++;
		}

		log.Info($"Prepared visual effect events. Scheduled={scheduled}.");
	}
}
