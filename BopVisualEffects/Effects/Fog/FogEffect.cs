using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;

namespace BopVisualEffects.Effects.Fog;

/// <summary>
/// Effect definition for a scene fog overlay that fades in and out over its duration.
/// </summary>
public sealed class FogEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "fog";

	/// <inheritdoc />
	public string DisplayName => "Fog";

	/// <inheritdoc />
	public string Description => "Gradually fades a scene fog effect in and back out for atmosphere and visual depth.";

	/// <inheritdoc />
	public MixtapeEventTemplate CreateTemplate(string pluginGuid)
	{
		return new MixtapeEventTemplate
		{
			dataModel = $"{pluginGuid}/{Id}",
			length = 4.0f,
			resizable = true,
			properties = new Dictionary<string, object>
			{
				["r"] = 0.8f,
				["g"] = 0.8f,
				["b"] = 0.9f,
				["density"] = 0.03f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<FogEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var r = entity.GetFloat("r");
		var g = entity.GetFloat("g");
		var b = entity.GetFloat("b");
		var density = entity.GetFloat("density");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<FogRunner>(runner =>
				runner.Initialize(loader.jukebox, startBeat, endBeat, r, g, b, density));
		}
	}

	/// <summary>
	/// Manages shared RenderSettings fog state across potentially overlapping FogRunner instances.
	/// Saves the pre-effect fog state when the first runner activates and restores it only when the last runner deactivates.
	/// </summary>
	private static class FogStateController
	{
		private static int _activeCount;
		private static bool _savedFog;
		private static Color _savedFogColor;
		private static float _savedFogDensity;
		private static FogMode _savedFogMode;

		/// <summary>
		/// Called by a FogRunner on activation. Saves pre-effect fog state on the first activation.
		/// </summary>
		public static void Activate()
		{
			if (_activeCount == 0)
			{
				_savedFog = RenderSettings.fog;
				_savedFogColor = RenderSettings.fogColor;
				_savedFogDensity = RenderSettings.fogDensity;
				_savedFogMode = RenderSettings.fogMode;
			}

			_activeCount++;
		}

		/// <summary>
		/// Called by a FogRunner on deactivation. Restores pre-effect fog state when the last runner deactivates.
		/// </summary>
		public static void Deactivate()
		{
			if (_activeCount <= 0)
				return;

			_activeCount--;
			if (_activeCount == 0)
			{
				RenderSettings.fog = _savedFog;
				RenderSettings.fogColor = _savedFogColor;
				RenderSettings.fogDensity = _savedFogDensity;
				RenderSettings.fogMode = _savedFogMode;
			}
		}
	}

	private sealed class FogRunner : MonoBehaviour
	{
		private float _r;
		private float _g;
		private float _b;
		private float _maxDensity;
		private float _startBeat;
		private float _endBeat;
		private JukeboxScript? _jukebox;
		private bool _activated;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(JukeboxScript? jukebox, float startBeat, float endBeat, float r, float g, float b, float density)
		{
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_r = Mathf.Clamp01(r);
			_g = Mathf.Clamp01(g);
			_b = Mathf.Clamp01(b);
			_maxDensity = Mathf.Max(0f, density);

			// Register with the shared controller before touching RenderSettings.
			FogStateController.Activate();
			_activated = true;

			RenderSettings.fog = true;
			RenderSettings.fogMode = FogMode.ExponentialSquared;
			RenderSettings.fogColor = new Color(_r, _g, _b);
			RenderSettings.fogDensity = 0f;
		}

		/// <summary>
		/// Stops this effect instance.
		/// </summary>
		public void Stop()
		{
			ReleaseFog();
			Destroy(this);
		}

		private void LateUpdate()
		{
			if (_jukebox is null)
			{
				Stop();
				return;
			}

			var currentBeat = _jukebox.CurrentBeat;
			if (currentBeat >= _endBeat)
			{
				Stop();
				return;
			}

			// Fade in over first 20%, hold, fade out over last 20%.
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			float envelope;
			if (progress < 0.2f)
				envelope = Mathf.InverseLerp(0f, 0.2f, progress);
			else if (progress > 0.8f)
				envelope = 1f - Mathf.InverseLerp(0.8f, 1f, progress);
			else
				envelope = 1f;

			RenderSettings.fogDensity = _maxDensity * envelope;
		}

		private void OnDisable()
		{
			ReleaseFog();
		}

		private void ReleaseFog()
		{
			if (!_activated)
				return;

			_activated = false;
			FogStateController.Deactivate();
		}
	}
}
