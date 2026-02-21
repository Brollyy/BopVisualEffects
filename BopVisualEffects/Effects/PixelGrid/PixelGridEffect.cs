using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.PixelGrid;

/// <summary>
/// Effect definition for a retro 8-bit pixel grid overlay.
/// Draws thin dark lines between pixel cells (horizontal and vertical) to simulate large pixel boundaries.
/// </summary>
public sealed class PixelGridEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "pixel grid";

	/// <inheritdoc />
	public string DisplayName => "Pixel Grid";

	/// <inheritdoc />
	public string Description => "Draws a full pixel grid over the screen to simulate the chunky pixel look of retro 8-bit games.";

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
				["alpha"] = 0.4f,
				["pixel_size"] = 0.025f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<PixelGridEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var alpha = entity.GetFloat("alpha");
		var pixelSize = entity.GetFloat("pixel_size");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<PixelGridRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, alpha, pixelSize));
		}
	}

	private sealed class PixelGridRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _alpha;
		private float _pixelSize;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private PixelGridOverlay? _overlay;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float alpha, float pixelSize)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_alpha = Mathf.Clamp01(alpha);
			_pixelSize = Mathf.Clamp(pixelSize, 0.005f, 0.25f);
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

			_overlay?.SetParams(_alpha * envelope, _pixelSize);
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

			_overlay = camera.gameObject.AddComponent<PixelGridOverlay>();
			_overlay.SetParams(_alpha, _pixelSize);
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
	/// Draws a 2D pixel cell grid (horizontal + vertical dividers) in OnPostRender.
	/// Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class PixelGridOverlay : MonoBehaviour
	{
		// The grid line thickness as a fraction of the cell size.
		private const float LineThicknessFraction = 0.15f;

		private static Material? _material;
		private float _alpha;
		private float _pixelSize;

		/// <summary>
		/// Updates the overlay parameters.
		/// </summary>
		public void SetParams(float alpha, float pixelSize)
		{
			_alpha = alpha;
			_pixelSize = pixelSize;
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
			if (_alpha <= 0f || _pixelSize <= 0f)
				return;

			var mat = GetMaterial();
			if (mat is null)
				return;

			mat.SetPass(0);

			var lineColor = new Color(0f, 0f, 0f, _alpha);
			var lineThickness = _pixelSize * LineThicknessFraction;

			// Number of cells along each axis.
			var cellsX = Mathf.CeilToInt(1f / _pixelSize);
			var cellsY = Mathf.CeilToInt(1f / _pixelSize);

			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.QUADS);
			GL.Color(lineColor);

			// Horizontal dividers between rows.
			for (var row = 1; row < cellsY; row++)
			{
				var y = row * _pixelSize;
				GL.Vertex3(0f, y, 0f);
				GL.Vertex3(0f, y + lineThickness, 0f);
				GL.Vertex3(1f, y + lineThickness, 0f);
				GL.Vertex3(1f, y, 0f);
			}

			// Vertical dividers between columns.
			for (var col = 1; col < cellsX; col++)
			{
				var x = col * _pixelSize;
				GL.Vertex3(x, 0f, 0f);
				GL.Vertex3(x, 1f, 0f);
				GL.Vertex3(x + lineThickness, 1f, 0f);
				GL.Vertex3(x + lineThickness, 0f, 0f);
			}

			GL.End();
			GL.PopMatrix();
		}
	}
}
