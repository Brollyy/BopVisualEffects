using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BopVisualEffects.Core;

/// <summary>
/// Runtime MonoBehaviour that hosts effect runner components.
/// New effects should add their own runner type without modifying this controller.
/// </summary>
public sealed class EffectRuntimeController : MonoBehaviour
{
	private static EffectRuntimeController? _instance;

	private static readonly AccessTools.FieldRef<MixtapeLoaderCustom, SceneKey[]> SceneKeysRef =
		AccessTools.FieldRefAccess<MixtapeLoaderCustom, SceneKey[]>("sceneKeys");
	private static readonly AccessTools.FieldRef<MixtapeLoaderCustom, Dictionary<SceneKey, GameplayScript>> ScriptsRef =
		AccessTools.FieldRefAccess<MixtapeLoaderCustom, Dictionary<SceneKey, GameplayScript>>("scripts");

	/// <summary>
	/// Singleton runtime instance.
	/// </summary>
	public static EffectRuntimeController Instance
	{
		get
		{
			if (_instance is null || !_instance)
			{
				EnsureInstance();
			}

			return _instance!;
		}
	}

	/// <summary>
	/// Ensures a persistent runtime controller exists.
	/// </summary>
	public static void EnsureInstance()
	{
		if (_instance is not null && _instance)
			return;

		var host = new GameObject("BVE_EffectRuntimeController");
		DontDestroyOnLoad(host);
		_instance = host.AddComponent<EffectRuntimeController>();
	}

	/// <summary>
	/// Spawns and initializes a runner component for a scheduled effect.
	/// </summary>
	public TRunner SpawnRunner<TRunner>(System.Action<TRunner> configure)
		where TRunner : MonoBehaviour
	{
		if (!this)
		{
			return Instance.SpawnRunner(configure);
		}

		var runner = gameObject.AddComponent<TRunner>();
		configure(runner);
		return runner;
	}

	/// <summary>
	/// Stops and removes all active effect runner components.
	/// </summary>
	public void StopAllEffects()
	{
		foreach (MonoBehaviour component in gameObject.GetComponents<MonoBehaviour>())
		{
			if (component == this)
				continue;

			Destroy(component);
		}
	}

	/// <summary>
	/// Resolves the camera to use by the effect.
	/// Uses the currently enabled scene camera from loader scripts.
	/// </summary>
	public static Camera? ResolveEffectCamera(MixtapeLoaderCustom? loader = null)
	{
		if (loader is not null)
		{
			SceneKey[] sceneKeys = SceneKeysRef(loader);
			Dictionary<SceneKey, GameplayScript> scripts = ScriptsRef(loader);
			foreach (SceneKey sceneKey in sceneKeys)
			{
				if (!scripts.TryGetValue(sceneKey, out GameplayScript gameplayScript) || gameplayScript is null)
					continue;

				if (!gameplayScript.cameraScript)
					continue;

				Camera? camera = gameplayScript.cameraScript.GetComponent<Camera>();
				if (camera is not null && camera.enabled)
					return camera;
			}
		}

		return Camera.main;
	}

	/// <summary>
	/// Returns whether the current active scene matches the optional scene filter.
	/// </summary>
	public static bool SceneMatches(string sceneFilter)
	{
		if (string.IsNullOrWhiteSpace(sceneFilter))
			return true;

		var active = TempoSceneManager.GetActiveSceneKey();
		return string.Equals(sceneFilter, active.ToString(), System.StringComparison.OrdinalIgnoreCase);
	}
}
