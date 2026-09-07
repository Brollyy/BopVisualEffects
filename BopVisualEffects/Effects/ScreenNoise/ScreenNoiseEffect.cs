using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.ScreenNoise;

/// <summary>
/// Effect definition for a TV static / glitch noise overlay.
/// </summary>
public sealed class ScreenNoiseEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "screen noise";

	/// <inheritdoc />
	public string DisplayName => "Screen Noise";

	/// <inheritdoc />
	public string ConfigKey => "ScreenNoise";

	/// <inheritdoc />
	public string Description => "Draws animated TV static noise specks over the screen for a glitchy or horror atmosphere.";

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
				["alpha"] = 0.5f,
				["count"] = 400.0f,
				["size"] = 0.01f,
				["ease_in"] = true,
				["ease_out"] = true
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<ScreenNoiseEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var alpha = entity.GetFloat("alpha");
		var count = entity.GetFloat("count");
		var size = entity.GetFloat("size");
		var easeIn = entity.GetBool("ease_in", true);
		var easeOut = entity.GetBool("ease_out", true);
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<ScreenNoiseRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, alpha, count, size, easeIn, easeOut));
		}
	}

	private sealed class ScreenNoiseRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _alpha;
		private int _count;
		private float _size;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private ScreenNoiseOverlay? _overlay;
		private bool _easeIn;
		private bool _easeOut;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float alpha, float count, float size, bool easeIn, bool easeOut)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_alpha = Mathf.Clamp01(alpha);
			_count = Mathf.Clamp(Mathf.RoundToInt(count), 10, 2000);
			_size = Mathf.Clamp(size, 0.005f, 0.1f);
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

			// Fade in over first 15%, hold, fade out over last 15%.
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			var envelope = EffectEnvelope.Evaluate(progress, _easeIn, _easeOut, 0.15f, 0.85f);

			if (_overlay != null)
				_overlay.SetParams(_alpha * envelope, _count, _size);
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

			_overlay = camera.gameObject.AddComponent<ScreenNoiseOverlay>();
			_overlay.SetParams(_alpha, _count, _size);
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
	/// Draws random noise specks in OnPostRender to simulate TV static. Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class ScreenNoiseOverlay : MonoBehaviour
	{
		private static Material? _material;
		private float _alpha;
		private int _count;
		private float _size;

		// Local PRNG to avoid perturbing the global UnityEngine.Random state.
		private readonly System.Random _rng = new System.Random();

		/// <summary>
		/// Updates the overlay parameters.
		/// </summary>
		public void SetParams(float alpha, int count, float size)
		{
			_alpha = alpha;
			_count = count;
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

		// Unity calls this on the camera's GameObject after it finishes rendering the scene.
		private void OnPostRender()
		{
			if (_alpha <= 0f || _count <= 0)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			mat.SetPass(0);

			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.QUADS);
			for (var i = 0; i < _count; i++)
			{
				var x = (float)_rng.NextDouble();
				var y = (float)_rng.NextDouble();
				var brightness = (float)_rng.NextDouble();
				var a = (float)_rng.NextDouble() * _alpha;

				GL.Color(new Color(brightness, brightness, brightness, a));
				GL.Vertex3(x, y, 0f);
				GL.Vertex3(x, y + _size, 0f);
				GL.Vertex3(x + _size, y + _size, 0f);
				GL.Vertex3(x + _size, y, 0f);
			}

			GL.End();
			GL.PopMatrix();
		}
	}
}
