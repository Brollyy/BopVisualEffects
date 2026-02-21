using System.Collections.Generic;
using System.Linq;

namespace BopVisualEffects.Core;

/// <summary>
/// Handles registration of Bop Visual Effects templates into the mixtape editor template list.
/// </summary>
public static class EffectTemplateManager
{
	private static readonly string[] PreferredInsertAfterKeys =
	[
		"visual effects",
		"visualeffects",
		"effects"
	];

	/// <summary>
	/// Injects or refreshes this plugin's event templates in the editor template dictionary.
	/// </summary>
	public static void RefreshTemplates(string pluginGuid, ClassLogger log)
	{
		var entities = MixtapeEventTemplates.entities;
		var templates = EffectDefinitionRegistry.Get().BuildTemplates();
		var pluginTemplates = new List<MixtapeEventTemplate>(templates);

		if (entities.ContainsKey(pluginGuid))
		{
			entities[pluginGuid] = pluginTemplates;
		}
		else
		{
			// Prefer inserting right after the Visual Effects category.
			var ordered = entities.ToList();
			int insertIndex = GetPreferredInsertIndex(ordered);
			ordered.Insert(insertIndex, new KeyValuePair<string, List<MixtapeEventTemplate>>(pluginGuid, pluginTemplates));

			entities.Clear();
			foreach (KeyValuePair<string, List<MixtapeEventTemplate>> pair in ordered)
			{
				entities[pair.Key] = pair.Value;
			}
		}

		log.Info($"Refreshed effect templates. Count={templates.Count}.");
	}

	private static int GetPreferredInsertIndex(List<KeyValuePair<string, List<MixtapeEventTemplate>>> ordered)
	{
		for (int i = 0; i < ordered.Count; i++)
		{
			string key = ordered[i].Key;
			foreach (string candidate in PreferredInsertAfterKeys)
			{
				if (string.Equals(key, candidate, System.StringComparison.OrdinalIgnoreCase))
					return i + 1;
			}
		}

		// Fallback: keep Global at index 0 and insert right after it.
		return ordered.Count > 0 ? 1 : 0;
	}

	/// <summary>
	/// Returns human-readable descriptions for currently registered effects.
	/// </summary>
	public static IReadOnlyList<string> DescribeEffects()
	{
		return EffectDefinitionRegistry.Get()
			.Effects
			.Select(e => $"{e.DisplayName}: {e.Description}")
			.ToArray();
	}
}
