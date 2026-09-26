using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    // the game's own dof chain (OnRenderImageDofBloom decode): half-res downsample
    // with coc in alpha, the separable weighted blur (prefilter/blurH/blurV), then
    // the squared-coc composite folded back into the camera color. focus math and
    // coc curve come from the PrepareDofParam decode; blur constants from the
    // SeparableWeightedBlurDof34 dxbc.
    public sealed class GallopDofFeature : ScriptableRendererFeature
    {
        // static state so GallopImageEffect can drive the pass without holding a
        // reference to the renderer-owned feature instance.
        public static bool Active;
        public static float FocusDistance;
        public static float FocalSize;
        public static float BlurSpread = 1f;
        public static float Smoothness = 1f;

        private sealed class DofPass : ScriptableRenderPass
        {
            private Material _material;
            private RTHandle _halfA;
            private RTHandle _halfB;
            private bool _broken;
            private bool _unsupportedLogged;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                desc.width /= 2;
                desc.height /= 2;

                if (_material == null && !_broken)
                {
                    var shader = Shader.Find("Gallop/ImageEffect/SeparableWeightedBlurDof34_CG");
                    if (shader != null && shader.isSupported)
                        _material = new Material(shader) { hideFlags = HideFlags.DontSave };
                    else
                    {
                        _broken = true;
                        if (!_unsupportedLogged)
                        {
                            _unsupportedLogged = true;
                            Gallop.Live.Director.FileLog("[gamedof] shader missing or unsupported; dof stays on the urp volume path");
                        }
                    }
                }

                RenderingUtils.ReAllocateIfNeeded(ref _halfA, desc, FilterMode.Bilinear, name: "_DofHalfA");
                RenderingUtils.ReAllocateIfNeeded(ref _halfB, desc, FilterMode.Bilinear, name: "_DofHalfB");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_broken || _material == null || !Active)
                    return;

                var cmd = CommandBufferPool.Get("GallopDof");
                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                float w = desc.width, h = desc.height;
                float farClip = renderingData.cameraData.camera.farClipPlane;

                // PrepareDofParam decode: the pow curve stays linear (both publishes
                // are 1.0), farBlend widens the sharp band with the focal size, and
                // the vertical scale rides the aspect term.
                float focal01 = Mathf.Clamp01(FocusDistance / Mathf.Max(1f, farClip));
                float farBlend = FocalSize / farClip * 0.5f + focal01;
                _material.SetVector("_CurveParams", new Vector4(1f, 1f, farBlend, (1f / h) * (w / h)));
                _material.SetVector("_InvRenderTargetSize", new Vector4(1f / w, 1f / h, w, h));
                // CalculateMaxCoc decode: the coc cap scales with resolution.
                float maxCoc = Mathf.Min(BlurSpread, (BlurSpread / 50f * 4f + 6f) / h);
                _material.SetFloat("_MaxCoC", maxCoc);
                _material.SetFloat("_Aspect", w / h);
                _material.SetFloat("_bloomDofWeight", 1f);
                _material.SetVector("_Offsets", new Vector4(BlurSpread / w, BlurSpread / h, BlurSpread / w, BlurSpread / h));

                cmd.Blit(source, _halfA, _material, 0);          // prefilter + coc
                cmd.Blit(_halfA, _halfB, _material, 1);           // blurH
                cmd.Blit(_halfB, _halfA, _material, 2);           // blurV
                // the blur result (rgb + preserved coc in alpha) lives in halfA;
                // the composite must read it while writing a DIFFERENT target or
                // the read-while-write hazard blacks the frame.
                _material.SetTexture(_BlurTexId, _halfA);
                cmd.Blit(source, _halfB, _material, 3);           // composite
                Blitter.BlitCameraTexture(cmd, _halfB, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            private static readonly int _BlurTexId = Shader.PropertyToID("_BlurTex");
        }

        private DofPass _pass;

        public override void Create()
        {
            _pass = new DofPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass != null && Active)
                renderer.EnqueuePass(_pass);
        }
    }
}
