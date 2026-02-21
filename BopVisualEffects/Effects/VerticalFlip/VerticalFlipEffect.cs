using System.Collections.Generic;
using BopVisualEffects.Core;
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
		private Matrix4x4 _originalProjection;
		private Matrix4x4 _flippedProjection;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float endBeat)
		{
			_loader = loader;
			_jukebox = jukebox;
			_endBeat = endBeat;
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

			_camera!.projectionMatrix = _flippedProjection;
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
			// Reset to the camera's natural projection so we always flip from a clean base,
			// regardless of whether another projection-modifying effect is currently active.
			camera.ResetProjectionMatrix();
			_originalProjection = camera.projectionMatrix;
			// Negate the Y axis of the projection matrix to mirror the screen vertically.
			_flippedProjection = Matrix4x4.Scale(new Vector3(1f, -1f, 1f)) * _originalProjection;
			_initialized = true;
		}

		private void ResetCamera()
		{
			if (_camera is null)
				return;

			_camera.projectionMatrix = _originalProjection;
		}
	}
}
