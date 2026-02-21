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
				["r"] = 1.0f,
				["g"] = 0.0f,
				["b"] = 0.0f,
				["alpha"] = 0.25f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<ColorTintEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var r = entity.GetFloat("r");
		var g = entity.GetFloat("g");
		var b = entity.GetFloat("b");
		var alpha = entity.GetFloat("alpha");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<ColorTintRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, r, g, b, alpha));
		}
	}

	private sealed class ColorTintRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _r;
		private float _g;
		private float _b;
		private float _maxAlpha;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private ColorTintOverlay? _overlay;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float r, float g, float b, float alpha)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_r = Mathf.Clamp01(r);
			_g = Mathf.Clamp01(g);
			_b = Mathf.Clamp01(b);
			_maxAlpha = Mathf.Clamp01(alpha);
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

			_overlay?.SetColor(_r, _g, _b, _maxAlpha * envelope);
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
			_overlay.SetColor(_r, _g, _b, _maxAlpha);
			_initialized = true;
		}

		private void RemoveOverlay()
		{
			if (_overlay is not null)
			{
				Destroy(_overlay);
				_overlay = null;
			}
		}
	}

	/// <summary>
	/// Draws a full-screen color tint in OnPostRender. Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class ColorTintOverlay : MonoBehaviour
	{
		private static Material? _material;
		private float _r;
		private float _g;
		private float _b;
		private float _alpha;

		/// <summary>
		/// Updates the overlay color and opacity.
		/// </summary>
		public void SetColor(float r, float g, float b, float alpha)
		{
			_r = r;
			_g = g;
			_b = b;
			_alpha = alpha;
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
			if (_alpha <= 0f)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			mat.SetPass(0);

			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.QUADS);
			GL.Color(new Color(_r, _g, _b, _alpha));
			GL.Vertex3(0f, 0f, 0f);
			GL.Vertex3(0f, 1f, 0f);
			GL.Vertex3(1f, 1f, 0f);
			GL.Vertex3(1f, 0f, 0f);
			GL.End();
			GL.PopMatrix();
		}
	}
}
