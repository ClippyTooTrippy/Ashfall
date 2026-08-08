Shader "SoulsLike/PS1VertexJitter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GeometryResolution ("Vertex Snap Resolution", Float) = 160
        _FogNear ("Fog Near", Float) = 6
        _FogFar ("Fog Far", Float) = 22
        _FogColor ("Fog Color", Color) = (0.02,0.02,0.03,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _GeometryResolution;
            float _FogNear;
            float _FogFar;
            fixed4 _FogColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;

                // Transform to clip space, then snap XY to a coarse grid before
                // dividing back out - this recreates the PS1's low vertex precision
                // "wobble" as the camera moves.
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                float4 snapped = clipPos;
                snapped.xyz = clipPos.xyz / clipPos.w;
                snapped.x = floor(snapped.x * _GeometryResolution) / _GeometryResolution;
                snapped.y = floor(snapped.y * _GeometryResolution) / _GeometryResolution;
                snapped.xyz *= clipPos.w;
                snapped.w = clipPos.w;

                o.vertex = snapped;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);

                float dist = length(UnityObjectToViewPos(v.vertex));
                o.fogFactor = saturate((dist - _FogNear) / max(0.001, (_FogFar - _FogNear)));

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * _Color;

                // Flat, per-vertex-ish lighting (no smooth normal interpolation tricks).
                float3 normal = normalize(i.worldNormal);
                float ndotl = max(0.15, dot(normal, normalize(_WorldSpaceLightPos0.xyz)));
                fixed3 lit = tex.rgb * ndotl * _LightColor0.rgb;

                // Crush color depth like a 15-bit PS1 framebuffer.
                lit = floor(lit * 31.0) / 31.0;

                fixed3 finalColor = lerp(lit, _FogColor.rgb, i.fogFactor);
                return fixed4(finalColor, tex.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
