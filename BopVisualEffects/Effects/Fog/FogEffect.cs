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

	private sealed class FogRunner : MonoBehaviour
	{
		private float _r;
		private float _g;
		private float _b;
		private float _maxDensity;
		private float _startBeat;
		private float _endBeat;
		private JukeboxScript? _jukebox;

		// Saved original fog state.
		private bool _originalFog;
		private Color _originalFogColor;
		private float _originalFogDensity;
		private FogMode _originalFogMode;

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

			// Save original fog settings and enable fog.
			_originalFog = RenderSettings.fog;
			_originalFogColor = RenderSettings.fogColor;
			_originalFogDensity = RenderSettings.fogDensity;
			_originalFogMode = RenderSettings.fogMode;

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
			RestoreFog();
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
			RestoreFog();
		}

		private void RestoreFog()
		{
			RenderSettings.fog = _originalFog;
			RenderSettings.fogColor = _originalFogColor;
			RenderSettings.fogDensity = _originalFogDensity;
			RenderSettings.fogMode = _originalFogMode;
		}
	}
}
