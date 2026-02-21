using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.Horror;

/// <summary>
/// Effect definition for a pulsing blood-red vignette overlay for a horror atmosphere.
/// Blood-red tinted gradient edges throb with a slow sinusoidal pulse to evoke dread.
/// </summary>
public sealed class HorrorEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "horror";

	/// <inheritdoc />
	public string DisplayName => "Horror";

	/// <inheritdoc />
	public string Description => "Draws a pulsing blood-red vignette that throbs at the screen edges for a horror atmosphere.";

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
				["alpha"] = 0.75f,
				["size"] = 0.35f,
				["pulse_rate"] = 0.5f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<HorrorEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var alpha = entity.GetFloat("alpha");
		var size = entity.GetFloat("size");
		var pulseRate = entity.GetFloat("pulse_rate");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<HorrorRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, alpha, size, pulseRate));
		}
	}

	private sealed class HorrorRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _alpha;
		private float _size;
		private float _pulseRate;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private HorrorOverlay? _overlay;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float alpha, float size, float pulseRate)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_alpha = Mathf.Clamp01(alpha);
			_size = Mathf.Clamp(size, 0f, 0.5f);
			_pulseRate = Mathf.Max(0.01f, pulseRate);
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

			// Sinusoidal pulse between 50% and 100% of max alpha — creates slow, dreadful throb.
			var beatPhase = (currentBeat - _startBeat) * _pulseRate * (2f * Mathf.PI);
			var pulse = 0.5f + 0.5f * Mathf.Sin(beatPhase);
			var pulsingAlpha = _alpha * envelope * (0.5f + 0.5f * pulse);

			_overlay?.SetParams(pulsingAlpha, _size);
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

			_overlay = camera.gameObject.AddComponent<HorrorOverlay>();
			_overlay.SetParams(_alpha, _size);
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
	/// Draws a blood-red gradient vignette in OnPostRender. Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class HorrorOverlay : MonoBehaviour
	{
		private static Material? _material;
		private float _alpha;
		private float _size;

		/// <summary>
		/// Updates the overlay parameters.
		/// </summary>
		public void SetParams(float alpha, float size)
		{
			_alpha = alpha;
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
			if (_alpha <= 0f)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			mat.SetPass(0);

			// Blood red at the outer edges, transparent towards the center.
			var blood = new Color(0.55f, 0f, 0f, _alpha);
			var clear = new Color(0.55f, 0f, 0f, 0f);
			var s = _size;

			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.QUADS);

			// Left panel: blood red (x=0) → transparent (x=s).
			GL.Color(blood); GL.Vertex3(0f, 0f, 0f);
			GL.Color(blood); GL.Vertex3(0f, 1f, 0f);
			GL.Color(clear); GL.Vertex3(s, 1f, 0f);
			GL.Color(clear); GL.Vertex3(s, 0f, 0f);

			// Right panel: transparent (x=1-s) → blood red (x=1).
			GL.Color(clear); GL.Vertex3(1f - s, 0f, 0f);
			GL.Color(clear); GL.Vertex3(1f - s, 1f, 0f);
			GL.Color(blood); GL.Vertex3(1f, 1f, 0f);
			GL.Color(blood); GL.Vertex3(1f, 0f, 0f);

			// Bottom panel: blood red (y=0) → transparent (y=s).
			GL.Color(blood); GL.Vertex3(0f, 0f, 0f);
			GL.Color(clear); GL.Vertex3(0f, s, 0f);
			GL.Color(clear); GL.Vertex3(1f, s, 0f);
			GL.Color(blood); GL.Vertex3(1f, 0f, 0f);

			// Top panel: transparent (y=1-s) → blood red (y=1).
			GL.Color(clear); GL.Vertex3(0f, 1f - s, 0f);
			GL.Color(blood); GL.Vertex3(0f, 1f, 0f);
			GL.Color(blood); GL.Vertex3(1f, 1f, 0f);
			GL.Color(clear); GL.Vertex3(1f, 1f - s, 0f);

			GL.End();
			GL.PopMatrix();
		}
	}
}
