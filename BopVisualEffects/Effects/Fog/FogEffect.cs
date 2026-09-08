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
				["color"] = new MixtapeEventTemplates.ColorField(new Color(0.8f, 0.8f, 0.9f, 0.6f)),
				["height"] = 0.5f,
				["persist_camera"] = false,
				["ease_in"] = true,
				["ease_out"] = true
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<FogEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var color = entity.GetColor("color");
		var height = entity.GetFloat("height");
		var persist = entity.GetBool("persist_camera", false);
		var easeIn = entity.GetBool("ease_in", true);
		var easeOut = entity.GetBool("ease_out", true);
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<FogRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, color, height, persist, easeIn, easeOut));
		}
	}

	private sealed class FogRunner : MonoBehaviour
	{
		private bool _initialized;
		private Color _color;
		private float _height;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private FogOverlay? _overlay;
		private Camera? _camera;
		private bool _persistCamera;
		private bool _easeIn;
		private bool _easeOut;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, Color color, float height, bool persistCamera, bool easeIn, bool easeOut)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_color = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), Mathf.Clamp01(color.a));
			_height = Mathf.Clamp01(height);
			_persistCamera = persistCamera;
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

			if (_persistCamera)
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
				_overlay.SetParams(new Color(_color.r, _color.g, _color.b, _color.a * envelope), _height);
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
			_camera = camera;
			_overlay.SetParams(_color, _height);
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
			_overlay = camera.gameObject.AddComponent<FogOverlay>();
			_camera = camera;
			_overlay.SetParams(_color, _height);
		}
	}

	/// <summary>
	/// Draws a ground fog gradient in OnPostRender — opaque at the bottom, fading to transparent
	/// at the specified height. Works in 2D and 3D scenes. Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class FogOverlay : MonoBehaviour
	{
		private static Material? _material;
		private Color _color;
		private float _height;

		/// <summary>
		/// Updates the overlay parameters.
		/// </summary>
		public void SetParams(Color color, float height)
		{
			_color = color;
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
			if (_color.a <= 0f || _height <= 0f)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			mat.SetPass(0);

			// Ground fog: opaque at y=0 (bottom), fades to transparent at y=_height.
			var clear = new Color(_color.r, _color.g, _color.b, 0f);

			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.QUADS);

			// Bottom-left → top-left → top-right → bottom-right
			GL.Color(_color); GL.Vertex3(0f, 0f, 0f);
			GL.Color(clear); GL.Vertex3(0f, _height, 0f);
			GL.Color(clear); GL.Vertex3(1f, _height, 0f);
			GL.Color(_color); GL.Vertex3(1f, 0f, 0f);

			GL.End();
			GL.PopMatrix();
		}
	}
}
