using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.Vignette;

/// <summary>
/// Effect definition for a dark edge gradient overlay for dramatic or horror atmosphere.
/// </summary>
public sealed class VignetteEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "vignette";

	/// <inheritdoc />
	public string DisplayName => "Vignette";

	/// <inheritdoc />
	public string ConfigKey => "Vignette";

	/// <inheritdoc />
	public string Description => "Darkens the screen edges with a smooth gradient frame for a dramatic or horror atmosphere.";

	/// <inheritdoc />
	public MixtapeEventTemplate CreateTemplate(string pluginGuid)
	{
		return new MixtapeEventTemplate
		{
			dataModel = $"{pluginGuid}/{Id}",
			length = 4.0f,
			resizable = true,
			properties = new Dictionary<string, object>
			{
				["alpha"] = 0.7f,
				["size"] = 0.1f,
				["persist_between_minigames"] = false,
				["ease_in"] = true,
				["ease_out"] = true
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<VignetteEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var alpha = entity.GetFloat("alpha");
		var size = entity.GetFloat("size");
		var persist = entity.GetBool("persist_between_minigames", false);
		var easeIn = entity.GetBool("ease_in", true);
		var easeOut = entity.GetBool("ease_out", true);
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<VignetteRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, alpha, size, persist, easeIn, easeOut));
		}
	}

	private sealed class VignetteRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _alpha;
		private float _size;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private VignetteOverlay? _overlay;
		private Camera? _camera;
		private bool _persistBetweenMinigames;
		private bool _easeIn;
		private bool _easeOut;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float alpha, float size, bool persistBetweenMinigames, bool easeIn, bool easeOut)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_alpha = Mathf.Clamp01(alpha);
			_size = Mathf.Clamp(size, 0f, 0.5f);
			_persistBetweenMinigames = persistBetweenMinigames;
			_easeIn = easeIn;
			_easeOut = easeOut;
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

			if (_persistBetweenMinigames)
				RebindOverlayIfNeeded();

			var currentBeat = _jukebox.CurrentBeat;
			if (currentBeat >= _endBeat)
			{
				Stop();
				return;
			}

			// Fade in over first 20%, hold, fade out over last 20%.
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			var envelope = EffectEnvelope.Evaluate(progress, _easeIn, _easeOut);

			if (_overlay != null)
				_overlay.SetParams(_alpha * envelope, _size);
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

			_overlay = camera.gameObject.AddComponent<VignetteOverlay>();
			_camera = camera;
			_overlay.SetParams(_alpha, _size);
			_initialized = true;
		}

		private void RemoveOverlay()
		{
			if (_overlay != null)
			{
				Destroy(_overlay);
			}

			_overlay = null;
			_camera = null;
		}

		private void RebindOverlayIfNeeded()
		{
			Camera? camera = EffectRuntimeController.ResolveEffectCamera(_loader);
			if (camera is null || _camera == camera)
				return;

			RemoveOverlay();
			_overlay = camera.gameObject.AddComponent<VignetteOverlay>();
			_camera = camera;
			_overlay.SetParams(_alpha, _size);
		}
	}

	/// <summary>
	/// Draws a radial/elliptical vignette in OnPostRender using a 32-segment triangle fan.
	/// The inner clear zone is an ellipse (aspect-corrected to appear circular on screen).
	/// Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class VignetteOverlay : MonoBehaviour
	{
		private const int Segments = 32;

		private static Material? _material;
		private float _alpha;
		private float _size;

		/// <summary>
		/// Updates the overlay parameters.
		/// </summary>
		public void SetParams(float alpha, float size)
		{
			_alpha = alpha;
			_size = size;
		}

		private static Material? GetMaterial()
		{
			if (!_material)
			{
				var shader = Shader.Find("Hidden/Internal-Colored");
				if (shader is null)
					return null;

				_material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
				_material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
				_material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
				_material.SetInt("_Cull", (int)CullMode.Off);
				_material.SetInt("_ZWrite", 0);
			}

			return _material;
		}

		// Returns the screen-boundary intersection point from the center (0.5, 0.5)
		// in the given direction (dx, dy).
		private static void ScreenBoundary(float dx, float dy, out float bx, out float by)
		{
			var tx = (dx != 0f) ? 0.5f / Mathf.Abs(dx) : float.MaxValue;
			var ty = (dy != 0f) ? 0.5f / Mathf.Abs(dy) : float.MaxValue;
			var t = Mathf.Min(tx, ty);
			bx = 0.5f + t * dx;
			by = 0.5f + t * dy;
		}

		// Unity calls this on the camera's GameObject after it finishes rendering the scene.
		private void OnPostRender()
		{
			if (_alpha <= 0f)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			// Get the camera aspect ratio to make the inner clear zone look circular on screen.
			var cam = GetComponent<Camera>();
			var aspect = (cam != null) ? cam.aspect : (16f / 9f);

			// Inner clear ellipse semi-axes.
			// ry = (0.5 - _size): fraction of half screen height that remains transparent.
			// rx = ry / aspect: scaled so the ellipse looks like a circle on screen.
			var innerRy = Mathf.Max(0f, 0.5f - _size);
			var innerRx = (aspect > 0f) ? innerRy / aspect : innerRy;

			var dark = new Color(0f, 0f, 0f, _alpha);
			var clear = new Color(0f, 0f, 0f, 0f);
			var step = 2f * Mathf.PI / Segments;

			mat.SetPass(0);

			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.TRIANGLES);

			for (var i = 0; i < Segments; i++)
			{
				var a0 = i * step;
				var a1 = (i + 1) * step;
				var cos0 = Mathf.Cos(a0);
				var sin0 = Mathf.Sin(a0);
				var cos1 = Mathf.Cos(a1);
				var sin1 = Mathf.Sin(a1);

				// Inner ellipse points (transparent edge of the clear zone).
				var ix0 = 0.5f + cos0 * innerRx;
				var iy0 = 0.5f + sin0 * innerRy;
				var ix1 = 0.5f + cos1 * innerRx;
				var iy1 = 0.5f + sin1 * innerRy;

				// Outer points extended from center to the screen boundary.
				ScreenBoundary(cos0, sin0, out var ox0, out var oy0);
				ScreenBoundary(cos1, sin1, out var ox1, out var oy1);

				// Triangle 1: inner0 (clear) → outer0 (dark) → outer1 (dark)
				GL.Color(clear); GL.Vertex3(ix0, iy0, 0f);
				GL.Color(dark); GL.Vertex3(ox0, oy0, 0f);
				GL.Color(dark); GL.Vertex3(ox1, oy1, 0f);

				// Triangle 2: inner0 (clear) → outer1 (dark) → inner1 (clear)
				GL.Color(clear); GL.Vertex3(ix0, iy0, 0f);
				GL.Color(dark); GL.Vertex3(ox1, oy1, 0f);
				GL.Color(clear); GL.Vertex3(ix1, iy1, 0f);
			}

			GL.End();
			GL.PopMatrix();
		}
	}
}
