using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;

namespace BopVisualEffects.Effects.ZoomPulse;

/// <summary>
/// Effect definition for a quick camera zoom-in/out pulse for beat accents or drops.
/// </summary>
public sealed class ZoomPulseEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "zoom pulse";

	/// <inheritdoc />
	public string DisplayName => "Zoom Pulse";

	/// <inheritdoc />
	public string Description => "Briefly zooms the camera in and back out for a punchy, cartoonish accent.";

	/// <inheritdoc />
	public MixtapeEventTemplate CreateTemplate(string pluginGuid)
	{
		return new MixtapeEventTemplate
		{
			dataModel = $"{pluginGuid}/{Id}",
			length = 0.5f,
			resizable = true,
			properties = new Dictionary<string, object>
			{
				["intensity"] = 0.15f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<ZoomPulseEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var intensity = entity.GetFloat("intensity");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<ZoomPulseRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, intensity));
		}
	}

	private sealed class ZoomPulseRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _intensity;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private Camera? _camera;
		private float _initialOrthographicSize;
		private float _initialFieldOfView;
		private bool _isOrthographic;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float intensity)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_intensity = Mathf.Max(0f, intensity);
			InitializeTargetCamera();
		}

		/// <summary>
		/// Stops this effect instance.
		/// </summary>
		public void Stop()
		{
			ResetCamera();
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
				InitializeTargetCamera();
				if (!_initialized)
					return;
			}

			var currentBeat = _jukebox.CurrentBeat;
			if (currentBeat >= _endBeat)
			{
				Stop();
				return;
			}

			// Zoom in quickly then ease back out (attack/decay envelope shaped like a triangle peaking at 30%).
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			var envelope = progress < 0.3f
				? Mathf.InverseLerp(0f, 0.3f, progress)
				: 1f - Mathf.InverseLerp(0.3f, 1f, progress);

			var zoomDelta = _intensity * envelope;

			if (_isOrthographic)
			{
				_camera!.orthographicSize = _initialOrthographicSize * (1f - zoomDelta);
			}
			else
			{
				_camera!.fieldOfView = _initialFieldOfView * (1f - zoomDelta);
			}
		}

		private void OnDisable()
		{
			ResetCamera();
		}

		private void InitializeTargetCamera()
		{
			Camera? camera = EffectRuntimeController.ResolveEffectCamera(_loader);
			if (camera is null)
				return;

			_camera = camera;
			_isOrthographic = camera.orthographic;
			_initialOrthographicSize = camera.orthographicSize;
			_initialFieldOfView = camera.fieldOfView;
			_initialized = true;
		}

		private void ResetCamera()
		{
			if (_camera is null)
				return;

			if (_isOrthographic)
				_camera.orthographicSize = _initialOrthographicSize;
			else
				_camera.fieldOfView = _initialFieldOfView;
		}
	}
}
