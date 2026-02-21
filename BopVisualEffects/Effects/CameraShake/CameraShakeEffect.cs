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
			CameraShakeRunner.GetOrAddShake(loader, loader.jukebox, startBeat, endBeat, amplitude, frequency);
		}
	}

	private sealed class CameraShakeRunner : MonoBehaviour
	{
		// Maps camera transform instance ID to the single active runner for that camera.
		private static readonly Dictionary<int, CameraShakeRunner> s_runnersByCamera = new();

		private bool _stopped;
		private JukeboxScript? _jukebox;
		private Transform? _targetTransform;
		private Vector3 _stableBaseline;

		// Per-layer shake parameters.
		private readonly List<ShakeLayer> _layers = new();

		private readonly struct ShakeLayer
		{
			public readonly float StartBeat;
			public readonly float EndBeat;
			public readonly float Amplitude;
			public readonly float Frequency;

			public ShakeLayer(float startBeat, float endBeat, float amplitude, float frequency)
			{
				StartBeat = startBeat;
				EndBeat = endBeat;
				Amplitude = Mathf.Max(0f, amplitude);
				Frequency = Mathf.Max(0.1f, frequency);
			}
		}

		/// <summary>
		/// Gets the existing runner for the camera resolved from <paramref name="loader"/>,
		/// or creates a new one, then adds a shake layer.
		/// All layers share the same stable baseline, preventing drift from overlapping shakes.
		/// </summary>
		public static void GetOrAddShake(
			MixtapeLoaderCustom loader, JukeboxScript? jukebox,
			float startBeat, float endBeat, float amplitude, float frequency)
		{
			Camera? camera = EffectRuntimeController.ResolveEffectCamera(loader);
			if (camera is null)
				return;

			int cameraId = camera.transform.GetInstanceID();
			if (!s_runnersByCamera.TryGetValue(cameraId, out CameraShakeRunner runner) || !runner)
			{
				// Explicitly clean up any stale (destroyed) entry before creating a new runner.
				s_runnersByCamera.Remove(cameraId);
				runner = EffectRuntimeController.Instance.gameObject.AddComponent<CameraShakeRunner>();
				runner._jukebox = jukebox;
				runner._targetTransform = camera.transform;
				runner._stableBaseline = camera.transform.localPosition;
				s_runnersByCamera[cameraId] = runner;
			}

			runner._layers.Add(new ShakeLayer(startBeat, endBeat, amplitude, frequency));
		}

		/// <summary>
		/// Stops this effect instance and restores the camera to the stable baseline.
		/// </summary>
		public void Stop()
		{
			Cleanup();
			Destroy(this);
		}

		private void LateUpdate()
		{
			if (_jukebox is null)
			{
				Stop();
				return;
			}

			var currentBeat = _jukebox.CurrentBeat;

			// Expire finished layers (iterate in reverse to allow in-place removal).
			for (int i = _layers.Count - 1; i >= 0; i--)
			{
				if (currentBeat >= _layers[i].EndBeat)
					_layers.RemoveAt(i);
			}

			if (_layers.Count == 0)
			{
				Stop();
				return;
			}

			// Accumulate offset from all active layers against the shared stable baseline.
			Vector3 totalOffset = Vector3.zero;
			for (int i = 0; i < _layers.Count; i++)
			{
				var layer = _layers[i];
				var progress = Mathf.InverseLerp(layer.StartBeat, layer.EndBeat, currentBeat);
				var envelope = 1f - progress;
				if (envelope <= 0f)
					continue;

				var strength = layer.Amplitude * envelope * Time.timeScale;
				var noiseTime = currentBeat * layer.Frequency;
				var x = Mathf.PerlinNoise(noiseTime, 0f) * 2f - 1f;
				var y = Mathf.PerlinNoise(0f, noiseTime) * 2f - 1f;
				totalOffset += new Vector3(x, y, 0f) * strength;
			}

			_targetTransform!.localPosition = _stableBaseline + totalOffset;
		}

		private void OnDisable()
		{
			Cleanup();
		}

		private void Cleanup()
		{
			// Guard against double-cleanup: Destroy(this) schedules destruction and immediately
			// fires OnDisable, so Stop() calling Cleanup() then Destroy() would run Cleanup() twice.
			if (_stopped)
				return;

			_stopped = true;

			if (_targetTransform is null)
				return;

			_targetTransform.localPosition = _stableBaseline;
			s_runnersByCamera.Remove(_targetTransform.GetInstanceID());
		}
	}
}
