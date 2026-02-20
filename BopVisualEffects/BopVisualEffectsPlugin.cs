using System.Diagnostics.CodeAnalysis;
using BepInEx;
using BopVisualEffects.Services;
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
	[SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Unity message method invoked by engine.")]
	private void Awake()
	{
		LogService.Initialize(Config, "BVE");

		Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");

		_harmony.PatchAll();
	}
}
