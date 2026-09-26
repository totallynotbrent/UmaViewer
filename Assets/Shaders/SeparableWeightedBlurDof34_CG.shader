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
        sampler2D _CocTex;
        sampler2D _CameraDepthTexture;
        float4 _CurveParams;     // x,y = pow curve (game leaves linear = 1), z = farBlend, w = offsetY
        float _bloomDofWeight;
        float4 _InvRenderTargetSize;
        float _MaxCoC;
        float _Aspect;
        float4 _Offsets;         // id 178: the blur-size texel deltas from BlurBlt

        static const float WEIGHT_PREFILTER_A = 1.25;
        static const float WEIGHT_PREFILTER_B = 1.5;

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

        // prefilter: downsamples the color and computes the coc from the camera
        // depth with the game's linear curve (both pow publishes are 1.0, so coc
        // is the distance ratio scaled by the curve params); the 1.25/1.5 weights
        // shape the downsample taps like the game's prefilter.
        float4 frag_prefilter(v2f_img i) : SV_Target
        {
            float2 t = _InvRenderTargetSize.xy * WEIGHT_PREFILTER_A;
            float4 c0 = tex2D(_MainTex, i.uv);
            float4 c1 = tex2D(_MainTex, i.uv + t);
            float4 c2 = tex2D(_MainTex, i.uv - t);
            float4 c3 = tex2D(_MainTex, i.uv + float2(t.y, -t.x));
            // the game's downsample-conservent pass normalizes its weighted taps
            // (final MUL by a ~1/7 constant); an unnormalized 4x1.5 sum would feed
            // the squared-blur composite a 6x overbright signal and blow out every
            // blurred region.
            float4 sum = (c0 + c1 + c2 + c3) * WEIGHT_PREFILTER_B / (4.0 * WEIGHT_PREFILTER_B);

            float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
            float eyeDepth = LinearEyeDepth(rawDepth);
            float depth01 = saturate(eyeDepth / _ProjectionParams.z);
            float band = _CurveParams.z;
            float coc = saturate(abs(depth01 - _CurveParams.w) * band);
            coc = min(coc, _MaxCoC);
            sum.a = coc;
            return sum;
        }

        // blurH: 5 samples * 0.2, MAX with the input's own coc alpha
        float4 frag_blur_h(vs_blur i) : SV_Target
        {
            float4 acc = 0;
            acc += tex2D(_MainTex, i.uv) * 0.2;
            acc += tex2D(_MainTex, i.off1.xy) * 0.2;
            acc += tex2D(_MainTex, i.off2.xy) * 0.2;
            acc += tex2D(_MainTex, i.off3.xy) * 0.2;
            acc += tex2D(_MainTex, i.off4.xy) * 0.2;
            // the center tap carries the prefilter's coc in alpha; the offset taps
            // carry their own. take the max across taps so no in-focus detail
            // bleeds through the blur.
            float cocMax = acc.a;
            cocMax = max(cocMax, tex2D(_MainTex, i.off1.xy).a);
            cocMax = max(cocMax, tex2D(_MainTex, i.off2.xy).a);
            cocMax = max(cocMax, tex2D(_MainTex, i.off3.xy).a);
            cocMax = max(cocMax, tex2D(_MainTex, i.off4.xy).a);
            acc.a = max(cocMax, acc.a);
            return acc;
        }

        // blurV: taps 0.75/0.5 scaled by 1/3.5, MAX with the input's own coc alpha
        float4 frag_blur_v(vs_blur i) : SV_Target
        {
            float2 t = _InvRenderTargetSize.xy;
            float4 c0 = tex2D(_MainTex, i.uv);
            float4 c1 = tex2D(_MainTex, i.uv + float2(0, t.y * 0.75));
            float4 c2 = tex2D(_MainTex, i.uv - float2(0, t.y * 0.75));
            float4 c3 = tex2D(_MainTex, i.uv + float2(t.x * 0.5, t.y * 0.5));
            float4 c4 = tex2D(_MainTex, i.uv - float2(t.x * 0.5, t.y * 0.5));
            float4 acc = c0 * 0.28571428;
            acc += c1 * 0.28571428 * 0.75;
            acc += c2 * 0.28571428 * 0.75;
            acc += c3 * 0.28571428 * 0.5;
            acc += c4 * 0.28571428 * 0.5;
            // keep the strongest coc across the taps so the composite never
            // under-blurs a bright region the prefilter marked.
            acc.a = max(max(max(c0.a, c1.a), max(c2.a, c3.a)), max(c4.a, acc.a));
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
