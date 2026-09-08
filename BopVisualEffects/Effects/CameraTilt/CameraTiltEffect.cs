using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;

namespace BopVisualEffects.Effects.CameraTilt;

/// <summary>
/// Effect definition for a temporary camera tilt (Z-axis roll) for expressive rhythm accents.
/// </summary>
public sealed class CameraTiltEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "camera tilt";

	/// <inheritdoc />
	public string DisplayName => "Camera Tilt";

	/// <inheritdoc />
	public string ConfigKey => "CameraTilt";

	/// <inheritdoc />
	public string Description => "Tilts the camera briefly on its Z-axis for a swinging, cartoonish rhythm accent.";

	/// <inheritdoc />
	public MixtapeEventTemplate CreateTemplate(string pluginGuid)
	{
		return new MixtapeEventTemplate
		{
			dataModel = $"{pluginGuid}/{Id}",
			length = 1.0f,
			resizable = true,
			properties = new Dictionary<string, object>
			{
				["angle"] = 5.0f,
				["persist_camera"] = false
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<CameraTiltEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var angle = entity.GetFloat("angle");
		var persist = entity.GetBool("persist_camera", false);
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<CameraTiltRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, angle, persist));
		}
	}

	private sealed class CameraTiltRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _angle;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private Transform? _targetTransform;
		private Quaternion _initialLocalRotation;
		private bool _persistCamera;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float angle, bool persistCamera)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_angle = angle;
			_persistCamera = persistCamera;
			InitializeTargetCamera();
		}

		/// <summary>
		/// Stops this effect instance.
		/// </summary>
		public void Stop()
		{
			ResetCameraTransform();
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

			if (_persistCamera)
				RebindCameraIfNeeded();

			var currentBeat = _jukebox.CurrentBeat;
			if (currentBeat >= _endBeat)
			{
				Stop();
				return;
			}

			// Swing tilt: rotate in one direction and back (full sine wave over the duration).
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			var tiltAngle = _angle * Mathf.Sin(progress * Mathf.PI);

			_targetTransform!.localRotation = _initialLocalRotation * Quaternion.Euler(0f, 0f, tiltAngle);
		}

		private void OnDisable()
		{
			ResetCameraTransform();
		}

		private void InitializeTargetCamera()
		{
			Camera? camera = EffectRuntimeController.ResolveEffectCamera(_loader);
			if (camera is null)
				return;

			_targetTransform = camera.transform;
			_initialLocalRotation = _targetTransform.localRotation;
			_initialized = true;
		}

		private void RebindCameraIfNeeded()
		{
			Camera? camera = EffectRuntimeController.ResolveEffectCamera(_loader);
			if (camera is null || _targetTransform == camera.transform)
				return;

			ResetCameraTransform();
			_targetTransform = camera.transform;
			_initialLocalRotation = _targetTransform.localRotation;
		}

		private void ResetCameraTransform()
		{
			if (_targetTransform is null)
				return;

			_targetTransform.localRotation = _initialLocalRotation;
		}
	}
}
