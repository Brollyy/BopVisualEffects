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
		private VerticalFlipOverlay? _overlay;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float endBeat)
		{
			_loader = loader;
			_jukebox = jukebox;
			_endBeat = endBeat;
			InitializeOverlay();
		}

		/// <summary>
		/// Stops this effect instance.
		/// </summary>
		public void Stop()
		{
			RemoveOverlay();
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
				InitializeOverlay();
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
			RemoveOverlay();
		}

		private void InitializeOverlay()
		{
			Camera? camera = EffectRuntimeController.ResolveEffectCamera(_loader);
			if (camera is null)
				return;

			_overlay = camera.gameObject.AddComponent<VerticalFlipOverlay>();
			_initialized = true;
		}

		private void RemoveOverlay()
		{
			if (_overlay != null)
				Destroy(_overlay);

			_overlay = null;
		}
	}

	/// <summary>
	/// Mirrors the camera projection vertically by negating the Y axis just before rendering
	/// and restoring it after, so that concurrent projection-modifying effects (e.g. zoom) are
	/// always picked up from the camera's current natural projection each frame.
	/// Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class VerticalFlipOverlay : MonoBehaviour
	{
		private Camera? _camera;

		private void Awake()
		{
			_camera = GetComponent<Camera>();
		}

		// Fires just before the camera culls the scene — apply flip to current natural projection.
		private void OnPreCull()
		{
			if (_camera is null)
				return;

			// Reset so Unity recomputes projection from current fieldOfView/orthographicSize,
			// picking up any changes made this frame by other effects (e.g. ZoomIn/ZoomOut).
			_camera.ResetProjectionMatrix();
			_camera.projectionMatrix = Matrix4x4.Scale(new Vector3(1f, -1f, 1f)) * _camera.projectionMatrix;
		}

		// Fires after the camera finishes rendering — restore natural projection for next frame.
		private void OnPostRender()
		{
			_camera?.ResetProjectionMatrix();
		}
	}
}
