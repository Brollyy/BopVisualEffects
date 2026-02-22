using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.Sepia;

/// <summary>
/// Mutable record holding the sepia intensity for one active effect instance.
/// Updated each frame by <see cref="SepiaEffect"/>'s runner.
/// </summary>
internal sealed class SepiaRequest
{
	public float Intensity;
}

/// <summary>
/// Shared per-camera coordinator for sepia filter effects.
/// Ensures a single <see cref="SepiaOverlay"/> component exists per camera and composites
/// all active sepia requests into one <see cref="Camera.OnPostRender"/> pass, preventing
/// multiplicative compounding when multiple sepia events overlap.
/// </summary>
internal static class SepiaService
{
	private sealed class CameraState
	{
		public readonly List<SepiaRequest> Requests = [];
		public SepiaOverlay? Overlay;
	}

	private static readonly Dictionary<Camera, CameraState> _states = [];

	/// <summary>
	/// Registers a new sepia request for the given camera, creating the overlay if needed.
	/// Returns the request token; caller updates <see cref="SepiaRequest.Intensity"/> each frame
	/// and passes it back to <see cref="RemoveRequest"/> when done.
	/// </summary>
	public static SepiaRequest AddRequest(Camera camera)
	{
		if (!_states.TryGetValue(camera, out var state))
		{
			state = new CameraState();
			_states[camera] = state;
		}

		// Recreate the overlay if it was destroyed externally (e.g., during a scene transition).
		if (!state.Overlay)
		{
			state.Overlay = camera.gameObject.AddComponent<SepiaOverlay>();
			state.Overlay.Requests = state.Requests;
		}

		var request = new SepiaRequest();
		state.Requests.Add(request);
		return request;
	}

	/// <summary>
	/// Unregisters the given sepia request for the camera, destroying the overlay when no requests remain.
	/// </summary>
	public static void RemoveRequest(Camera camera, SepiaRequest request)
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
	/// Three-pass GL overlay that produces a warm vintage sepia look in <see cref="Camera.OnPostRender"/>.
	/// <list type="number">
	///   <item>Alpha-blend toward neutral grey to desaturate the image.</item>
	///   <item>Alpha-blend a warm amber overlay to add the characteristic brownish tint.</item>
	///   <item>Multiply-blend with a warm sepia tone to complete the coloring.</item>
	/// </list>
	/// Multiple concurrent requests are composited using screen-blend intensity so they
	/// reinforce rather than compound multiplicatively.
	/// There is at most one instance per camera, managed by <see cref="SepiaService"/>.
	/// </summary>
	internal sealed class SepiaOverlay : MonoBehaviour
	{
		private static Material? _material;

		/// <summary>
		/// Active requests for this camera. Assigned by <see cref="SepiaService"/> on creation.
		/// </summary>
		internal List<SepiaRequest>? Requests;

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

			// Combine all active request intensities using screen-blend compositing:
			// combined = 1 − (1−i₁)·(1−i₂)·… so overlapping sepia events reinforce each other.
			var combinedIntensity = 0f;
			foreach (var r in Requests)
			{
				if (r.Intensity > 0f)
					combinedIntensity = 1f - (1f - combinedIntensity) * (1f - r.Intensity);
			}

			if (combinedIntensity <= 0f)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			GL.PushMatrix();
			GL.LoadOrtho();

			try
			{
				// Pass 1 — desaturate: blend toward neutral grey.
				mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
				mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
				mat.SetPass(0);

				GL.Begin(GL.QUADS);
				GL.Color(new Color(0.5f, 0.5f, 0.5f, combinedIntensity * 0.75f));
				GL.Vertex3(0f, 0f, 0f);
				GL.Vertex3(0f, 1f, 0f);
				GL.Vertex3(1f, 1f, 0f);
				GL.Vertex3(1f, 0f, 0f);
				GL.End();

				// Pass 2 — warm tint: blend a warm amber colour over the desaturated image.
				mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
				mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
				mat.SetPass(0);

				GL.Begin(GL.QUADS);
				GL.Color(new Color(0.60f, 0.40f, 0.20f, combinedIntensity * 0.5f));
				GL.Vertex3(0f, 0f, 0f);
				GL.Vertex3(0f, 1f, 0f);
				GL.Vertex3(1f, 1f, 0f);
				GL.Vertex3(1f, 0f, 0f);
				GL.End();

				// Pass 3 — sepia multiply: apply warm toning multiply to complete the look.
				var g = Mathf.Lerp(1.0f, 0.85f, combinedIntensity);
				var b = Mathf.Lerp(1.0f, 0.55f, combinedIntensity);
				mat.SetInt("_SrcBlend", (int)BlendMode.DstColor);
				mat.SetInt("_DstBlend", (int)BlendMode.Zero);
				mat.SetPass(0);

				GL.Begin(GL.QUADS);
				GL.Color(new Color(1.0f, g, b, 1.0f));
				GL.Vertex3(0f, 0f, 0f);
				GL.Vertex3(0f, 1f, 0f);
				GL.Vertex3(1f, 1f, 0f);
				GL.Vertex3(1f, 0f, 0f);
				GL.End();
			}
			finally
			{
				GL.PopMatrix();
			}
		}
	}
}
