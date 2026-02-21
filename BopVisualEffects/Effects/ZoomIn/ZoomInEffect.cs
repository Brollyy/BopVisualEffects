using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;

namespace BopVisualEffects.Effects.ZoomIn;

/// <summary>
/// Effect definition for a camera zoom-in that eases to a closer view, holds, then returns to normal.
/// </summary>
public sealed class ZoomInEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "zoom in";

	/// <inheritdoc />
	public string DisplayName => "Zoom In";

	/// <inheritdoc />
	public string ConfigKey => "ZoomIn";

	/// <inheritdoc />
	public string Description => "Eases the camera into a zoomed-in view, holds, then returns to normal — great for building tension.";

	/// <inheritdoc />
	public MixtapeEventTemplate CreateTemplate(string pluginGuid)
	{
		return new MixtapeEventTemplate
		{
			dataModel = $"{pluginGuid}/{Id}",
			length = 2.0f,
			resizable = true,
			properties = new Dictionary<string, object>
			{
				["intensity"] = 0.2f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<ZoomInEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var intensity = entity.GetFloat("intensity");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<ZoomInRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, intensity));
		}
	}

	private sealed class ZoomInRunner : MonoBehaviour
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
			_intensity = Mathf.Clamp(intensity, 0f, 0.99f);
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

			// Ease in over first 20%, hold zoomed in from 20-80%, ease back out over last 20%.
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			float envelope;
			if (progress < 0.2f)
				envelope = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.2f, progress));
			else if (progress > 0.8f)
				envelope = Mathf.SmoothStep(0f, 1f, 1f - Mathf.InverseLerp(0.8f, 1f, progress));
			else
				envelope = 1f;

			// Zoom in: zoomFactor < 1 means smaller camera size = more zoomed in.
			var zoomFactor = Mathf.Clamp(1f - _intensity * envelope, 0.01f, 1f);

			if (_isOrthographic)
				_camera!.orthographicSize = _initialOrthographicSize * zoomFactor;
			else
				_camera!.fieldOfView = _initialFieldOfView * zoomFactor;
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
