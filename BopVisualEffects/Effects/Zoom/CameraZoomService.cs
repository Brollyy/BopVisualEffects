using System.Collections.Generic;
using UnityEngine;

namespace BopVisualEffects.Effects.Zoom;

/// <summary>
/// Shared per-camera coordinator for zoom effects (Zoom In, Zoom Out, Zoom Pulse).
/// Composites multiple concurrent zoom factor requests multiplicatively against the camera's
/// baseline size, so that any combination of zoom effects composes correctly.
/// </summary>
internal static class CameraZoomService
{
	private sealed class ZoomState
	{
		public readonly bool IsOrthographic;
		public readonly float BaselineOrthographicSize;
		public readonly float BaselineFieldOfView;
		public readonly Dictionary<object, float> Factors = [];

		public ZoomState(Camera camera)
		{
			IsOrthographic = camera.orthographic;
			BaselineOrthographicSize = camera.orthographicSize;
			BaselineFieldOfView = camera.fieldOfView;
		}
	}

	private static readonly Dictionary<Camera, ZoomState> _states = [];

	/// <summary>
	/// Sets or updates the zoom factor contributed by <paramref name="key"/> on <paramref name="camera"/>
	/// and immediately applies the composite result to the camera.
	/// The first call for a given camera captures the current camera values as the baseline.
	/// </summary>
	public static void SetFactor(Camera camera, object key, float factor)
	{
		if (!_states.TryGetValue(camera, out var state))
		{
			state = new ZoomState(camera);
			_states[camera] = state;
		}

		state.Factors[key] = factor;
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

		state.Factors.Remove(key);

		if (state.Factors.Count == 0)
		{
			RestoreBaseline(camera, state);
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

		var composite = 1f;
		foreach (var f in state.Factors.Values)
			composite *= f;

		if (state.IsOrthographic)
			camera.orthographicSize = state.BaselineOrthographicSize * composite;
		else
			camera.fieldOfView = Mathf.Min(state.BaselineFieldOfView * composite, 179f);
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
}
