using System.Collections.Generic;
using BopVisualEffects.Core;
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
/// HSL requests through one <see cref="Camera.OnRenderImage"/> pass.
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
	/// Camera-attached component that applies HSL adjustments for all active requests in
	/// <see cref="Camera.OnRenderImage"/>. When the shader AssetBundle is available, each request
	/// is applied as a separate <see cref="Graphics.Blit(RenderTexture, RenderTexture, Material)"/>
	/// pass chained through intermediate <see cref="RenderTexture"/>s, giving true per-pixel hue
	/// rotation, saturation scaling and lightness offset. When the bundle is absent the overlay
	/// falls back to GL blend operations applied directly to the destination <see cref="RenderTexture"/>.
	/// There is at most one instance per camera, managed by <see cref="HslService"/>.
	/// </summary>
	internal sealed class HslOverlay : MonoBehaviour
	{
		private const string ShaderAssetPath = "Assets/Shaders/BopVisualEffects_HSL.shader";

		private static Material? _hslMaterial;
		private static bool _hslShaderUnavailable;
		private static Material? _glMaterial;

		/// <summary>
		/// Active requests for this camera. Assigned by <see cref="HslService"/> on creation.
		/// </summary>
		internal List<HslRequest>? Requests;

		private static Material? GetHslMaterial()
		{
			if (_hslMaterial)
				return _hslMaterial;

			if (_hslShaderUnavailable)
				return null;

			var bundle = ShaderBundleLoader.GetBundle();
			if (bundle is null)
			{
				_hslShaderUnavailable = true;
				return null;
			}

			var shader = bundle.LoadAsset<Shader>(ShaderAssetPath);
			if (shader is null || !shader.isSupported)
			{
				_hslShaderUnavailable = true;
				return null;
			}

			_hslMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
			return _hslMaterial;
		}

		private static Material? GetGlMaterial()
		{
			if (!_glMaterial)
			{
				var shader = Shader.Find("Hidden/Internal-Colored");
				if (shader is null)
					return null;

				_glMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
			}

			return _glMaterial;
		}

		// Unity calls this on the camera's GameObject with the rendered image as the source.
		private void OnRenderImage(RenderTexture src, RenderTexture dest)
		{
			if (Requests is null)
			{
				Graphics.Blit(src, dest);
				return;
			}

			var active = new List<HslRequest>(Requests.Count);
			foreach (var r in Requests)
			{
				if (r.Intensity > 0f)
					active.Add(r);
			}

			if (active.Count == 0)
			{
				Graphics.Blit(src, dest);
				return;
			}

			var mat = GetHslMaterial();
			if (mat != null)
				ApplyShader(src, dest, active, mat);
			else
				ApplyGlFallback(src, dest, active);
		}

		private static void ApplyShader(
			RenderTexture src, RenderTexture dest, List<HslRequest> active, Material mat)
		{
			// Chain each request through a temporary RT so every request's output
			// feeds into the next one's input.
			var intermediates = new List<RenderTexture>(active.Count - 1);
			try
			{
				for (var i = 0; i < active.Count; i++)
				{
					var req = active[i];
					var source = i == 0 ? src : intermediates[i - 1];
					RenderTexture target;

					if (i == active.Count - 1)
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

		private static void ApplyGlFallback(
			RenderTexture src, RenderTexture dest, List<HslRequest> active)
		{
			// Copy source into dest first, then draw GL blend operations on top.
			Graphics.Blit(src, dest);

			var glMat = GetGlMaterial();
			if (glMat is null)
				return;

			var prevActive = RenderTexture.active;
			RenderTexture.active = dest;

			GL.PushMatrix();
			GL.LoadOrtho();

			try
			{
				foreach (var req in active)
				{
					// Saturation reduction: blend toward neutral grey.
					// Saturation values above 1 (boost) require the shader and are ignored here.
					var desatAmount = Mathf.Clamp01((1f - req.Saturation) * req.Intensity);
					if (desatAmount > 0f)
					{
						glMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
						glMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
						glMat.SetPass(0);

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
						glMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
						glMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
						glMat.SetPass(0);

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
						glMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
						glMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
						glMat.SetPass(0);

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
				RenderTexture.active = prevActive;
			}
		}
	}
}

