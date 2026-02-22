using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;

namespace BopVisualEffects.Effects.Hsl;

/// <summary>
/// Effect definition for a per-pixel HSL (Hue/Saturation/Lightness) color filter.
/// Rotates hue, scales saturation, and offsets lightness over the full screen.
/// Uses <see cref="Camera.OnRenderImage"/> with a Cg shader for per-pixel color space conversion.
/// Falls back to a passthrough blit when shader compilation is not available.
/// </summary>
public sealed class HslEffect : IVisualEffectDefinition
{
	/// <inheritdoc />
	public string Id => "hsl";

	/// <inheritdoc />
	public string DisplayName => "HSL Filter";

	/// <inheritdoc />
	public string ConfigKey => "Hsl";

	/// <inheritdoc />
	public string Description => "Adjusts hue, saturation and lightness of the whole screen for creative colour grading.";

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
				["hue_shift"] = 0.0f,
				["saturation"] = 1.0f,
				["lightness"] = 0.0f,
				["intensity"] = 1.0f
			}
		};
	}

	/// <inheritdoc />
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		var log = ClassLogger.GetForClass<HslEffect>();
		var durationBeats = Mathf.Max(0.01f, entity.length);
		var hueShift = entity.GetFloat("hue_shift");
		var saturation = entity.GetFloat("saturation");
		var lightness = entity.GetFloat("lightness");
		var intensity = entity.GetFloat("intensity");
		var startBeat = entity.beat;
		var endBeat = startBeat + durationBeats;

		loader.scheduler.Schedule(startBeat, (System.Action?)SpawnAction);
		log.Debug($"Scheduled '{DisplayName}' from beat {startBeat:0.###} to {endBeat:0.###}.");
		return true;

		void SpawnAction()
		{
			EffectRuntimeController.Instance.SpawnRunner<HslRunner>(runner =>
				runner.Initialize(loader, loader.jukebox, startBeat, endBeat, hueShift, saturation, lightness, intensity));
		}
	}

	private sealed class HslRunner : MonoBehaviour
	{
		private bool _initialized;
		private float _hueShift;
		private float _saturation;
		private float _lightness;
		private float _maxIntensity;
		private float _startBeat;
		private float _endBeat;
		private MixtapeLoaderCustom? _loader;
		private JukeboxScript? _jukebox;
		private HslOverlay? _overlay;

		/// <summary>
		/// Initializes this runner with effect parameters.
		/// </summary>
		public void Initialize(MixtapeLoaderCustom loader, JukeboxScript? jukebox, float startBeat, float endBeat,
			float hueShift, float saturation, float lightness, float intensity)
		{
			_loader = loader;
			_jukebox = jukebox;
			_startBeat = startBeat;
			_endBeat = endBeat;
			_hueShift = Mathf.Clamp(hueShift, -180f, 180f);
			_saturation = Mathf.Max(0f, saturation);
			_lightness = Mathf.Clamp(lightness, -0.5f, 0.5f);
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
				_overlay.SetParams(_hueShift, _saturation, _lightness, _maxIntensity * envelope);
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

			_overlay = camera.gameObject.AddComponent<HslOverlay>();
			_overlay.SetParams(_hueShift, _saturation, _lightness, _maxIntensity);
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
	/// Applies a per-pixel HSL transformation in <see cref="Camera.OnRenderImage"/> using
	/// <see cref="Graphics.Blit(RenderTexture, RenderTexture, Material)"/> with an embedded Cg shader.
	/// Falls back to a passthrough blit when the shader is unavailable at runtime.
	/// Must be attached to a Camera's GameObject.
	/// </summary>
	private sealed class HslOverlay : MonoBehaviour
	{
		// Cg shader for per-pixel HSL transformation. Converts RGB ↔ HSL, applies
		// hue rotation (_HueShift degrees), saturation scaling (_Saturation multiplier),
		// lightness offset (_Lightness additive), then lerps with original by _Intensity.
		private const string ShaderSource = @"Shader ""Hidden/BopVisualEffects_HSL""
{
    Properties
    {
        _MainTex (""Texture"", 2D) = ""white"" {}
        _HueShift (""Hue Shift"", Float) = 0.0
        _Saturation (""Saturation"", Float) = 1.0
        _Lightness (""Lightness"", Float) = 0.0
        _Intensity (""Intensity"", Float) = 1.0
    }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include ""UnityCG.cginc""

            sampler2D _MainTex;
            float _HueShift;
            float _Saturation;
            float _Lightness;
            float _Intensity;

            float3 RgbToHsl(float3 c)
            {
                float maxC = max(c.r, max(c.g, c.b));
                float minC = min(c.r, min(c.g, c.b));
                float delta = maxC - minC;
                float l = (maxC + minC) * 0.5;
                float h = 0.0;
                float s = 0.0;
                if (delta > 1e-5)
                {
                    float denom = 1.0 - abs(2.0 * l - 1.0);
                    s = (denom > 1e-5) ? (delta / denom) : 1.0;
                    if (abs(maxC - c.r) < 1e-5)
                        h = fmod((c.g - c.b) / delta, 6.0);
                    else if (abs(maxC - c.g) < 1e-5)
                        h = (c.b - c.r) / delta + 2.0;
                    else
                        h = (c.r - c.g) / delta + 4.0;
                    h /= 6.0;
                    if (h < 0.0) h += 1.0;
                }
                return float3(h, s, l);
            }

            float3 HslToRgb(float3 hsl)
            {
                float h = hsl.x;
                float s = hsl.y;
                float l = hsl.z;
                float c = (1.0 - abs(2.0 * l - 1.0)) * s;
                float x = c * (1.0 - abs(fmod(h * 6.0, 2.0) - 1.0));
                float m = l - c * 0.5;
                float3 rgb;
                float h6 = h * 6.0;
                if      (h6 < 1.0) rgb = float3(c, x, 0.0);
                else if (h6 < 2.0) rgb = float3(x, c, 0.0);
                else if (h6 < 3.0) rgb = float3(0.0, c, x);
                else if (h6 < 4.0) rgb = float3(0.0, x, c);
                else if (h6 < 5.0) rgb = float3(x, 0.0, c);
                else               rgb = float3(c, 0.0, x);
                return saturate(rgb + m);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float3 hsl = RgbToHsl(col.rgb);
                hsl.x = frac(hsl.x + _HueShift / 360.0);
                hsl.y = saturate(hsl.y * _Saturation);
                hsl.z = saturate(hsl.z + _Lightness);
                float3 adjusted = HslToRgb(hsl);
                col.rgb = lerp(col.rgb, adjusted, _Intensity);
                return col;
            }
            ENDCG
        }
    }
}";

		private static Material? _material;
		private float _hueShift;
		private float _saturation = 1f;
		private float _lightness;
		private float _intensity;

		/// <summary>
		/// Updates the HSL parameters.
		/// </summary>
		public void SetParams(float hueShift, float saturation, float lightness, float intensity)
		{
			_hueShift = hueShift;
			_saturation = saturation;
			_lightness = lightness;
			_intensity = intensity;
		}

		private static Material? GetMaterial()
		{
			if (_material)
				return _material;

#pragma warning disable CS0618 // Material(string) is obsolete for new code but functional in PC standalone builds.
			_material = new Material(ShaderSource) { hideFlags = HideFlags.HideAndDontSave };
#pragma warning restore CS0618
			if (!_material.shader || !_material.shader.isSupported)
			{
				Destroy(_material);
				_material = null;
			}

			return _material;
		}

		// Unity calls this on the camera's GameObject with the rendered image as the source.
		private void OnRenderImage(RenderTexture src, RenderTexture dest)
		{
			if (_intensity <= 0f)
			{
				Graphics.Blit(src, dest);
				return;
			}

			var mat = GetMaterial();
			if (mat is null)
			{
				Graphics.Blit(src, dest);
				return;
			}

			mat.SetFloat("_HueShift", _hueShift);
			mat.SetFloat("_Saturation", _saturation);
			mat.SetFloat("_Lightness", _lightness);
			mat.SetFloat("_Intensity", _intensity);
			Graphics.Blit(src, dest, mat);
		}
	}
}
