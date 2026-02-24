using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.Scanlines;

/// <summary>
/// Effect definition for a CRT-style horizontal scan-line overlay for a retro 8-bit aesthetic.
/// </summary>
public sealed class ScanlinesEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "scanlines";

	/// <inheritdoc />
	public string DisplayName => "Scanlines";

	/// <inheritdoc />
	public string ConfigKey => "Scanlines";

	/// <inheritdoc />
	public string Description => "Draws horizontal CRT-style scan lines over the screen for a retro 8-bit aesthetic.";

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
				["alpha"] = 0.35f,
				["count"] = 60.0f,
				["scroll_speed"] = 0.0f,
				["easing_curve"] = DefaultEasingCurve()
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<ScanlinesEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var alpha = entity.GetFloat("alpha");
		var count = entity.GetFloat("count");
		var scrollSpeed = entity.GetFloat("scroll_speed");
		var easingCurve = entity.GetAnimationCurve("easing_curve", DefaultEasingCurve());
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<ScanlinesRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, alpha, count, scrollSpeed, easingCurve));
		}
	}

	private static AnimationCurve DefaultEasingCurve() => new AnimationCurve(
		new Keyframe(0f, 0f, 0f, 20f / 3f),
		new Keyframe(0.15f, 1f, 20f / 3f, 0f),
		new Keyframe(0.85f, 1f, 0f, -20f / 3f),
		new Keyframe(1f, 0f, -20f / 3f, 0f)
	);

	private sealed class ScanlinesRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _alpha;
		private int _count;
		private float _scrollSpeed;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private ScanlinesOverlay? _overlay;
		private AnimationCurve _easingCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float alpha, float count, float scrollSpeed, AnimationCurve easingCurve)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_alpha = Mathf.Clamp01(alpha);
			_count = Mathf.Clamp(Mathf.RoundToInt(count), 4, 2000);
			_scrollSpeed = scrollSpeed;
			_easingCurve = easingCurve;
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

			// Fade in over first 15%, hold, fade out over last 15%.
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			var envelope = _easingCurve.Evaluate(progress);

			if (_overlay != null)
				_overlay.SetParams(_alpha * envelope, _count, _scrollSpeed);
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

			_overlay = camera.gameObject.AddComponent<ScanlinesOverlay>();
			_overlay.SetParams(_alpha, _count, _scrollSpeed);
			_initialized = true;
		}

		private void RemoveOverlay()
		{
			if (_overlay != null)
			{
				Destroy(_overlay);
			}

			_overlay = null;
		}
	}

	/// <summary>
	/// Draws horizontal CRT scan lines in OnPostRender. Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class ScanlinesOverlay : MonoBehaviour
	{
		private static Material? _material;
		private float _alpha;
		private int _count;
		private float _scrollSpeed;

		/// <summary>
		/// Updates the overlay parameters.
		/// </summary>
		public void SetParams(float alpha, int count, float scrollSpeed)
		{
			_alpha = alpha;
			_count = count;
			_scrollSpeed = scrollSpeed;
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

		// Unity calls this on the camera's GameObject after it finishes rendering the scene.
		private void OnPostRender()
		{
			if (_alpha <= 0f || _count <= 0)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			mat.SetPass(0);

			// Each cell height is 1/count. The scan line occupies the lower half of each cell.
			// scroll_speed shifts lines upward over time (wraps within one cell height).
			var lineColor = new Color(0f, 0f, 0f, _alpha);
			var cellH = 1f / _count;
			var lineH = cellH * 0.5f;
			var scrollOffset = (_scrollSpeed != 0f) ? (Time.time * _scrollSpeed * cellH) % cellH : 0f;

			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.QUADS);
			for (var i = 0; i < _count; i++)
			{
				var y0 = ((float)i / _count + scrollOffset) % 1f;
				var y1 = y0 + lineH;
				GL.Color(lineColor);

				if (y1 <= 1f)
				{
					// Simple case: scan line is fully within the 0..1 vertical range.
					GL.Vertex3(0f, y0, 0f);
					GL.Vertex3(0f, y1, 0f);
					GL.Vertex3(1f, y1, 0f);
					GL.Vertex3(1f, y0, 0f);
				}
				else
				{
					// Wrap case: split into two quads around the top/bottom boundary.
					var wrappedY1 = y1 - 1f;

					// Top segment (y0 to screen top).
					GL.Vertex3(0f, y0, 0f);
					GL.Vertex3(0f, 1f, 0f);
					GL.Vertex3(1f, 1f, 0f);
					GL.Vertex3(1f, y0, 0f);

					// Bottom segment (screen bottom to wrapped height).
					GL.Vertex3(0f, 0f, 0f);
					GL.Vertex3(0f, wrappedY1, 0f);
					GL.Vertex3(1f, wrappedY1, 0f);
					GL.Vertex3(1f, 0f, 0f);
				}
			}

			GL.End();
			GL.PopMatrix();
		}
	}
}
