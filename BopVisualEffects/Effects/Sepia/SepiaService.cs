using System.Collections.Generic;
using BopVisualEffects.Core;
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
/// all active sepia requests into one <see cref="Camera.OnRenderImage"/> pass, preventing
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
	/// Camera-attached component that applies the sepia tone filter in
	/// <see cref="Camera.OnRenderImage"/>. When the shader AssetBundle is available it uses a
	/// per-pixel sepia conversion shader (standard Adobe/Kodak matrix) via
	/// <see cref="Graphics.Blit(RenderTexture, RenderTexture, Material)"/>.  When the bundle is
	/// absent it falls back to three GL blend passes that produce an approximation.  Multiple
	/// concurrent requests are composited using screen-blend intensity so overlapping sepia events
	/// reinforce each other predictably rather than compounding multiplicatively.
	/// There is at most one instance per camera, managed by <see cref="SepiaService"/>.
	/// </summary>
	internal sealed class SepiaOverlay : MonoBehaviour
	{
		private const string ShaderAssetPath = "Assets/Shaders/BopVisualEffects_Sepia.shader";
		private static readonly ClassLogger _log = ClassLogger.GetForClass<SepiaOverlay>();

		private static Material? _sepiaMaterial;
		private static bool _sepiaShaderUnavailable;
		private static bool _sepiaFallbackLogged;
		private static Material? _glMaterial;

		/// <summary>
		/// Active requests for this camera. Assigned by <see cref="SepiaService"/> on creation.
		/// </summary>
		internal List<SepiaRequest>? Requests;

		private static Material? GetSepiaMaterial()
		{
			if (_sepiaMaterial)
				return _sepiaMaterial;

			if (_sepiaShaderUnavailable)
				return null;

			var bundle = ShaderBundleLoader.GetBundle();
			if (bundle is null)
			{
				_sepiaShaderUnavailable = true;
				LogSepiaFallback("Shader AssetBundle not available");
				return null;
			}

			var shader = bundle.LoadAsset<Shader>(ShaderAssetPath);
			if (shader is null || !shader.isSupported)
			{
				_sepiaShaderUnavailable = true;
				LogSepiaFallback("Sepia shader missing or unsupported in bundle");
				return null;
			}

			_sepiaMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
			return _sepiaMaterial;
		}

		private static void LogSepiaFallback(string reason)
		{
			if (_sepiaFallbackLogged)
				return;

			_sepiaFallbackLogged = true;
			_log.Warning(
				$"[Sepia] Using GL fallback ({reason}). Sepia uses the approximation path without shader bundle.");
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

			// Combine all active request intensities using screen-blend compositing:
			// combined = 1 − (1−i₁)·(1−i₂)·… so overlapping sepia events reinforce each other.
			var combined = 0f;
			foreach (var r in Requests)
			{
				if (r.Intensity > 0f)
					combined = 1f - (1f - combined) * (1f - r.Intensity);
			}

			if (combined <= 0f)
			{
				Graphics.Blit(src, dest);
				return;
			}

			var mat = GetSepiaMaterial();
			if (mat != null)
			{
				mat.SetFloat("_Intensity", combined);
				Graphics.Blit(src, dest, mat);
			}
			else
			{
				ApplyGlFallback(src, dest, combined);
			}
		}

		private static void ApplyGlFallback(RenderTexture src, RenderTexture dest, float combinedIntensity)
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
				// Pass 1 — desaturate: blend toward neutral grey.
				glMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
				glMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
				glMat.SetPass(0);

				GL.Begin(GL.QUADS);
				GL.Color(new Color(0.5f, 0.5f, 0.5f, combinedIntensity * 0.75f));
				GL.Vertex3(0f, 0f, 0f);
				GL.Vertex3(0f, 1f, 0f);
				GL.Vertex3(1f, 1f, 0f);
				GL.Vertex3(1f, 0f, 0f);
				GL.End();

				// Pass 2 — warm tint: blend a warm amber colour over the desaturated image.
				glMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
				glMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
				glMat.SetPass(0);

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
				glMat.SetInt("_SrcBlend", (int)BlendMode.DstColor);
				glMat.SetInt("_DstBlend", (int)BlendMode.Zero);
				glMat.SetPass(0);

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
				RenderTexture.active = prevActive;
			}
		}
	}
}
