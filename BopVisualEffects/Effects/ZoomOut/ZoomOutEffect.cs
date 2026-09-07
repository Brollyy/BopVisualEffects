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
				["ease_in"] = true,
				["ease_out"] = true
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<ZoomOutEffect>();
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
			EffectRuntimeController.Instance.SpawnRunner<ZoomOutRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, intensity, easeIn, easeOut));
		}
	}

	private sealed class ZoomOutRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _intensity;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private Camera? _camera;
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
			_intensity = Mathf.Max(0f, intensity);
			_easeIn = easeIn;
			_easeOut = easeOut;
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
			var envelope = EffectEnvelope.Evaluate(progress, _easeIn, _easeOut);

			// Zoom out: zoomFactor > 1 means larger camera size = more zoomed out.
			var zoomFactor = 1f + _intensity * envelope;
			CameraZoomService.SetFactor(_camera!, this, zoomFactor);
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
