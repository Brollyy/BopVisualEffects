using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.Letterbox;

/// <summary>
/// Effect definition for cinematic black bars at the top and bottom of the screen.
/// </summary>
public sealed class LetterboxEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "letterbox";

	/// <inheritdoc />
	public string DisplayName => "Letterbox";

	/// <inheritdoc />
	public string Description => "Adds cinematic black bars at the top and bottom of the screen for a dramatic widescreen feel.";

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
				["size"] = 0.1f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<LetterboxEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var size = entity.GetFloat("size");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<LetterboxRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, size));
		}
	}

	private sealed class LetterboxRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _size;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private LetterboxOverlay? _overlay;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float size)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_size = Mathf.Clamp(size, 0f, 0.49f);
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

			// Slide bars in over first 15%, hold, slide out over last 15%.
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			float envelope;
			if (progress < 0.15f)
				envelope = Mathf.InverseLerp(0f, 0.15f, progress);
			else if (progress > 0.85f)
				envelope = 1f - Mathf.InverseLerp(0.85f, 1f, progress);
			else
				envelope = 1f;

			_overlay?.SetParams(_size * envelope);
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

			_overlay = camera.gameObject.AddComponent<LetterboxOverlay>();
			_overlay.SetParams(_size);
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
	/// Draws solid black bars at the top and bottom of the screen in OnPostRender. Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class LetterboxOverlay : MonoBehaviour
	{
		private static Material? _material;
		private float _barSize;

		/// <summary>
		/// Updates the bar height as a normalized screen fraction.
		/// </summary>
		public void SetParams(float barSize)
		{
			_barSize = barSize;
		}

		private static Material? GetMaterial()
		{
			if (!_material)
			{
				var shader = Shader.Find("Hidden/Internal-Colored");
				if (shader is null)
					return null;

				_material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
				_material.SetInt("_SrcBlend", (int)BlendMode.One);
				_material.SetInt("_DstBlend", (int)BlendMode.Zero);
				_material.SetInt("_Cull", (int)CullMode.Off);
				_material.SetInt("_ZWrite", 0);
			}

			return _material;
		}

		// Unity calls this on the camera's GameObject after it finishes rendering the scene.
		private void OnPostRender()
		{
			if (_barSize <= 0f)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			mat.SetPass(0);

			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.QUADS);

			var black = new Color(0f, 0f, 0f, 1f);

			// Bottom bar.
			GL.Color(black);
			GL.Vertex3(0f, 0f, 0f);
			GL.Vertex3(0f, _barSize, 0f);
			GL.Vertex3(1f, _barSize, 0f);
			GL.Vertex3(1f, 0f, 0f);

			// Top bar.
			GL.Color(black);
			GL.Vertex3(0f, 1f - _barSize, 0f);
			GL.Vertex3(0f, 1f, 0f);
			GL.Vertex3(1f, 1f, 0f);
			GL.Vertex3(1f, 1f - _barSize, 0f);

			GL.End();
			GL.PopMatrix();
		}
	}
}
