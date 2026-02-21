using System.Diagnostics.CodeAnalysis;
using BepInEx;
using BopVisualEffects.Core;
using HarmonyLib;

namespace BopVisualEffects;

/// <summary>
/// Main BepInEx entrypoint for the Bop Visual Effects mod.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class BopVisualEffectsPlugin : BaseUnityPlugin
{
	/// <summary>
	/// Harmony instance used to apply all runtime patches from this assembly.
	/// </summary>
	private readonly Harmony _harmony = new(MyPluginInfo.PLUGIN_GUID);

	/// <summary>
	/// Unity lifecycle callback invoked by the game engine when the plugin is loaded.
	/// Initializes shared services and applies Harmony patches.
	/// </summary>
	[SuppressMessage(
		"Style",
		"IDE0051:Remove unused private members",
		Justification = "Unity message method invoked by engine.")]
	private void Awake()
	{
		ClassLogger.Initialize(Config, MyPluginInfo.PLUGIN_GUID);
		var pluginLog = ClassLogger.GetForClass<BopVisualEffectsPlugin>();

		EffectRuntimeController.EnsureInstance();
		EffectDefinitionRegistry.Initialize(MyPluginInfo.PLUGIN_GUID, ClassLogger.GetForClass<EffectDefinitionRegistry>());
		EffectTemplateManager.RefreshTemplates(
			MyPluginInfo.PLUGIN_GUID,
			ClassLogger.GetForClass(typeof(EffectTemplateManager)));

		foreach (var effectDescription in EffectTemplateManager.DescribeEffects())
		{
			pluginLog.Info($"Available effect: {effectDescription}");
		}

		_harmony.PatchAll();
		Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
	}
}
