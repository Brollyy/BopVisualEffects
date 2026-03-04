using System.Collections.Generic;
using UnityEngine;

namespace BopVisualEffects.Effects.Zoom;

/// <summary>
/// Shared per-camera coordinator for zoom effects (Zoom In, Zoom Out, Zoom Pulse).
/// Composites multiple concurrent zoom factor requests multiplicatively against the camera's
/// baseline size, and sums the per-entry NDC focal-point offsets so that any combination of
/// zoom effects — including those with different zoom targets — composes correctly.
/// The focal-point translation is applied via a projection-matrix overlay (<see cref="CameraZoomOverlay"/>)
/// in <c>OnPreCull</c>, which composes cleanly with other projection-matrix modifiers such as
/// <c>CameraFlipOverlay</c> and does not conflict with transform-based effects like Camera Shake.
/// </summary>
internal static class CameraZoomService
{
	private readonly struct ZoomEntry(float factor, Vector2 focalNDC)
	{
		public readonly float Factor = factor;

		/// <summary>
		/// Focal point in NDC space: (0, 0) = screen center, (±1, ±1) = screen corners.
		/// The zoom will keep this point stationary as the camera size changes.
		/// </summary>
		public readonly Vector2 FocalNDC = focalNDC;
	}

	private sealed class ZoomState
	{
		public readonly bool IsOrthographic;
		public readonly float BaselineOrthographicSize;
		public readonly float BaselineFieldOfView;
		public readonly Dictionary<object, ZoomEntry> Entries = [];
		public CameraZoomOverlay? Overlay;

		public ZoomState(Camera camera)
		{
			IsOrthographic = camera.orthographic;
			BaselineOrthographicSize = camera.orthographicSize;
			BaselineFieldOfView = camera.fieldOfView;
		}
	}

	private static readonly Dictionary<Camera, ZoomState> _states = [];

	/// <summary>
	/// Sets or updates the zoom factor and focal point contributed by <paramref name="key"/> on
	/// <paramref name="camera"/> and immediately applies the composite result.
	/// The first call for a given camera captures the current camera values as the baseline.
	/// </summary>
	/// <param name="camera">The camera to apply the zoom to.</param>
	/// <param name="key">Unique key identifying this zoom contributor.</param>
	/// <param name="factor">
	/// Zoom factor: values below 1 zoom in, values above 1 zoom out.
	/// </param>
	/// <param name="focalNDC">
	/// Focal point in NDC space — (0, 0) is the screen center, (±1, ±1) are the corners.
	/// The zoom keeps this screen point stationary as the size changes.
	/// Defaults to (0, 0) (screen center) for backwards compatibility.
	/// </param>
	public static void SetFactor(Camera camera, object key, float factor, Vector2 focalNDC = default)
	{
		if (!_states.TryGetValue(camera, out var state))
		{
			state = new ZoomState(camera);
			_states[camera] = state;
		}

		state.Entries[key] = new ZoomEntry(factor, focalNDC);

		if (!state.Overlay)
			state.Overlay = camera.gameObject.AddComponent<CameraZoomOverlay>();

		ApplyComposite(camera, state);
	}

	/// <summary>
	/// Removes the zoom factor contributed by <paramref name="key"/> from <paramref name="camera"/>.
	/// Restores the baseline camera values when no factors remain.
	/// </summary>
	public static void RemoveFactor(Camera camera, object key)
	{
		if (!_states.TryGetValue(camera, out var state))
			return;

		state.Entries.Remove(key);

		if (state.Entries.Count == 0)
		{
			RestoreBaseline(camera, state);
			if (state.Overlay)
			{
				state.Overlay.SetNDCOffset(Vector2.zero);
				Object.Destroy(state.Overlay);
			}
			_states.Remove(camera);
		}
		else
		{
			ApplyComposite(camera, state);
		}
	}

	private static void ApplyComposite(Camera camera, ZoomState state)
	{
		if (!camera)
			return;

		var compositeZoom = 1f;
		var compositeNDCOffset = Vector2.zero;
		foreach (var entry in state.Entries.Values)
		{
			compositeZoom *= entry.Factor;
			// Each entry's NDC contribution: focalNDC * (1 - 1/factor).
			// This is the projection-space shift needed to keep entry.FocalNDC stationary after
			// the zoom factor changes the camera size. Contributions are summed across all entries.
			compositeNDCOffset += entry.FocalNDC * (1f - 1f / entry.Factor);
		}

		if (state.IsOrthographic)
			camera.orthographicSize = state.BaselineOrthographicSize * compositeZoom;
		else
			camera.fieldOfView = Mathf.Min(state.BaselineFieldOfView * compositeZoom, 179f);

		if (state.Overlay)
			state.Overlay.SetNDCOffset(compositeNDCOffset);
	}

	private static void RestoreBaseline(Camera camera, ZoomState state)
	{
		if (!camera)
			return;

		if (state.IsOrthographic)
			camera.orthographicSize = state.BaselineOrthographicSize;
		else
			camera.fieldOfView = state.BaselineFieldOfView;
	}

	/// <summary>
	/// Camera-attached component that applies the composite focal-point NDC translation to the
	/// projection matrix in <see cref="OnPreCull"/> and restores it in <see cref="OnPostRender"/>.
	/// There is at most one instance of this component per camera, managed by <see cref="CameraZoomService"/>.
	/// Running at execution order 10 ensures this fires after <c>CameraFlipOverlay</c> (order 0),
	/// so the focal-point shift is applied on top of any flip that is already in effect.
	/// </summary>
	[DefaultExecutionOrder(10)]
	private sealed class CameraZoomOverlay : MonoBehaviour
	{
		private Camera? _camera;
		private Vector2 _ndcOffset;

		/// <summary>
		/// Updates the composite NDC offset. Called by <see cref="CameraZoomService"/> whenever the zoom state changes.
		/// </summary>
		public void SetNDCOffset(Vector2 offset) => _ndcOffset = offset;

		private void Awake() => _camera = GetComponent<Camera>();

		// Fires just before the camera culls the scene — apply the focal-point translation on top
		// of whatever projection matrix is already in effect this frame (e.g. flip from CameraFlipOverlay).
		private void OnPreCull()
		{
			if (!_camera || _ndcOffset == Vector2.zero)
				return;

			var offsetMatrix = Matrix4x4.identity;
			offsetMatrix.m03 = _ndcOffset.x;
			offsetMatrix.m13 = _ndcOffset.y;
			_camera.projectionMatrix = offsetMatrix * _camera.projectionMatrix;
		}

		// Fires after the camera finishes rendering — restore natural projection for next frame.
		private void OnPostRender() => ResetProjection();

		private void OnDisable() => ResetProjection();

		private void ResetProjection()
		{
			if (_camera)
				_camera.ResetProjectionMatrix();
		}
	}
}
