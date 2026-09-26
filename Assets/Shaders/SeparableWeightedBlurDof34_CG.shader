// the game's separable weighted-blur DOF (SeparableWeightedBlurDof34), ported from
// the decoded DXBC: prefilter 4-tap with 1.25/1.5 weights, blurH 5x0.2, blurV
// 0.75/0.5 scaled by 1/3.5, composite squared-MAD against the COC texture.
// pass indices follow the game's material: 1 = prefilter, 2 = blurH, 3 = blurV,
// 5 = composite (FindPass names PASS_DOF34_*).
Shader "Gallop/ImageEffect/SeparableWeightedBlurDof34_CG"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        sampler2D _CameraDepthTexture;
        float4 _InvRenderTargetSize;
        float _MaxCoC;
        float _DofFocal01;       // camera-space focal distance / far clip (PrepareDofParam focal01)
        float _DofCocScale;      // the coc scale term (game cb0[140].y)
        float4 _Offsets;         // id 178: the blur-size texel deltas from BlurBlt


        struct vs_blur
        {
            float4 pos : POSITION;
            float2 uv  : TEXCOORD0;
            float4 off1 : TEXCOORD1;
            float4 off2 : TEXCOORD2;
            float4 off3 : TEXCOORD3;
            float4 off4 : TEXCOORD4;
        };

        // the VS emits four sample offsets scaled by the blur deltas (cb0[2] in the
        // game's blob = blur size * invRT); UV0 stays the plain blit uv.
        vs_blur vert_blur(appdata_img v, float4 off1 : TEXCOORD1, float4 off2 : TEXCOORD2,
                          float4 off3 : TEXCOORD3, float4 off4 : TEXCOORD4)
        {
            vs_blur o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.texcoord;
            o.off1 = float4(v.texcoord + _Offsets.xy * 1.0, 0, 0);
            o.off2 = float4(v.texcoord + _Offsets.xy * 2.0, 0, 0);
            o.off3 = float4(v.texcoord + _Offsets.zw * 1.0, 0, 0);
            o.off4 = float4(v.texcoord + _Offsets.zw * 2.0, 0, 0);
            return o;
        }

        // prefilter: the game's downsample-conservent pass, register-exact
        // (postdofbloom_coc_downsample_decoded.md): 9 taps each weighted 1/7 - a
        // deliberate 9/7 gain saturated on write - and the coc computed from the
        // CENTER tap only as (1/z - focal) * scale, eye-space reciprocal depth via
        // the camera z-projection params, with the game's per-pixel noise jitter.
        float4 frag_prefilter(v2f_img i) : SV_Target
        {
            float2 t = _InvRenderTargetSize.xy;
            float4 sum = 0;
            // the 9-tap accumulate loop (center + 4x +offset + 4x -offset).
            [unroll]
            for (int dy = -1; dy <= 1; dy++)
            {
                [unroll]
                for (int dx = -1; dx <= 1; dx++)
                    sum += tex2D(_MainTex, i.uv + float2(dx, dy) * t);
            }
            sum.rgb = saturate(sum.rgb * (1.0 / 7.0));

            // the game jitters the depth sample per pixel (its cb0[138] noise seed)
            // so the coc dithers; a small screen-space hash carries the shimmer.
            float2 noise = frac(sin(dot(i.uv, float2(12.9898, 78.233))) * 43758.5453).xx;
            float2 uvj = i.uv + (noise - 0.5) * t * 2.0;
            float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uvj);
            // 1/(a*z + b) with the z-projection params = the game's cb0[23]
            // linearize; eye distance normalized by the far clip.
            float z01 = 1.0 / (_ZBufferParams.x * rawDepth + _ZBufferParams.y);
            float coc = abs(z01 - _DofFocal01) * _DofCocScale;
            sum.a = min(coc, _MaxCoC);
            return sum;
        }

        // blurH: 5 samples * 0.2; the coc rides the center tap's alpha untouched -
        // the game conserves it through the blur, it never maxes across taps.
        float4 frag_blur_h(vs_blur i) : SV_Target
        {
            float4 center = tex2D(_MainTex, i.uv);
            float4 acc = center * 0.2;
            acc += tex2D(_MainTex, i.off1.xy) * 0.2;
            acc += tex2D(_MainTex, i.off2.xy) * 0.2;
            acc += tex2D(_MainTex, i.off3.xy) * 0.2;
            acc += tex2D(_MainTex, i.off4.xy) * 0.2;
            acc.a = center.a;
            return acc;
        }

        // blurV: taps 0.75/0.5 scaled by 1/3.5; the coc rides the center tap's
        // alpha untouched like the game's downsample-conservent chain.
        float4 frag_blur_v(vs_blur i) : SV_Target
        {
            float2 t = _InvRenderTargetSize.xy;
            float4 center = tex2D(_MainTex, i.uv);
            float4 acc = center * 0.28571428;
            acc += tex2D(_MainTex, i.uv + float2(0, t.y * 0.75)) * 0.28571428 * 0.75;
            acc += tex2D(_MainTex, i.uv - float2(0, t.y * 0.75)) * 0.28571428 * 0.75;
            acc += tex2D(_MainTex, i.uv + float2(t.x * 0.5, t.y * 0.5)) * 0.28571428 * 0.5;
            acc += tex2D(_MainTex, i.uv - float2(t.x * 0.5, t.y * 0.5)) * 0.28571428 * 0.5;
            acc.a = center.a;
            return acc;
        }

        sampler2D _BlurTex;

        // composite: blurred^2 blend against the depth-sampled sharp image, MAD
        // weighted by the CoC. the coc rides the blur chain's alpha channel (the
        // prefilter writes it and the blur passes preserve it), so the composite
        // reads it from _BlurTex instead of a separate coc texture.
        float4 frag_composite(v2f_img i) : SV_Target
        {
            float4 blur = tex2D(_BlurTex, i.uv);
            float coc = blur.a;
            float4 sharp = tex2D(_MainTex, i.uv);
            float4 blurred = blur * blur;
            float w = saturate(coc);
            return blurred * w + sharp * (1.0 - w);
        }
        ENDCG

        Pass // 1 - prefilter
        {
            Name "Prefilter"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_prefilter
            ENDCG
        }
        Pass // 2 - blurH
        {
            Name "BlurH"
            CGPROGRAM
            #pragma vertex vert_blur
            #pragma fragment frag_blur_h
            ENDCG
        }
        Pass // 3 - blurV
        {
            Name "BlurV"
            CGPROGRAM
            #pragma vertex vert_blur
            #pragma fragment frag_blur_v
            ENDCG
        }
        Pass // 5 - composite
        {
            Name "Composite"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_composite
            ENDCG
        }
    }
    Fallback Off
}
