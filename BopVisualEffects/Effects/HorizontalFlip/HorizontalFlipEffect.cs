using System.Collections.Generic;
using BopVisualEffects.Core;
using BopVisualEffects.Effects.Flip;
using UnityEngine;

namespace BopVisualEffects.Effects.HorizontalFlip;

/// <summary>
/// Effect definition for a horizontal camera flip that mirrors the screen left-to-right.
/// </summary>
public sealed class HorizontalFlipEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "horizontal flip";

	/// <inheritdoc />
	public string DisplayName => "Horizontal Flip";

	/// <inheritdoc />
	public string ConfigKey => "HorizontalFlip";

	/// <inheritdoc />
	public string Description => "Mirrors the screen horizontally for a disorienting, playful effect.";

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
				["persist_camera"] = false
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<HorizontalFlipEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;
		var persist = entity.GetBool("persist_camera", false);

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<HorizontalFlipRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, endBeat, persist));
		}
	}

	private sealed class HorizontalFlipRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private Camera? _camera;
		private bool _persistCamera;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float endBeat, bool persistCamera)
		{
			_loader = loader;
			_jukebox = jukebox;
			_endBeat = endBeat;
			_persistCamera = persistCamera;
			InitializeFlip();
		}

		/// <summary>
		/// Stops this effect instance.
		/// </summary>
		public void Stop()
		{
			RemoveFlip();
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
				InitializeFlip();
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
		}

		private void OnDisable()
		{
			RemoveFlip();
		}

		private void InitializeFlip()
		{
			Camera? camera = EffectRuntimeController.ResolveEffectCamera(_loader);
			if (camera is null)
				return;

			_camera = camera;
			CameraFlipService.AddHorizontalFlip(camera);
			_initialized = true;
		}

		private void RemoveFlip()
		{
			if (_camera is null)
				return;

			CameraFlipService.RemoveHorizontalFlip(_camera);
			_camera = null;
		}

		private void RebindCameraIfNeeded()
		{
			Camera? camera = EffectRuntimeController.ResolveEffectCamera(_loader);
			if (camera is null || _camera == camera)
				return;

			RemoveFlip();
			_camera = camera;
			CameraFlipService.AddHorizontalFlip(camera);
		}
	}
}
