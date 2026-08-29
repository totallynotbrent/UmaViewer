Shader "Custom/CyalumeSimpleOfficialLike"
{
    Properties
    {
        _MainTex ("Flipbook", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }
        // Note: CGPROGRAM built-in shader kept for look - not SRP Batcher compatible by design (additive cyalume)
        // If converted to HLSLPROGRAM with CBUFFER UnityPerMaterial, SRP Batcher would batch but visual parity not guaranteed.

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Tint)
                UNITY_DEFINE_INSTANCED_PROP(float, _Intensity)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Tint);
                float intensity = UNITY_ACCESS_INSTANCED_PROP(Props, _Intensity);
                fixed3 rgb = tex.rgb * tint.rgb * intensity;
                return fixed4(rgb, 1.0);
            }
            ENDCG
        }
    }
}