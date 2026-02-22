Shader "Hidden/BopVisualEffects_Sepia"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 1)) = 1.0
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
            float _Intensity;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // Per-pixel sepia conversion using the standard Adobe/Kodak sepia tone matrix.
                fixed3 sepia;
                sepia.r = dot(col.rgb, fixed3(0.393, 0.769, 0.189));
                sepia.g = dot(col.rgb, fixed3(0.349, 0.686, 0.168));
                sepia.b = dot(col.rgb, fixed3(0.272, 0.534, 0.131));

                col.rgb = lerp(col.rgb, sepia, _Intensity);
                return col;
            }
            ENDCG
        }
    }
}
