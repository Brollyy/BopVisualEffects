using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;

namespace BopVisualEffects.Effects.Sepia;

/// <summary>
/// Effect definition for a sepia-tone color filter that gives a warm vintage photograph look.
/// Uses a three-pass GL approach: desaturates the image, overlays a warm amber tint, and
/// finishes with a warm sepia multiply — producing a visible amber/brown vintage look at all
/// intensities. Multiple concurrent instances are composited via <see cref="SepiaService"/>.
/// </summary>
public sealed class SepiaEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "sepia";

	/// <inheritdoc />
	public string DisplayName => "Sepia";

	/// <inheritdoc />
	public string ConfigKey => "Sepia";

	/// <inheritdoc />
	public string Description => "Applies a sepia-tone color filter for a warm, vintage photograph aesthetic.";

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
				["intensity"] = 0.8f,
				["ease_in"] = true,
				["ease_out"] = true
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<SepiaEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var intensity = entity.GetFloat("intensity");
		var easeIn = entity.GetBool("ease_in", true);
		var easeOut = entity.GetBool("ease_out", true);
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<SepiaRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, intensity, easeIn, easeOut));
		}
	}

	private sealed class SepiaRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _maxIntensity;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private Camera? _camera;
		private SepiaRequest? _request;
		private bool _easeIn;
		private bool _easeOut;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float intensity, bool easeIn, bool easeOut)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_maxIntensity = Mathf.Clamp01(intensity);
			_easeIn = easeIn;
			_easeOut = easeOut;
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
			var envelope = EffectEnvelope.Evaluate(progress, _easeIn, _easeOut);

			if (_request != null)
				_request.Intensity = _maxIntensity * envelope;
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
			_request = SepiaService.AddRequest(camera);
			_request.Intensity = _maxIntensity;
			_initialized = true;
		}

		private void RemoveRequest()
		{
			if (_camera != null && _request != null)
				SepiaService.RemoveRequest(_camera, _request);

			_camera = null;
			_request = null;
		}
	}
}
