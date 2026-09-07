using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.SpeedLines;

/// <summary>
/// Effect definition for animated speed-line triangles radiating inward from the screen edges.
/// </summary>
public sealed class SpeedLinesEffect : IVisualEffectDefinition
{
	private const float DefaultReach = 0.25f;

	/// <inheritdoc />
	public string Id => "speed lines";

	/// <inheritdoc />
	public string DisplayName => "Speed Lines";

	/// <inheritdoc />
	public string ConfigKey => "SpeedLines";

	/// <inheritdoc />
	public string Description => "Applies an animated, colored radial speed-line post-processing effect for a high-speed rush feeling.";

	/// <inheritdoc />
	public MixtapeEventTemplate CreateTemplate(string pluginGuid)
	{
		return new MixtapeEventTemplate
		{
			dataModel = $"{pluginGuid}/{Id}",
			length = 2.0f,
			resizable = true,
			properties = new Dictionary<string, object>
			{
				["alpha"] = 0.8f,
				["count"] = 96.0f,
				["speed"] = 3.5f,
				["reach"] = DefaultReach,
				["color"] = new MixtapeEventTemplates.ColorField(Color.white),
				["ease_in"] = true,
				["ease_out"] = true
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<SpeedLinesEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var alpha = entity.GetFloat("alpha");
		var count = entity.GetFloat("count");
		var speed = entity.GetFloat("speed");
		var reach = entity.GetFloat("reach");
		var color = entity.GetColor("color");
		var easeIn = entity.GetBool("ease_in", true);
		var easeOut = entity.GetBool("ease_out", true);
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;
		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<SpeedLinesRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, alpha, count, speed, reach, color, easeIn, easeOut));
		}
	}

	private sealed class SpeedLinesRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _alpha;
		private int _count;
		private float _speed;
		private float _reach;
		private Color _color;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private Camera? _camera;
		private SpeedLinesRequest? _request;
		private bool _easeIn;
		private bool _easeOut;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float alpha, float count, float speed, float reach, Color color, bool easeIn, bool easeOut)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_alpha = Mathf.Clamp01(alpha);
			_count = Mathf.Clamp(Mathf.RoundToInt(count), 24, 192);
			_speed = Mathf.Max(0.1f, speed);
			_reach = Mathf.Clamp01(reach);
			_color = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), Mathf.Clamp01(color.a));
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

			var currentBeat = _jukebox.CurrentBeat;
			if (currentBeat >= _endBeat)
			{
				Stop();
				return;
			}

			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			var envelope = EffectEnvelope.Evaluate(progress, _easeIn, _easeOut, 0.15f, 0.85f);

			if (_request != null)
				_request.Alpha = _alpha * envelope;
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

			_camera = camera;
			_request = SpeedLinesService.AddRequest(camera);
			_request.Alpha = _alpha;
			_request.Count = _count;
			_request.Speed = _speed;
			_request.Reach = _reach;
			_request.Color = _color;
			_initialized = true;
		}

		private void RemoveOverlay()
		{
			if (_camera != null && _request != null)
				SpeedLinesService.RemoveRequest(_camera, _request);
			_camera = null;
			_request = null;
		}
	}

	/// <summary>
	/// Draws animated radial speed-line streaks in OnPostRender.
	/// Each streak has an independent phase, speed, and length so the effect remains visible
	/// while continuously moving toward the screen edge.
	/// Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class SpeedLinesOverlay : MonoBehaviour
	{
		// Half-width of a streak at the outer end, in screen-height fractions.
		private const float HalfWidth = 0.009f;

		private static Material? _material;
		private float _alpha;
		private float _speed;
		private float _reach;
		private Color _color;
		private int _count;
		private float _seed;

		/// <summary>
		/// Generates the per-triangle angular positions and flicker phase offsets
		/// using a deterministic seed so the layout is stable for each effect instance.
		/// </summary>
		public void Initialize(int count, float seed, float reach, Color color)
		{
			_count = count;
			_seed = seed;
			_reach = Mathf.Clamp01(reach);
			_color = color;
		}

		/// <summary>
		/// Updates the maximum opacity and flicker speed applied to all speed lines.
		/// </summary>
		public void SetParams(float alpha, float speed)
		{
			_alpha = alpha;
			_speed = speed;
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

		// Returns the screen-boundary intersection from the center (0.5, 0.5) in
		// direction (dx, dy), expressed in GL ortho space (x: 0..1, y: 0..1).
		private static void ScreenBoundary(float dx, float dy, out float bx, out float by)
		{
			var tx = (dx != 0f) ? 0.5f / Mathf.Abs(dx) : float.MaxValue;
			var ty = (dy != 0f) ? 0.5f / Mathf.Abs(dy) : float.MaxValue;
			var t = Mathf.Min(tx, ty);
			bx = 0.5f + t * dx;
			by = 0.5f + t * dy;
		}

		private static float Hash01(int x, int y)
		{
			var value = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
			return value - Mathf.Floor(value);
		}

		// Unity calls this on the camera's GameObject after it finishes rendering the scene.
		private void OnPostRender()
		{
			if (_alpha <= 0f || _count <= 0 || _reach <= 0f)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			var cam = GetComponent<Camera>();
			// invAspect is used to convert the visual direction to GL ortho space.
			var aspect = (cam != null) ? cam.aspect : (16f / 9f);
			var invAspect = (aspect > 0f) ? 1f / aspect : 1f;

			var time = Time.time;

			mat.SetPass(0);
			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.TRIANGLES);

			var innerLimit = 1f - _reach;
			for (var i = 0; i < _count; i++)
			{
				var angleNoise = Hash01(i, Mathf.FloorToInt(_seed * 100f));
				var angle = 2f * Mathf.PI * (i + 0.5f + (angleNoise - 0.5f) * 0.7f) / _count;
				var cosA = Mathf.Cos(angle);
				var sinA = Mathf.Sin(angle);

				// Direction and perpendicular in GL ortho space (x-axis scaled by invAspect
				// so angles and widths appear visually correct on screen).
				var dirX = cosA * invAspect;
				var dirY = sinA;

				// Find the screen-edge endpoint for this aspect-corrected visual ray.
				ScreenBoundary(dirX, dirY, out var edgeX, out var edgeY);

				// Perpendicular in GL ortho space (visual unit vector → GL representation).
				var perpX = -sinA * invAspect;
				var perpY = cosA;

				var phase = Hash01(i + 17, Mathf.FloorToInt(_seed * 100f));
				var rate = Mathf.Lerp(0.75f, 1.35f, Hash01(i + 31, Mathf.FloorToInt(_seed * 47f)));
				var progress = Mathf.Repeat(time * _speed * rate + phase, 1f);
				var innerT = Mathf.Lerp(innerLimit, 1f, progress);
				var outerT = Mathf.Min(1f, innerT + Mathf.Lerp(0.14f, 0.32f, Hash01(i + 53, Mathf.FloorToInt(_seed * 71f))));
				if (outerT - innerT <= 0.001f)
					continue;

				var innerX = Mathf.Lerp(0.5f, edgeX, innerT);
				var innerY = Mathf.Lerp(0.5f, edgeY, innerT);
				var outerX = Mathf.Lerp(0.5f, edgeX, outerT);
				var outerY = Mathf.Lerp(0.5f, edgeY, outerT);
				var innerWidth = HalfWidth * 0.12f;
				var outerWidth = HalfWidth * Mathf.Lerp(0.7f, 1.15f, outerT);
				var pulse = 0.75f + 0.25f * Mathf.Sin(time * _speed * rate * (2f * Mathf.PI) + phase * 2f * Mathf.PI);
				GL.Color(new Color(_color.r, _color.g, _color.b, _alpha * _color.a * pulse));
				GL.Vertex3(innerX + perpX * innerWidth, innerY + perpY * innerWidth, 0f);
				GL.Vertex3(outerX + perpX * outerWidth, outerY + perpY * outerWidth, 0f);
				GL.Vertex3(outerX - perpX * outerWidth, outerY - perpY * outerWidth, 0f);
				GL.Vertex3(innerX + perpX * innerWidth, innerY + perpY * innerWidth, 0f);
				GL.Vertex3(outerX - perpX * outerWidth, outerY - perpY * outerWidth, 0f);
				GL.Vertex3(innerX - perpX * innerWidth, innerY - perpY * innerWidth, 0f);
			}

			GL.End();
			GL.PopMatrix();
		}
	}
}
