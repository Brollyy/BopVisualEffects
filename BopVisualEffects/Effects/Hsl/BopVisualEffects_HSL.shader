Shader "Hidden/BopVisualEffects_HSL"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _HueShift ("Hue Shift", Float) = 0.0
        _Saturation ("Saturation", Float) = 1.0
        _Lightness ("Lightness", Float) = 0.0
        _Intensity ("Intensity", Float) = 1.0
    }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

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
}
