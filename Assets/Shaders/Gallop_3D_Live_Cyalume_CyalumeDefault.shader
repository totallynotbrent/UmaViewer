Shader "Gallop_3D_Live_Cyalume_CyalumeDefault"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 5)) = 1.0
        _ScrollSpeed ("Scroll Speed", Vector) = (0, 0, 0, 0)
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }
        
        LOD 100
        
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            
            #include "UnityCG.cginc"
            
            // Group matrix array (11 groups max) - used by CyalumeControllerBase
            float4x4 _CyalumeGroupMatrix[11];
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _Tint;
            float _Intensity;
            float4 _ScrollSpeed;
            
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                // Transform vertex by group matrix if instancing
                float4 transformedVertex = v.vertex;
                
                #if UNITY_INSTANCING_ENABLED
                    int groupIndex = UNITY_GET_INSTANCE_ID(v) % 11;
                    transformedVertex = mul(_CyalumeGroupMatrix[groupIndex], v.vertex);
                #endif
                
                o.vertex = UnityObjectToClipPos(transformedVertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // Apply UV scroll
                o.uv += _ScrollSpeed.xy * _Time.y;
                
                // Use vertex color
                o.color = v.color;
                
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture and apply the color, tint, and intensity
                fixed4 texColor = tex2D(_MainTex, i.uv);
                fixed4 finalColor = texColor * i.color * _Color * _Tint;
                finalColor.rgb *= _Intensity;
                
                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                return finalColor;
            }
            ENDCG
        }
    }
    
    FallBack "Particles/Standard Unlit"
}