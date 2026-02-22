using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;

namespace BopVisualEffects.Effects.Hsl;

/// <summary>
/// Effect definition for a per-pixel HSL (Hue/Saturation/Lightness) color filter.
/// Rotates hue, scales saturation, and offsets lightness over the full screen.
/// Uses <see cref="Camera.OnRenderImage"/> via <see cref="HslService"/>, which ensures a
/// single overlay component per camera regardless of how many HSL events run concurrently.
/// </summary>
public sealed class HslEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "hsl filter";

	/// <inheritdoc />
	public string DisplayName => "HSL Filter";

	/// <inheritdoc />
	public string ConfigKey => "Hsl";

	/// <inheritdoc />
	public string Description => "Adjusts hue, saturation and lightness of the whole screen for creative colour grading.";

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
				["hue_shift"] = 0.0f,
				["saturation"] = 1.0f,
				["lightness"] = 0.0f,
				["intensity"] = 1.0f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<HslEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var hueShift = entity.GetFloat("hue_shift");
		var saturation = entity.GetFloat("saturation");
		var lightness = entity.GetFloat("lightness");
		var intensity = entity.GetFloat("intensity");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<HslRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, hueShift, saturation, lightness, intensity));
		}
	}

	private sealed class HslRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _hueShift;
		private float _saturation;
		private float _lightness;
		private float _maxIntensity;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private Camera? _camera;
		private HslRequest? _request;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat,
			float hueShift, float saturation, float lightness, float intensity)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_hueShift = Mathf.Clamp(hueShift, -180f, 180f);
			_saturation = Mathf.Max(0f, saturation);
			_lightness = Mathf.Clamp(lightness, -0.5f, 0.5f);
			_maxIntensity = Mathf.Clamp01(intensity);
			InitializeRequest();
		}

		/// <summary>
		/// Stops this effect instance.
		/// </summary>
		public void Stop()
		{
			RemoveRequest();
			Destroy(this);
		}

		private void LateUpdate()
		{
			if (_jukebox is null)
			{
				Stop();
				return;
			}

			if (!_initialized)
			{
				InitializeRequest();
				if (!_initialized)
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

			if (_request != null)
			{
				_request.HueShift = _hueShift;
				_request.Saturation = _saturation;
				_request.Lightness = _lightness;
				_request.Intensity = _maxIntensity * envelope;
			}
		}

		private void OnDisable()
		{
			RemoveRequest();
		}

		private void InitializeRequest()
		{
			Camera? camera = EffectRuntimeController.ResolveEffectCamera(_loader);
			if (camera is null)
				return;

			_camera = camera;
			_request = HslService.AddRequest(camera);
			_request.HueShift = _hueShift;
			_request.Saturation = _saturation;
			_request.Lightness = _lightness;
			_request.Intensity = _maxIntensity;
			_initialized = true;
		}

		private void RemoveRequest()
		{
			if (_camera != null && _request != null)
			{
				HslService.RemoveRequest(_camera, _request);
			}

			_camera = null;
			_request = null;
		}
	}
}

