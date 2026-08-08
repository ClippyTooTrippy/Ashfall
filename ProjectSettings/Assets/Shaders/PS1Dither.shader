Shader "Hidden/SoulsLike/PS1Dither"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DitherStrength ("Dither Strength", Range(0,1)) = 0.06
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _DitherStrength;

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // Cheap per-pixel noise dither (stand-in for an ordered Bayer matrix).
                float2 pixel = i.uv * _ScreenParams.xy;
                float noise = frac(sin(dot(floor(pixel), float2(12.9898, 78.233))) * 43758.5453);
                col.rgb += (noise - 0.5) * _DitherStrength;

                // Crush to a 15-bit-ish palette like the PS1 framebuffer.
                col.rgb = floor(saturate(col.rgb) * 31.0) / 31.0;

                return col;
            }
            ENDCG
        }
    }
}
