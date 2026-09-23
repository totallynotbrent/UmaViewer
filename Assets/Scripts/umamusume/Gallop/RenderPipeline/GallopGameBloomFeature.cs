using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// renders the authored bloom with the game's own FastBloom shader exactly the
    /// way the game drives it: a half-res pyramid built with passes 1,1,2,3 into a
    /// texture published as the global _Bloom, then a pass-0 composite that reads
    /// the globals _Bloom, _BloomIsScreenBlend and _bloomDofWeight.
    /// </summary>
    public class GallopGameBloomFeature : ScriptableRendererFeature
    {
        private GallopGameBloomPass _pass;

        public override void Create()
        {
            _pass = new GallopGameBloomPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!GallopGameBloomPass.GameBloomEnabled)
                return;
            renderer.EnqueuePass(_pass);
        }

        public class GallopGameBloomPass : ScriptableRenderPass
        {
            // set by GallopImageEffect when the game shader path is active for the frame.
            public static bool GameBloomEnabled;
            public static float Intensity = 1f;
            public static float Threshold = 0.8f;
            public static float BlurSize = 3f;
            public static float BloomDofWeight = 1f;
            public static float BloomIsScreenBlend = 1f;

            private Material _fastBloomMaterial;
            private RTHandle _bloomA;
            private RTHandle _bloomB;
            private RTHandle _bloomC;
            private RTHandle _composite;

            private static readonly int ParameterId = Shader.PropertyToID("_Parameter");
            private static readonly int BloomId = Shader.PropertyToID("_Bloom");
            private static readonly int BloomIsScreenBlendId = Shader.PropertyToID("_BloomIsScreenBlend");
            private static readonly int BloomDofWeightId = Shader.PropertyToID("_bloomDofWeight");

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;

                // the composite needs a full-res target that is never the same
                // texture the shader samples as the global _Bloom (a read-while-write
                // hazard renders black on most drivers).
                RenderingUtils.ReAllocateIfNeeded(ref _composite, desc, FilterMode.Bilinear, name: "_GameBloomComposite");

                // the game's bloom pyramid runs at half resolution and below.
                desc.width /= 2;
                desc.height /= 2;
                RenderingUtils.ReAllocateIfNeeded(ref _bloomA, desc, FilterMode.Bilinear, name: "_GameBloomA");
                desc.width /= 2;
                desc.height /= 2;
                RenderingUtils.ReAllocateIfNeeded(ref _bloomB, desc, FilterMode.Bilinear, name: "_GameBloomB");
                desc.width /= 2;
                desc.height /= 2;
                RenderingUtils.ReAllocateIfNeeded(ref _bloomC, desc, FilterMode.Bilinear, name: "_GameBloomC");

                if (_fastBloomMaterial == null)
                {
                    var shader = Gallop.ShaderManager.GetShader(Gallop.ShaderManager.ShaderKinds.FastBloom);
                    if (shader != null && shader.isSupported)
                    {
                        _fastBloomMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                        Debug.Log($"[gamebloom] FastBloom shader loaded, passCount={shader.passCount}");
                    }
                    else
                    {
                        Debug.LogWarning("[gamebloom] game FastBloom shader unavailable, falling back to URP bloom");
                        GameBloomEnabled = false;
                    }
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_fastBloomMaterial == null || !GameBloomEnabled)
                    return;

                var cmd = CommandBufferPool.Get("GameFastBloom");
                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

                int halfW = renderingData.cameraData.cameraTargetDescriptor.width / 2;
                int halfH = renderingData.cameraData.cameraTargetDescriptor.height / 2;

                // the game publishes the blur offsets and the authored threshold and
                // intensity together as the global _Parameter vector, mirroring its
                // CreateBloomTexture: x/y are blur texel offsets, z/w the auth values.
                float blur = Mathf.Max(0.5f, BlurSize);
                Vector4 parameter = new Vector4(
                    (4f / halfW) * blur,
                    (4f / halfH) * blur,
                    Threshold,
                    Intensity);
                cmd.SetGlobalVector(ParameterId, parameter);

                // pass 1: threshold downsample at half res (the game's first pyramid blit).
                Blit(cmd, source, _bloomA, _fastBloomMaterial, 1);

                // passes 1, 2, 3: the descending blur ladder the game's
                // CreateBloomTexture walks through its temporary textures.
                Blit(cmd, _bloomA, _bloomB, _fastBloomMaterial, 1);
                Blit(cmd, _bloomB, _bloomC, _fastBloomMaterial, 2);
                Blit(cmd, _bloomC, _bloomA, _fastBloomMaterial, 3);

                // pass 0 is the composite: it reads the globals _Bloom, _BloomIsScreenBlend
                // and _bloomDofWeight, exactly like the game's OnRenderImageFastBloom.
                // it must write to a texture other than _Bloom itself or the
                // sampled texel and the written texel are the same memory.
                cmd.SetGlobalTexture(BloomId, _bloomA);
                cmd.SetGlobalFloat(BloomIsScreenBlendId, BloomIsScreenBlend);
                cmd.SetGlobalFloat(BloomDofWeightId, BloomDofWeight);
                Blit(cmd, source, _composite, _fastBloomMaterial, 0);
                Blit(cmd, _composite, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Cleanup()
            {
                _bloomA?.Release();
                _bloomB?.Release();
                _bloomC?.Release();
                _composite?.Release();
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                // rt handles are pooled; nothing per-frame to free.
            }
        }
    }
}
