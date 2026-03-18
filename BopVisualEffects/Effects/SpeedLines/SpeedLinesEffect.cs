using System;
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
	/// <inheritdoc />
	public string Id => "speed lines";

	/// <inheritdoc />
	public string DisplayName => "Speed Lines";

	/// <inheritdoc />
	public string ConfigKey => "SpeedLines";

	/// <inheritdoc />
	public string Description => "Draws animated black triangles radiating inward from the screen edges for a high-speed rush feeling.";

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
				["count"] = 20.0f,
				["speed"] = 3.5f
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
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<SpeedLinesRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, alpha, count, speed));
		}
	}

	private sealed class SpeedLinesRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _alpha;
		private int _count;
		private float _speed;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private SpeedLinesOverlay? _overlay;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float alpha, float count, float speed)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_alpha = Mathf.Clamp01(alpha);
			_count = Mathf.Clamp(Mathf.RoundToInt(count), 4, 64);
			_speed = Mathf.Max(0.1f, speed);
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
			float envelope;
			if (progress < 0.15f)
				envelope = Mathf.InverseLerp(0f, 0.15f, progress);
			else if (progress > 0.85f)
				envelope = 1f - Mathf.InverseLerp(0.85f, 1f, progress);
			else
				envelope = 1f;

			if (_overlay != null)
				_overlay.SetParams(_alpha * envelope, _speed);
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

			_overlay = camera.gameObject.AddComponent<SpeedLinesOverlay>();
			_overlay.Initialize(_count, _startBeat);
			_overlay.SetParams(_alpha, _speed);
			_initialized = true;
		}

		private void RemoveOverlay()
		{
			if (_overlay != null)
				Destroy(_overlay);
			_overlay = null;
		}
	}

	/// <summary>
	/// Draws animated black speed-line triangles in OnPostRender.
	/// Each triangle points inward from the screen edge toward the center,
	/// flickering at an independently randomised phase to create a high-speed rush effect.
	/// Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class SpeedLinesOverlay : MonoBehaviour
	{
		// Distance from screen centre to the triangle tip, in screen-height fractions.
		private const float InnerRadius = 0.12f;

		// Half-width of the triangle base at the screen boundary, in screen-height fractions.
		private const float HalfWidth = 0.025f;

		private static Material? _material;
		private float _alpha;
		private float _speed;
		private float[]? _angles;
		private float[]? _phases;

		/// <summary>
		/// Generates the per-triangle angular positions and flicker phase offsets
		/// using a deterministic seed so the layout is stable for each effect instance.
		/// </summary>
		public void Initialize(int count, float seed)
		{
			var rng = new Random(Mathf.FloorToInt(seed * 100f) + count * 7919);
			_angles = new float[count];
			_phases = new float[count];
			for (var i = 0; i < count; i++)
			{
				// Stratified random angles: distribute evenly around the screen with a small
				// random jitter so the layout looks natural but not perfectly uniform.
				var baseAngle = 2f * Mathf.PI * i / count;
				var jitter = (float)(rng.NextDouble() - 0.5) * (2f * Mathf.PI / count) * 0.5f;
				_angles[i] = baseAngle + jitter;
				_phases[i] = (float)(rng.NextDouble() * 2.0 * Math.PI);
			}
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

		// Returns the screen-boundary intersection from the centre (0.5, 0.5) in
		// direction (dx, dy), expressed in GL ortho space (x: 0..1, y: 0..1).
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
			if (_alpha <= 0f || _angles is null || _phases is null)
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

			for (var i = 0; i < _angles.Length; i++)
			{
				// Squaring the sine value produces sharper bright pulses separated by longer dark
				// periods, reinforcing the high-speed feel. Each triangle uses a unique phase
				// so they flash at different times rather than all at once.
				var squaredSin = Mathf.Sin(time * _speed + _phases[i]);
				squaredSin *= squaredSin;
				var flickerAlpha = squaredSin * _alpha;
				if (flickerAlpha <= 0.005f)
					continue;

				var angle = _angles[i];
				var cosA = Mathf.Cos(angle);
				var sinA = Mathf.Sin(angle);

				// Direction and perpendicular in GL ortho space (x-axis scaled by invAspect
				// so angles and widths appear visually correct on screen).
				var dirX = cosA * invAspect;
				var dirY = sinA;

				// Triangle tip: InnerRadius away from screen centre along the visual direction.
				var tipX = 0.5f + cosA * InnerRadius * invAspect;
				var tipY = 0.5f + sinA * InnerRadius;

				// Triangle base: centred on the screen boundary at this angle.
				ScreenBoundary(dirX, dirY, out var bx, out var by);

				// Perpendicular in GL ortho space (visual unit vector → GL representation).
				var perpX = -sinA * invAspect;
				var perpY = cosA;

				// Offset the base centre by ±HalfWidth along the perpendicular.
				var base1X = bx + perpX * HalfWidth;
				var base1Y = by + perpY * HalfWidth;
				var base2X = bx - perpX * HalfWidth;
				var base2Y = by - perpY * HalfWidth;

				var color = new Color(0f, 0f, 0f, flickerAlpha);
				GL.Color(color);
				GL.Vertex3(tipX, tipY, 0f);
				GL.Vertex3(base1X, base1Y, 0f);
				GL.Vertex3(base2X, base2Y, 0f);
			}

			GL.End();
			GL.PopMatrix();
		}
	}
}
