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
				["persist_between_minigames"] = false
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<SepiaEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var intensity = entity.GetFloat("intensity");
		var persist = entity.GetBool("persist_between_minigames", false);
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<SepiaRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, intensity, persist));
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
		private bool _persistBetweenMinigames;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float intensity, bool persistBetweenMinigames)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_maxIntensity = Mathf.Clamp01(intensity);
			_persistBetweenMinigames = persistBetweenMinigames;
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

			if (_persistBetweenMinigames)
				RebindRequestIfNeeded();

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

		private void RebindRequestIfNeeded()
		{
			Camera? camera = EffectRuntimeController.ResolveEffectCamera(_loader);
			if (camera is null || _camera == camera)
				return;

			if (_camera is not null && _request is not null)
				SepiaService.RemoveRequest(_camera, _request);

			_camera = camera;
			_request = SepiaService.AddRequest(camera);
			_request.Intensity = _maxIntensity;
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
