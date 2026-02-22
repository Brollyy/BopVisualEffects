using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.Hsl;

/// <summary>
/// Mutable record holding the HSL parameters for one active effect instance.
/// Updated each frame by <see cref="HslEffect"/>'s runner; read by <see cref="HslService"/>'s overlay.
/// </summary>
internal sealed class HslRequest
{
	public float HueShift;
	public float Saturation = 1f;
	public float Lightness;
	public float Intensity;
}

/// <summary>
/// Shared per-camera coordinator for HSL filter effects.
/// Ensures a single <see cref="HslOverlay"/> component exists per camera and applies all active
/// HSL requests in one <see cref="Camera.OnPostRender"/> pass using GL blend operations.
/// </summary>
internal static class HslService
{
	private sealed class CameraState
	{
		public readonly List<HslRequest> Requests = [];
		public HslOverlay? Overlay;
	}

	private static readonly Dictionary<Camera, CameraState> _states = [];

	/// <summary>
	/// Registers a new HSL request for the given camera, creating the overlay if needed.
	/// Returns the request token; the caller updates its fields each frame and passes it back to
	/// <see cref="RemoveRequest"/> when done.
	/// </summary>
	public static HslRequest AddRequest(Camera camera)
	{
		if (!_states.TryGetValue(camera, out var state))
		{
			state = new CameraState();
			_states[camera] = state;
		}

		// Recreate the overlay if it was destroyed externally (e.g., during a scene transition).
		if (!state.Overlay)
		{
			state.Overlay = camera.gameObject.AddComponent<HslOverlay>();
			state.Overlay.Requests = state.Requests;
		}

		var request = new HslRequest();
		state.Requests.Add(request);
		return request;
	}

	/// <summary>
	/// Unregisters the given HSL request for the camera, destroying the overlay when no requests remain.
	/// </summary>
	public static void RemoveRequest(Camera camera, HslRequest request)
	{
		if (!_states.TryGetValue(camera, out var state))
			return;

		state.Requests.Remove(request);

		if (state.Requests.Count == 0)
		{
			if (state.Overlay)
				Object.Destroy(state.Overlay);

			_states.Remove(camera);
		}
		else if (!state.Overlay)
		{
			// Overlay was destroyed externally; clean up the stale state entry.
			_states.Remove(camera);
		}
	}

	/// <summary>
	/// Camera-attached GL overlay that applies HSL adjustments for all active requests in
	/// <see cref="Camera.OnPostRender"/> using blend operations:
	/// <list type="bullet">
	///   <item>Saturation reduction (0–1): alpha-blend toward neutral grey.</item>
	///   <item>Lightness increase: alpha-blend toward white.</item>
	///   <item>Lightness decrease: alpha-blend toward black.</item>
	/// </list>
	/// Hue rotation and saturation boosting above 1 are not achievable via GL blend operations
	/// and are silently ignored.
	/// There is at most one instance per camera, managed by <see cref="HslService"/>.
	/// </summary>
	internal sealed class HslOverlay : MonoBehaviour
	{
		private static Material? _material;

		/// <summary>
		/// Active requests for this camera. Assigned by <see cref="HslService"/> on creation.
		/// </summary>
		internal List<HslRequest>? Requests;

		private static Material? GetMaterial()
		{
			if (!_material)
			{
				var shader = Shader.Find("Hidden/Internal-Colored");
				if (shader is null)
					return null;

				_material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
			}

			return _material;
		}

		// Unity calls this on the camera's GameObject after the scene finishes rendering.
		private void OnPostRender()
		{
			if (Requests is null)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			GL.PushMatrix();
			GL.LoadOrtho();

			try
			{
				foreach (var req in Requests)
				{
					if (req.Intensity <= 0f)
						continue;

					// Saturation reduction: blend toward neutral grey.
					// Saturation values above 1 (boost) are not achievable via GL and are ignored.
					var desatAmount = Mathf.Clamp01((1f - req.Saturation) * req.Intensity);
					if (desatAmount > 0f)
					{
						mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
						mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
						mat.SetPass(0);

						GL.Begin(GL.QUADS);
						GL.Color(new Color(0.5f, 0.5f, 0.5f, desatAmount));
						GL.Vertex3(0f, 0f, 0f);
						GL.Vertex3(0f, 1f, 0f);
						GL.Vertex3(1f, 1f, 0f);
						GL.Vertex3(1f, 0f, 0f);
						GL.End();
					}

					// Lightness shift: blend toward white (positive) or black (negative).
					var lightnessAmount = Mathf.Clamp(req.Lightness * req.Intensity, -1f, 1f);
					if (lightnessAmount > 0f)
					{
						mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
						mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
						mat.SetPass(0);

						GL.Begin(GL.QUADS);
						GL.Color(new Color(1f, 1f, 1f, lightnessAmount));
						GL.Vertex3(0f, 0f, 0f);
						GL.Vertex3(0f, 1f, 0f);
						GL.Vertex3(1f, 1f, 0f);
						GL.Vertex3(1f, 0f, 0f);
						GL.End();
					}
					else if (lightnessAmount < 0f)
					{
						mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
						mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
						mat.SetPass(0);

						GL.Begin(GL.QUADS);
						GL.Color(new Color(0f, 0f, 0f, -lightnessAmount));
						GL.Vertex3(0f, 0f, 0f);
						GL.Vertex3(0f, 1f, 0f);
						GL.Vertex3(1f, 1f, 0f);
						GL.Vertex3(1f, 0f, 0f);
						GL.End();
					}
				}
			}
			finally
			{
				GL.PopMatrix();
			}
		}
	}
}

