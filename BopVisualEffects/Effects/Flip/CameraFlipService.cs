using System.Collections.Generic;
using UnityEngine;

namespace BopVisualEffects.Effects.Flip;

/// <summary>
/// Shared per-camera coordinator for horizontal and vertical flip effects.
/// Composites multiple concurrent flip requests into a single projection matrix transformation,
/// with correct backface-culling inversion when an odd number of axes are flipped.
/// </summary>
internal static class CameraFlipService
{
	private sealed class FlipState
	{
		public int HorizontalCount;
		public int VerticalCount;
		public CameraFlipOverlay? Overlay;
	}

	private static readonly Dictionary<Camera, FlipState> _states = [];

	/// <summary>Registers a horizontal flip request for the given camera.</summary>
	public static void AddHorizontalFlip(Camera camera) => AddFlip(camera, horizontal: true);

	/// <summary>Unregisters a horizontal flip request for the given camera.</summary>
	public static void RemoveHorizontalFlip(Camera camera) => RemoveFlip(camera, horizontal: true);

	/// <summary>Registers a vertical flip request for the given camera.</summary>
	public static void AddVerticalFlip(Camera camera) => AddFlip(camera, horizontal: false);

	/// <summary>Unregisters a vertical flip request for the given camera.</summary>
	public static void RemoveVerticalFlip(Camera camera) => RemoveFlip(camera, horizontal: false);

	private static void AddFlip(Camera camera, bool horizontal)
	{
		if (!_states.TryGetValue(camera, out var state))
		{
			state = new FlipState { Overlay = camera.gameObject.AddComponent<CameraFlipOverlay>() };
			_states[camera] = state;
		}

		if (horizontal)
			state.HorizontalCount++;
		else
			state.VerticalCount++;

		state.Overlay!.UpdateState(state.HorizontalCount > 0, state.VerticalCount > 0);
	}

	private static void RemoveFlip(Camera camera, bool horizontal)
	{
		if (!_states.TryGetValue(camera, out var state))
			return;

		if (horizontal)
		{
			if (state.HorizontalCount > 0)
				state.HorizontalCount--;
		}
		else
		{
			if (state.VerticalCount > 0)
				state.VerticalCount--;
		}

		if (state.HorizontalCount == 0 && state.VerticalCount == 0)
		{
			if (state.Overlay != null)
				Object.Destroy(state.Overlay);

			_states.Remove(camera);
		}
		else
		{
			state.Overlay!.UpdateState(state.HorizontalCount > 0, state.VerticalCount > 0);
		}
	}

	/// <summary>
	/// Camera-attached component that applies the combined flip to the projection matrix
	/// in <see cref="OnPreCull"/> and restores it in <see cref="OnPostRender"/>.
	/// There is at most one instance of this component per camera, managed by <see cref="CameraFlipService"/>.
	/// </summary>
	private sealed class CameraFlipOverlay : MonoBehaviour
	{
		private Camera? _camera;
		private bool _flipH;
		private bool _flipV;
		private bool _savedInvertCulling;

		/// <summary>
		/// Updates the active flip axes. Called by <see cref="CameraFlipService"/> whenever the flip state changes.
		/// </summary>
		public void UpdateState(bool flipH, bool flipV)
		{
			_flipH = flipH;
			_flipV = flipV;
		}

		private void Awake()
		{
			_camera = GetComponent<Camera>();
		}

		// Fires just before the camera culls the scene — apply combined flip to the current natural projection.
		private void OnPreCull()
		{
			if (_camera is null)
				return;

			// Reset so Unity recomputes projection from current fieldOfView/orthographicSize,
			// picking up any changes made this frame by other effects (e.g. ZoomIn/ZoomOut).
			_camera.ResetProjectionMatrix();
			var scaleX = _flipH ? -1f : 1f;
			var scaleY = _flipV ? -1f : 1f;
			_camera.projectionMatrix = Matrix4x4.Scale(new Vector3(scaleX, scaleY, 1f)) * _camera.projectionMatrix;
			// An odd number of negated axes reverses triangle winding; invert culling to compensate.
			_savedInvertCulling = GL.invertCulling;
			GL.invertCulling = _flipH != _flipV;
		}

		// Fires after the camera finishes rendering — restore natural projection and culling for next frame.
		private void OnPostRender()
		{
			GL.invertCulling = _savedInvertCulling;
			_camera?.ResetProjectionMatrix();
		}

		private void OnDisable()
		{
			GL.invertCulling = _savedInvertCulling;
			_camera?.ResetProjectionMatrix();
		}
	}
}
