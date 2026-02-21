using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.Sepia;

/// <summary>
/// Effect definition for a sepia-tone color filter that gives a warm vintage photograph look.
/// Uses multiply blending to reduce blue and green channels, warming the overall image.
/// </summary>
public sealed class SepiaEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "sepia";

	/// <inheritdoc />
	public string DisplayName => "Sepia";

	/// <inheritdoc />
	public string ConfigKey => "Sepia";

	/// <inheritdoc />
	public string Description => "Applies a sepia-tone color filter for a warm, vintage photograph aesthetic.";

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
				["intensity"] = 0.8f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<SepiaEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var intensity = entity.GetFloat("intensity");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<SepiaRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, intensity));
		}
	}

	private sealed class SepiaRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _maxIntensity;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private SepiaOverlay? _overlay;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float intensity)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_maxIntensity = Mathf.Clamp01(intensity);
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
				_overlay.SetIntensity(_maxIntensity * envelope);
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

			_overlay = camera.gameObject.AddComponent<SepiaOverlay>();
			_overlay.SetIntensity(_maxIntensity);
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
	/// Draws a full-screen sepia-tone multiply overlay in OnPostRender.
	/// Uses multiply blend (SrcBlend=DstColor, DstBlend=Zero) to scale the framebuffer's
	/// green and blue channels downward, warming the image toward sepia tones without
	/// affecting the red channel. Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class SepiaOverlay : MonoBehaviour
	{
		// At full intensity: red is preserved (1.0), green is reduced (0.85), blue is reduced (0.55).
		// The lerp from white means intensity=0 is a no-op and intensity=1 is the full sepia toning.
		private const float SepiaTargetG = 0.85f;
		private const float SepiaTargetB = 0.55f;

		private static Material? _material;
		private float _intensity;

		/// <summary>
		/// Updates the sepia intensity (0 = no effect, 1 = full sepia toning).
		/// </summary>
		public void SetIntensity(float intensity)
		{
			_intensity = intensity;
		}

		private static Material? GetMaterial()
		{
			if (!_material)
			{
				var shader = Shader.Find("Hidden/Internal-Colored");
				if (shader is null)
					return null;

				_material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
				_material.SetInt("_SrcBlend", (int)BlendMode.DstColor);
				_material.SetInt("_DstBlend", (int)BlendMode.Zero);
				_material.SetInt("_Cull", (int)CullMode.Off);
				_material.SetInt("_ZWrite", 0);
			}

			return _material;
		}

		// Unity calls this on the camera's GameObject after it finishes rendering the scene.
		private void OnPostRender()
		{
			if (_intensity <= 0f)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			// Lerp multiply color from white (no-op) toward the warm sepia target.
			var g = Mathf.Lerp(1.0f, SepiaTargetG, _intensity);
			var b = Mathf.Lerp(1.0f, SepiaTargetB, _intensity);

			mat.SetPass(0);

			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.QUADS);
			GL.Color(new Color(1.0f, g, b, 1.0f));
			GL.Vertex3(0f, 0f, 0f);
			GL.Vertex3(0f, 1f, 0f);
			GL.Vertex3(1f, 1f, 0f);
			GL.Vertex3(1f, 0f, 0f);
			GL.End();
			GL.PopMatrix();
		}
	}
}
