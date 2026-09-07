Shader "Hidden/BopVisualEffects_SpeedLines"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Colour ("Colour", Color) = (1,1,1,1)
        _SpeedLinesTiling ("Speed Lines Tiling", Float) = 192
        _SpeedLinesAnimation ("Speed Lines Animation", Float) = 3
        _SpeedLinesRemap ("Speed Lines Remap", Range(0,1)) = 0.65
        _SpeedLinesPower ("Speed Lines Power", Float) = 1
        _Reach ("Reach", Range(0,1)) = 0.25
        _Intensity ("Intensity", Range(0,1)) = 1
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Colour;
            float _SpeedLinesTiling;
            float _SpeedLinesAnimation;
            float _SpeedLinesRemap;
            float _SpeedLinesPower;
            float _Reach;
            float _Intensity;

            float3 mod289(float3 x) { return x - floor(x / 289.0) * 289.0; }
            float2 mod289(float2 x) { return x - floor(x / 289.0) * 289.0; }
            float3 permute(float3 x) { return mod289((x * 34.0 + 1.0) * x); }

            float snoise(float2 v)
            {
                const float4 C = float4(0.2113248654, 0.3660254038, -0.5773502692, 0.0243902439);
                float2 i = floor(v + dot(v, C.yy));
                float2 x0 = v - i + dot(i, C.xx);
                float2 i1 = (x0.x > x0.y) ? float2(1, 0) : float2(0, 1);
                float4 x12 = x0.xyxy + C.xxzz;
                x12.xy -= i1;
                i = mod289(i);
                float3 p = permute(permute(i.y + float3(0, i1.y, 1)) + i.x + float3(0, i1.x, 1));
                float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0);
                m *= m; m *= m;
                float3 x = 2.0 * frac(p * C.www) - 1.0;
                float3 h = abs(x) - 0.5;
                float3 ox = floor(x + 0.5);
                float3 a0 = x - ox;
                m *= 1.7928429 - 0.8537347 * (a0 * a0 + h * h);
                float3 g;
                g.x = a0.x * x0.x + h.x * x0.y;
                g.yz = a0.yz * x12.xz + h.yz * x12.yw;
                return 130.0 * dot(m, g);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 centered = i.uv - 0.5;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 aspectCorrected = float2(centered.x * aspect, centered.y);
                float radial = length(aspectCorrected) * 2.0;
                float angle = atan2(centered.x, centered.y) / 6.28318530718;

                float2 noiseUv = float2(radial * 0.2, angle * _SpeedLinesTiling);
                noiseUv.x -= _SpeedLinesAnimation * _Time.y;
                float noise = snoise(noiseUv) * 0.5 + 0.5;
                float lines = saturate((pow(noise, _SpeedLinesPower) - _SpeedLinesRemap) / max(0.001, 1.0 - _SpeedLinesRemap));

                // Use normalized distance to the nearest rectangular screen edge for the
                // reach mask. The radial coordinate can exceed 1 on a widescreen display;
                // masking it as a circle would incorrectly remove the side edges.
                float edgeDistance = max(abs(centered.x), abs(centered.y)) * 2.0;
                float innerRadius = saturate(1.0 - _Reach);
                float mask = smoothstep(innerRadius, min(1.0, innerRadius + 0.12), edgeDistance);
                float effect = lines * mask * _Intensity * _Colour.a;

                fixed4 scene = tex2D(_MainTex, i.uv);
                scene.rgb = lerp(scene.rgb, _Colour.rgb, effect);
                return scene;
            }
            ENDCG
        }
    }
}
