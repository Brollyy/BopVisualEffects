#if SKIP_GAME_REFERENCES
#pragma warning disable CA1050 // Declare types in namespaces — stubs mirror global-namespace game types
#pragma warning disable CA1051 // Do not declare visible instance fields — stubs mirror Unity-serialized public fields
#pragma warning disable CA1822 // Mark members as static — stubs must match real instance method signatures
using System;
using System.Collections.Generic;
using UnityEngine;

public enum SceneKey
{
	Unknown = 0,
	MixtapeEditor = 1
}

public sealed class MixtapeEventTemplate
{
	public string dataModel = string.Empty;
	public float length;
	public bool resizable;
	public Dictionary<string, object> properties = [];
}

public static class MixtapeEventTemplates
{
	public static readonly Dictionary<string, List<MixtapeEventTemplate>> entities = [];
}

public sealed class Entity
{
	public string dataModel = string.Empty;
	public float length;
	public float beat;
	public readonly Dictionary<string, object> properties = [];

	public float GetFloat(string key)
	{
		if (!properties.TryGetValue(key, out object? value) || value is null)
			return 0f;

		return value switch
		{
			float floatValue => floatValue,
			double doubleValue => (float)doubleValue,
			int intValue => intValue,
			_ => 0f
		};
	}
}

public sealed class MixtapeLoaderCustom : MonoBehaviour
{
	public readonly Scheduler scheduler = new();
	public JukeboxScript? jukebox;
}

public sealed class Scheduler
{
	public void Schedule(float beat, Action? callback)
	{
		callback?.Invoke();
	}
}

public sealed class JukeboxScript : MonoBehaviour
{
	public float CurrentBeat { get; set; }
}

public sealed class GameplayScript : MonoBehaviour
{
	public MonoBehaviour? cameraScript;
}

public sealed class MixtapeEditorScript
{
}

public static class TempoSceneManager
{
	public static SceneKey GetActiveSceneKey()
	{
		return SceneKey.Unknown;
	}
}
#endif
