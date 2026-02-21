using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.Fog;

/// <summary>
/// Effect definition for a 2D-compatible fog overlay that rises from the bottom of the screen.
/// </summary>
public sealed class FogEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "fog";

	/// <inheritdoc />
	public string DisplayName => "Fog";

	/// <inheritdoc />
	public string ConfigKey => "Fog";

	/// <inheritdoc />
	public string Description => "Draws a ground fog overlay that rises from the bottom of the screen, fading in and out over its duration.";

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
				["r"] = 0.8f,
				["g"] = 0.8f,
				["b"] = 0.9f,
				["alpha"] = 0.6f,
				["height"] = 0.5f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<FogEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var r = entity.GetFloat("r");
		var g = entity.GetFloat("g");
		var b = entity.GetFloat("b");
		var alpha = entity.GetFloat("alpha");
		var height = entity.GetFloat("height");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<FogRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, r, g, b, alpha, height));
		}
	}

	private sealed class FogRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _r;
		private float _g;
		private float _b;
		private float _maxAlpha;
		private float _height;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private FogOverlay? _overlay;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float r, float g, float b, float alpha, float height)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_r = Mathf.Clamp01(r);
			_g = Mathf.Clamp01(g);
			_b = Mathf.Clamp01(b);
			_maxAlpha = Mathf.Clamp01(alpha);
			_height = Mathf.Clamp01(height);
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
				_overlay.SetParams(_r, _g, _b, _maxAlpha * envelope, _height);
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

			_overlay = camera.gameObject.AddComponent<FogOverlay>();
			_overlay.SetParams(_r, _g, _b, _maxAlpha, _height);
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
	/// Draws a ground fog gradient in OnPostRender — opaque at the bottom, fading to transparent
	/// at the specified height. Works in 2D and 3D scenes. Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class FogOverlay : MonoBehaviour
	{
		private static Material? _material;
		private float _r;
		private float _g;
		private float _b;
		private float _alpha;
		private float _height;

		/// <summary>
		/// Updates the overlay parameters.
		/// </summary>
		public void SetParams(float r, float g, float b, float alpha, float height)
		{
			_r = r;
			_g = g;
			_b = b;
			_alpha = alpha;
			_height = height;
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
			if (_alpha <= 0f || _height <= 0f)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			mat.SetPass(0);

			// Ground fog: opaque at y=0 (bottom), fades to transparent at y=_height.
			var fogColor = new Color(_r, _g, _b, _alpha);
			var clear = new Color(_r, _g, _b, 0f);

			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.QUADS);

			// Bottom-left → top-left → top-right → bottom-right
			GL.Color(fogColor); GL.Vertex3(0f, 0f, 0f);
			GL.Color(clear); GL.Vertex3(0f, _height, 0f);
			GL.Color(clear); GL.Vertex3(1f, _height, 0f);
			GL.Color(fogColor); GL.Vertex3(1f, 0f, 0f);

			GL.End();
			GL.PopMatrix();
		}
	}
}
