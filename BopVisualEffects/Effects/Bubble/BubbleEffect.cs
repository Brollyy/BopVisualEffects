using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.Bubble;

/// <summary>
/// Effect definition for a rising-bubble overlay.
/// Bubbles are generated once as a fixed tileable pattern and scrolled vertically with wrapping.
/// </summary>
public sealed class BubbleEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "bubble";

	/// <inheritdoc />
	public string DisplayName => "Bubble";

	/// <inheritdoc />
	public string ConfigKey => "Bubble";

	/// <inheritdoc />
	public string Description => "Renders randomly-sized bubbles rising from the bottom of the screen, fading in and out smoothly.";

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
				["color"] = new MixtapeEventTemplates.ColorField(new Color(0.6f, 0.85f, 1.0f, 0.5f)),
				["count"] = 20.0f,
				["speed"] = 0.15f,
				["max_size"] = 0.06f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<BubbleEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var color = entity.GetColor("color");
		var count = entity.GetFloat("count");
		var speed = entity.GetFloat("speed");
		var maxSize = entity.GetFloat("max_size");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<BubbleRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, color, count, speed, maxSize));
		}
	}

	private sealed class BubbleRunner : MonoBehaviour
	{
		private bool _initialized;
		private Color _color;
		private int _count;
		private float _speed;
		private float _maxSize;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private BubbleOverlay? _overlay;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, Color color, float count, float speed, float maxSize)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_color = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), Mathf.Clamp01(color.a));
			_count = Mathf.Clamp(Mathf.RoundToInt(count), 5, 200);
			_speed = Mathf.Max(0f, speed);
			_maxSize = Mathf.Clamp(maxSize, 0.01f, 0.5f);
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

			// Fade in over first 20%, hold, fade out over last 20%.
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			float envelope;
			if (progress < 0.2f)
				envelope = Mathf.InverseLerp(0f, 0.2f, progress);
			else if (progress > 0.8f)
				envelope = 1f - Mathf.InverseLerp(0.8f, 1f, progress);
			else
				envelope = 1f;

			if (_overlay != null)
				_overlay.SetParams(new Color(_color.r, _color.g, _color.b, _color.a * envelope), _count, _speed, _maxSize);
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

			_overlay = camera.gameObject.AddComponent<BubbleOverlay>();
			_overlay.SetParams(_color, _count, _speed, _maxSize);
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
	/// Draws rising bubble rings in OnPostRender.
	/// Bubble positions are generated once as a fixed tileable pattern and scrolled vertically.
	/// Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class BubbleOverlay : MonoBehaviour
	{
		private const int CircleSegments = 16;
		private const float InnerRadiusScale = 0.7f;

		private static Material? _material;
		private Color _color;
		private int _count;
		private float _speed;
		private float _maxSize;

		private BubbleData[]? _bubbles;
		private int _lastCount;
		private float _lastMaxSize;

		// Local PRNG to avoid perturbing the global UnityEngine.Random state.
		private readonly System.Random _rng = new System.Random();

		/// <summary>
		/// Updates the overlay parameters and regenerates the bubble pattern when needed.
		/// </summary>
		public void SetParams(Color color, int count, float speed, float maxSize)
		{
			_color = color;
			_count = count;
			_speed = speed;
			_maxSize = maxSize;

			if (_bubbles is null || _lastCount != count || Mathf.Abs(_lastMaxSize - maxSize) > 0.0001f)
				GenerateBubbles();
		}

		private void GenerateBubbles()
		{
			var minRadius = _maxSize * 0.2f;
			_bubbles = new BubbleData[_count];
			for (var i = 0; i < _count; i++)
			{
				_bubbles[i] = new BubbleData(
					x: (float)_rng.NextDouble(),
					y: (float)_rng.NextDouble(),
					radius: minRadius + (float)_rng.NextDouble() * (_maxSize - minRadius));
			}

			_lastCount = _count;
			_lastMaxSize = _maxSize;
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
			if (_color.a <= 0f || _count <= 0 || _bubbles is null)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			var cam = GetComponent<Camera>();
			var aspect = (cam != null) ? cam.aspect : (16f / 9f);
			var scrollOffset = Time.time * _speed;
			var step = 2f * Mathf.PI / CircleSegments;

			mat.SetPass(0);
			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.TRIANGLES);

			foreach (var bubble in _bubbles)
			{
				var ry = bubble.Radius;
				var rx = ry / aspect;
				var cx = bubble.X;

				// Scroll upward with wrapping so the pattern tiles seamlessly.
				var cy = ((bubble.Y + scrollOffset) % 1.0f + 1.0f) % 1.0f;

				DrawBubbleRing(cx, cy, rx, ry, step);

				// Also draw at the adjacent vertical tile edge so bubbles don't pop when wrapping.
				if (cy < ry)
					DrawBubbleRing(cx, cy + 1.0f, rx, ry, step);
				else if (cy > 1.0f - ry)
					DrawBubbleRing(cx, cy - 1.0f, rx, ry, step);
			}

			GL.End();
			GL.PopMatrix();
		}

		private void DrawBubbleRing(float cx, float cy, float rx, float ry, float step)
		{
			for (var seg = 0; seg < CircleSegments; seg++)
			{
				var a0 = seg * step;
				var a1 = (seg + 1) * step;
				var cos0 = Mathf.Cos(a0);
				var sin0 = Mathf.Sin(a0);
				var cos1 = Mathf.Cos(a1);
				var sin1 = Mathf.Sin(a1);

				// Outer ring vertices.
				var ox0 = cx + cos0 * rx;
				var oy0 = cy + sin0 * ry;
				var ox1 = cx + cos1 * rx;
				var oy1 = cy + sin1 * ry;

				// Inner ring vertices (scaled inward to form the ring thickness).
				var ix0 = cx + cos0 * rx * InnerRadiusScale;
				var iy0 = cy + sin0 * ry * InnerRadiusScale;
				var ix1 = cx + cos1 * rx * InnerRadiusScale;
				var iy1 = cy + sin1 * ry * InnerRadiusScale;

				GL.Color(_color);

				// Triangle 1: outer0, outer1, inner0.
				GL.Vertex3(ox0, oy0, 0f);
				GL.Vertex3(ox1, oy1, 0f);
				GL.Vertex3(ix0, iy0, 0f);

				// Triangle 2: outer1, inner1, inner0.
				GL.Vertex3(ox1, oy1, 0f);
				GL.Vertex3(ix1, iy1, 0f);
				GL.Vertex3(ix0, iy0, 0f);
			}
		}

		private readonly struct BubbleData
		{
			public readonly float X;
			public readonly float Y;
			public readonly float Radius;

			public BubbleData(float x, float y, float radius)
			{
				X = x;
				Y = y;
				Radius = radius;
			}
		}
	}
}
