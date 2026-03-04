using System.Collections.Generic;
using BopVisualEffects.Core;
using BopVisualEffects.Effects.Zoom;
using UnityEngine;

namespace BopVisualEffects.Effects.ZoomOut;

/// <summary>
/// Effect definition for a camera zoom-out that eases to a wider view, holds, then returns to normal.
/// </summary>
public sealed class ZoomOutEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "zoom out";

	/// <inheritdoc />
	public string DisplayName => "Zoom Out";

	/// <inheritdoc />
	public string ConfigKey => "ZoomOut";

	/// <inheritdoc />
	public string Description => "Eases the camera out to a wider view, holds, then returns to normal — great for revealing the scene.";

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
				["intensity"] = 0.2f,
				["focal_x"] = 0.5f,
				["focal_y"] = 0.5f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<ZoomOutEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var intensity = entity.GetFloat("intensity");
		var focalX = entity.GetFloat("focal_x", 0.5f);
		var focalY = entity.GetFloat("focal_y", 0.5f);
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<ZoomOutRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, intensity, focalX, focalY));
		}
	}

	private sealed class ZoomOutRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _intensity;
		private float _focalX;
		private float _focalY;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private Camera? _camera;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float intensity, float focalX, float focalY)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_intensity = Mathf.Max(0f, intensity);
			_focalX = focalX;
			_focalY = focalY;
			InitializeTargetCamera();
		}

		/// <summary>
		/// Stops this effect instance.
		/// </summary>
		public void Stop()
		{
			if (_camera is not null)
				CameraZoomService.RemoveFactor(_camera, this);
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

			// Ease out over first 20%, hold zoomed out from 20-80%, ease back in over last 20%.
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			float envelope;
			if (progress < 0.2f)
				envelope = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.2f, progress));
			else if (progress > 0.8f)
				envelope = Mathf.SmoothStep(0f, 1f, 1f - Mathf.InverseLerp(0.8f, 1f, progress));
			else
				envelope = 1f;

			// Zoom out: zoomFactor > 1 means larger camera size = more zoomed out.
			var zoomFactor = 1f + _intensity * envelope;

			// Convert normalized screen coordinates [0, 1] to NDC [-1, 1]:
			// (0.5, 0.5) → (0, 0) = screen center (default, backwards-compatible).
			var focalNDC = new Vector2((_focalX - 0.5f) * 2f, (_focalY - 0.5f) * 2f);
			CameraZoomService.SetFactor(_camera!, this, zoomFactor, focalNDC);
		}

		private void OnDisable()
		{
			if (_camera is not null)
				CameraZoomService.RemoveFactor(_camera, this);
		}

		private void InitializeTargetCamera()
		{
			Camera? camera = EffectRuntimeController.ResolveEffectCamera(_loader);
			if (camera is null)
				return;

			_camera = camera;
			_initialized = true;
		}
	}
}
