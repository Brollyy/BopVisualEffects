using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;

namespace BopVisualEffects.Effects.CameraShake;

/// <summary>
/// Effect definition for applying camera shake with configurable duration/amplitude/frequency.
/// </summary>
public sealed class CameraShakeEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "camera-shake";

	/// <inheritdoc />
	public string DisplayName => "Camera Shake";

	/// <inheritdoc />
	public string Description => "Applies temporary camera shake for impact or emphasis.";

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
				["amplitude"] = 0.1f,
				["frequency"] = 18.0f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<CameraShakeEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var amplitude = entity.GetFloat("amplitude");
		var frequency = entity.GetFloat("frequency");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<CameraShakeRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, amplitude, frequency));
		}
	}

	private sealed class CameraShakeRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _amplitude;
		private float _frequency;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private Transform? _targetTransform;
		private Vector3 _initialLocalPosition;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float amplitude, float frequency)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_amplitude = Mathf.Max(0f, amplitude);
			_frequency = Mathf.Max(0.1f, frequency);
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

			var currentBeat = _jukebox.CurrentBeat;
			if (currentBeat >= _endBeat)
			{
				Stop();
				return;
			}

			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			var envelope = 1f - progress;
			if (envelope <= 0f)
			{
				Stop();
				return;
			}

			var timeScale = Time.timeScale;
			var strength = _amplitude * envelope * timeScale;

			var noiseTime = currentBeat * _frequency;
			var x = Mathf.PerlinNoise(noiseTime, 0f) * 2f - 1f;
			var y = Mathf.PerlinNoise(0f, noiseTime) * 2f - 1f;
			Vector3 offset = new(x, y, 0f);

			_targetTransform!.localPosition = _initialLocalPosition + (offset * strength);
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
			_initialLocalPosition = _targetTransform.localPosition;
			_initialized = true;
		}

		private void ResetCameraTransform()
		{
			if (_targetTransform is null)
				return;

			_targetTransform.localPosition = _initialLocalPosition;
		}
	}
}
