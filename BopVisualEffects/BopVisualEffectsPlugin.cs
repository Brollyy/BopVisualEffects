using System.Diagnostics.CodeAnalysis;
using BepInEx;
using BopVisualEffects.Services;
using HarmonyLib;

namespace BopVisualEffects;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class BopVisualEffectsPlugin : BaseUnityPlugin
{
	private ClassLogger _log = null!;
	private readonly Harmony _harmony = new(MyPluginInfo.PLUGIN_GUID);

	[SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Unity message method invoked by engine.")]
	private void Awake()
	{
		LogService.Initialize(Config, MyPluginInfo.PLUGIN_NAME);
		_log = ClassLogger.GetForClass<BopVisualEffectsPlugin>();

		_log.Info(
			$"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded."
		);

		_harmony.PatchAll();
	}
}
