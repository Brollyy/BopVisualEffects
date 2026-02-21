using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BopVisualEffects.Effects.Scanlines;

/// <summary>
/// Effect definition for a CRT-style horizontal scan-line overlay for a retro 8-bit aesthetic.
/// </summary>
public sealed class ScanlinesEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "scanlines";

	/// <inheritdoc />
	public string DisplayName => "Scanlines";

	/// <inheritdoc />
	public string Description => "Draws horizontal CRT-style scan lines over the screen for a retro 8-bit aesthetic.";

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
				["alpha"] = 0.35f,
				["count"] = 60.0f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<ScanlinesEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var alpha = entity.GetFloat("alpha");
		var count = entity.GetFloat("count");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<ScanlinesRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, alpha, count));
		}
	}

	private sealed class ScanlinesRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _alpha;
		private int _count;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private ScanlinesOverlay? _overlay;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float alpha, float count)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_alpha = Mathf.Clamp01(alpha);
			_count = Mathf.Max(4, Mathf.RoundToInt(count));
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

			_overlay?.SetParams(_alpha * envelope, _count);
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

			_overlay = camera.gameObject.AddComponent<ScanlinesOverlay>();
			_overlay.SetParams(_alpha, _count);
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
	/// Draws horizontal CRT scan lines in OnPostRender. Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class ScanlinesOverlay : MonoBehaviour
	{
		private static Material? _material;
		private float _alpha;
		private int _count;

		/// <summary>
		/// Updates the overlay parameters.
		/// </summary>
		public void SetParams(float alpha, int count)
		{
			_alpha = alpha;
			_count = count;
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

			// Each cell height is 1/count. The scan line occupies the lower half of each cell.
			var lineColor = new Color(0f, 0f, 0f, _alpha);
			var lineH = 0.5f / _count;

			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.QUADS);
			for (var i = 0; i < _count; i++)
			{
				var y0 = (float)i / _count;
				var y1 = y0 + lineH;
				GL.Color(lineColor);
				GL.Vertex3(0f, y0, 0f);
				GL.Vertex3(0f, y1, 0f);
				GL.Vertex3(1f, y1, 0f);
				GL.Vertex3(1f, y0, 0f);
			}

			GL.End();
			GL.PopMatrix();
		}
	}
}
