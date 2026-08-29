// ponytail: official 400-variant uber-shader stub - 5-6 files share this CBUFFER. Ceiling: extract real Gallop CharacterToonTSER via AbList["shader"] dump
Shader "Gallop/3D/Character/CharacterToonStub"
{
    Properties { _MainTex("Base", 2D) = "white" {} _ToonMap("Toon",2D)="white"{} _OutlineWidth("Outline",Float)=0 _RimColor("Rim",Color)=(1,1,1,1) _RimStep("RimStep",Float)=0.5 _RimFeather("Feather",Float)=0.2 }
    SubShader {
        Tags{ "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_ToonMap); SAMPLER(sampler_ToonMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float _OutlineWidth; float4 _RimColor; float _RimStep; float _RimFeather;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 viewDirWS:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings vert(Attributes IN){ Varyings OUT; UNITY_SETUP_INSTANCE_ID(IN); UNITY_TRANSFER_INSTANCE_ID(IN,OUT);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS; OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS); OUT.normalWS = n.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(pos.positionWS); return OUT;
            }
            half4 frag(Varyings IN):SV_Target{
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                // ponytail: toon via _ToonMap + rim via viewDotNormal, not full 400 variants
                half NdotV = saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                half rim = smoothstep(_RimStep, _RimStep+_RimFeather, 1-NdotV);
                baseCol.rgb += _RimColor.rgb * rim;
                return baseCol;
            }
            ENDHLSL
        }
    }
}
