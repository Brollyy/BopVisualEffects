using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

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
/// Ensures a single <see cref="HslOverlay"/> component exists per camera and chains all active
/// HSL requests into one <see cref="Camera.OnRenderImage"/> blit pipeline, preventing
/// multiple handlers from stacking on the same camera.
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
	/// Camera-attached component that chains all active HSL requests as a sequential blit
	/// pipeline in <see cref="Camera.OnRenderImage"/>.
	/// There is at most one instance per camera, managed by <see cref="HslService"/>.
	/// </summary>
	internal sealed class HslOverlay : MonoBehaviour
	{
		private const string ShaderResourceName = "BopVisualEffects.Effects.Hsl.BopVisualEffects_HSL.shader";

		private static Material? _material;
		private static bool _shaderUnavailable;

		/// <summary>
		/// Active requests for this camera. Assigned by <see cref="HslService"/> on creation.
		/// </summary>
		internal List<HslRequest>? Requests;

		private static string? LoadShaderSource()
		{
			using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ShaderResourceName);
			if (stream is null)
				return null;

			using var reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}

		private static Material? GetMaterial()
		{
			if (_material)
				return _material;

			if (_shaderUnavailable)
				return null;

			var shaderSource = LoadShaderSource();
			if (shaderSource is null)
			{
				_shaderUnavailable = true;
				return null;
			}

#pragma warning disable CS0618 // Material(string) is obsolete for new code but functional in PC standalone builds.
			_material = new Material(shaderSource) { hideFlags = HideFlags.HideAndDontSave };
#pragma warning restore CS0618
			if (!_material.shader || !_material.shader.isSupported)
			{
				Destroy(_material);
				_material = null;
				_shaderUnavailable = true;
			}

			return _material;
		}

		// Unity calls this on the camera's GameObject with the rendered image as the source.
		// Chains all active HSL requests as sequential blits through intermediate RenderTextures.
		private void OnRenderImage(RenderTexture src, RenderTexture dest)
		{
			var mat = GetMaterial();
			if (mat is null || Requests is null)
			{
				Graphics.Blit(src, dest);
				return;
			}

			var activeCount = 0;
			foreach (var r in Requests)
			{
				if (r.Intensity > 0f)
					activeCount++;
			}

			if (activeCount == 0)
			{
				Graphics.Blit(src, dest);
				return;
			}

			// Chain active requests: src → [temp₁ → … → tempₙ₋₁] → dest.
			// Each request applies its own HSL adjustment to the output of the previous one.
			var intermediates = new List<RenderTexture>(activeCount - 1);
			var processed = 0;

			try
			{
				foreach (var req in Requests)
				{
					if (req.Intensity <= 0f)
						continue;

					processed++;
					var isLast = processed == activeCount;
					var source = (processed == 1) ? src : intermediates[intermediates.Count - 1];
					RenderTexture target;

					if (isLast)
					{
						target = dest;
					}
					else
					{
						target = RenderTexture.GetTemporary(src.descriptor);
						intermediates.Add(target);
					}

					mat.SetFloat("_HueShift", req.HueShift);
					mat.SetFloat("_Saturation", req.Saturation);
					mat.SetFloat("_Lightness", req.Lightness);
					mat.SetFloat("_Intensity", req.Intensity);
					Graphics.Blit(source, target, mat);
				}
			}
			finally
			{
				foreach (var t in intermediates)
					RenderTexture.ReleaseTemporary(t);
			}
		}
	}
}
