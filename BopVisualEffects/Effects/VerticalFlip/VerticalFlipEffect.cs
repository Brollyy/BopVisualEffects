using System.Collections.Generic;
using BopVisualEffects.Core;
using BopVisualEffects.Effects.Flip;
using UnityEngine;

namespace BopVisualEffects.Effects.VerticalFlip;

/// <summary>
/// Effect definition for a vertical camera flip that mirrors the screen top-to-bottom.
/// </summary>
public sealed class VerticalFlipEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "vertical flip";

	/// <inheritdoc />
	public string DisplayName => "Vertical Flip";

	/// <inheritdoc />
	public string ConfigKey => "VerticalFlip";

	/// <inheritdoc />
	public string Description => "Mirrors the screen vertically for a disorienting, playful effect.";

	/// <inheritdoc />
	public MixtapeEventTemplate CreateTemplate(string pluginGuid)
	{
		return new MixtapeEventTemplate
		{
			dataModel = $"{pluginGuid}/{Id}",
			length = 2.0f,
			resizable = true,
			properties = new Dictionary<string, object>()
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<VerticalFlipEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<VerticalFlipRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, endBeat));
		}
	}

	private sealed class VerticalFlipRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private Camera? _camera;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float endBeat)
		{
			_loader = loader;
			_jukebox = jukebox;
			_endBeat = endBeat;
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
			CameraFlipService.AddVerticalFlip(camera);
			_initialized = true;
		}

		private void RemoveFlip()
		{
			if (_camera is null)
				return;

			CameraFlipService.RemoveVerticalFlip(_camera);
			_camera = null;
		}
	}
}

