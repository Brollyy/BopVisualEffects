using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.ColorTint;

/// <summary>
/// Effect definition for a sustained full-screen color tint that fades in, holds, then fades out.
/// </summary>
public sealed class ColorTintEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "color tint";

	/// <inheritdoc />
	public string DisplayName => "Color Tint";

	/// <inheritdoc />
	public string ConfigKey => "ColorTint";

	/// <inheritdoc />
	public string Description => "Applies a sustained full-screen color tint that fades in and out, for mood or atmosphere.";

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
				["color"] = new MixtapeEventTemplates.ColorField(new Color(1.0f, 0.0f, 0.0f, 0.25f)),
				["easing_curve"] = DefaultEasingCurve()
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<ColorTintEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var color = entity.GetColor("color");
		var easingCurve = entity.GetAnimationCurve("easing_curve", DefaultEasingCurve());
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<ColorTintRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, color, easingCurve));
		}
	}

	private static AnimationCurve DefaultEasingCurve() => new AnimationCurve(
		new Keyframe(0f, 0f, 0f, 5f),
		new Keyframe(0.2f, 1f, 5f, 0f),
		new Keyframe(0.8f, 1f, 0f, -5f),
		new Keyframe(1f, 0f, -5f, 0f)
	);

	private sealed class ColorTintRunner : MonoBehaviour
	{
		private bool _initialized;
		private Color _color;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private ColorTintOverlay? _overlay;
		private AnimationCurve _easingCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, Color color, AnimationCurve easingCurve)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_color = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), Mathf.Clamp01(color.a));
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

			// Fade in over first 20%, hold, fade out over last 20%.
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			var envelope = _easingCurve.Evaluate(progress);

			if (_overlay != null)
				_overlay.SetColor(new Color(_color.r, _color.g, _color.b, _color.a * envelope));
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

			_overlay = camera.gameObject.AddComponent<ColorTintOverlay>();
			_overlay.SetColor(_color);
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
	/// Draws a full-screen color tint in OnPostRender. Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class ColorTintOverlay : MonoBehaviour
	{
		private static Material? _material;
		private Color _color;

		/// <summary>
		/// Updates the overlay color and opacity.
		/// </summary>
		public void SetColor(Color color)
		{
			_color = color;
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
			if (_color.a <= 0f)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			mat.SetPass(0);

			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.QUADS);
			GL.Color(_color);
			GL.Vertex3(0f, 0f, 0f);
			GL.Vertex3(0f, 1f, 0f);
			GL.Vertex3(1f, 1f, 0f);
			GL.Vertex3(1f, 0f, 0f);
			GL.End();
			GL.PopMatrix();
		}
	}
}
