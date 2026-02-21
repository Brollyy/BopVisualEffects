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
	public string Id => "camera shake";

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
		private CameraShakeManager? _manager;
		private System.Func<Vector3>? _offsetProvider;

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
			TryRegisterWithManager();
		}

		/// <summary>
		/// Stops this effect instance.
		/// </summary>
		public void Stop()
		{
			Deregister();
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
				TryRegisterWithManager();
				if (!_initialized)
					return;
			}

			if (_jukebox.CurrentBeat >= _endBeat)
			{
				Stop();
				return;
			}
		}

		private void OnDisable()
		{
			Deregister();
		}

		private void TryRegisterWithManager()
		{
			Camera? camera = EffectRuntimeController.ResolveEffectCamera(_loader);
			if (camera is null)
				return;

			_offsetProvider = ComputeOffset;
			_manager = CameraShakeManager.GetOrCreate(camera);
			_manager.Register(_offsetProvider);
			_initialized = true;
		}

		private void Deregister()
		{
			if (_offsetProvider is null || _manager is null || !_manager)
				return;

			_manager.Deregister(_offsetProvider);
			_offsetProvider = null;
		}

		private Vector3 ComputeOffset()
		{
			if (_jukebox is null)
				return Vector3.zero;

			var currentBeat = _jukebox.CurrentBeat;
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			var envelope = 1f - progress;
			if (envelope <= 0f)
				return Vector3.zero;

			var strength = _amplitude * envelope * Time.timeScale;
			var noiseTime = currentBeat * _frequency;
			var x = Mathf.PerlinNoise(noiseTime, 0f) * 2f - 1f;
			var y = Mathf.PerlinNoise(0f, noiseTime) * 2f - 1f;
			return new Vector3(x, y, 0f) * strength;
		}
	}

	/// <summary>
	/// Manages the aggregate camera shake for a single camera.
	/// Captures the camera's baseline position once at creation and accumulates
	/// offset contributions from all active <see cref="CameraShakeRunner"/> instances,
	/// restoring the baseline when all shakes complete.
	/// </summary>
	private sealed class CameraShakeManager : MonoBehaviour
	{
		private Vector3 _baseline;
		private readonly List<System.Func<Vector3>> _offsetProviders = [];

		/// <summary>
		/// Gets or creates a <see cref="CameraShakeManager"/> on the given camera's
		/// <see cref="GameObject"/>, capturing the baseline position on first creation.
		/// </summary>
		public static CameraShakeManager GetOrCreate(Camera camera)
		{
			var existing = camera.GetComponent<CameraShakeManager>();
			if (existing is not null)
				return existing;

			var manager = camera.gameObject.AddComponent<CameraShakeManager>();
			manager._baseline = camera.transform.localPosition;
			return manager;
		}

		/// <summary>
		/// Registers an offset provider contributed by one <see cref="CameraShakeRunner"/>.
		/// </summary>
		public void Register(System.Func<Vector3> offsetProvider)
		{
			_offsetProviders.Add(offsetProvider);
		}

		/// <summary>
		/// Deregisters an offset provider. Restores the baseline and self-destructs
		/// when no providers remain.
		/// </summary>
		public void Deregister(System.Func<Vector3> offsetProvider)
		{
			_offsetProviders.Remove(offsetProvider);
			if (_offsetProviders.Count == 0)
			{
				transform.localPosition = _baseline;
				Destroy(this);
			}
		}

		private void LateUpdate()
		{
			var combined = Vector3.zero;
			foreach (var provider in _offsetProviders)
				combined += provider();
			transform.localPosition = _baseline + combined;
		}

		private void OnDisable()
		{
			transform.localPosition = _baseline;
		}
	}
}
