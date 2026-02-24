using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;

namespace BopVisualEffects.Effects.PixelGrid;

/// <summary>
/// Effect definition for a true retro 8-bit pixelation effect.
/// Downsamples the rendered frame to a low resolution and upsamples it with nearest-neighbour filtering,
/// averaging all colours within each pixel block — producing a genuine pixelated look.
/// </summary>
public sealed class PixelGridEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "pixel grid";

	/// <inheritdoc />
	public string DisplayName => "Pixel Grid";

	/// <inheritdoc />
	public string ConfigKey => "PixelGrid";

	/// <inheritdoc />
	public string Description => "Pixelates the screen by averaging pixel blocks, simulating the chunky look of retro 8-bit games.";

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
				["pixel_size"] = 4.0f,
				["easing_curve"] = DefaultEasingCurve()
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<PixelGridEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var pixelSize = entity.GetFloat("pixel_size");
		var easingCurve = entity.GetAnimationCurve("easing_curve", DefaultEasingCurve());
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<PixelGridRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, pixelSize, easingCurve));
		}
	}

	private static AnimationCurve DefaultEasingCurve() => new AnimationCurve(
		new Keyframe(0f, 0f, 0f, 20f / 3f),
		new Keyframe(0.15f, 1f, 20f / 3f, 0f),
		new Keyframe(0.85f, 1f, 0f, -20f / 3f),
		new Keyframe(1f, 0f, -20f / 3f, 0f)
	);

	private sealed class PixelGridRunner : MonoBehaviour
	{
		private bool _initialized;
		private int _pixelSize;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private PixelGridOverlay? _overlay;
		private AnimationCurve _easingCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat, float pixelSize, AnimationCurve easingCurve)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_pixelSize = Mathf.Clamp(Mathf.RoundToInt(pixelSize), 2, 64);
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

			// Ramp block size up over first 15%, hold, ramp down over last 15%.
			// block_size=1 means no pixelation; ramping from 1 → _pixelSize gives a "zooming into pixels" look.
			var progress = Mathf.InverseLerp(_startBeat, _endBeat, currentBeat);
			var envelope = _easingCurve.Evaluate(progress);

			var currentBlockSize = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, _pixelSize, envelope)));
			if (_overlay != null)
				_overlay.SetBlockSize(currentBlockSize);
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
			_overlay.SetBlockSize(_pixelSize);
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
	/// Pixelates the camera output via OnRenderImage: downsamples to a low-resolution RenderTexture
	/// with nearest-neighbour filtering, then upsamples back — averaging all colours within each block.
	/// Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class PixelGridOverlay : MonoBehaviour
	{
		private int _blockSize = 1;

		/// <summary>
		/// Sets the pixel block size in screen pixels.
		/// </summary>
		public void SetBlockSize(int blockSize)
		{
			_blockSize = blockSize;
		}

		// Unity calls this with the camera's rendered image as source.
		private void OnRenderImage(RenderTexture src, RenderTexture dest)
		{
			if (_blockSize <= 1 || src is null)
			{
				Graphics.Blit(src, dest);
				return;
			}

			// Downsample to a low-resolution RT using the source descriptor to preserve
			// format/HDR/sRGB settings, then upsample with point (nearest-neighbour) filtering.
			var lowW = Mathf.Max(1, src.width / _blockSize);
			var lowH = Mathf.Max(1, src.height / _blockSize);

			var descriptor = src.descriptor;
			descriptor.width = lowW;
			descriptor.height = lowH;

			RenderTexture? lowRes = null;
			try
			{
				lowRes = RenderTexture.GetTemporary(descriptor);
				lowRes.filterMode = FilterMode.Point;

				Graphics.Blit(src, lowRes);
				Graphics.Blit(lowRes, dest);
			}
			finally
			{
				if (lowRes != null)
					RenderTexture.ReleaseTemporary(lowRes);
			}
		}
	}
}
