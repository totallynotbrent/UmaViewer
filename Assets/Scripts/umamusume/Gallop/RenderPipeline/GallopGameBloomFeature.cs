using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// renders the authored bloom + diffusion with the game's own FastBloom shader
    /// from shader.a instead of the URP volume approximation; the game's post look
    /// is defined by these passes.
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
            public static Color BloomTint = Color.white;

            private Material _fastBloomMaterial;
            private RTHandle _tempA;
            private RTHandle _tempB;

            private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
            private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
            private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
            private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            private static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");
            private static readonly int TintColorId = Shader.PropertyToID("_BloomTintColor");

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;

                RenderingUtils.ReAllocateIfNeeded(ref _tempA, desc, FilterMode.Bilinear, name: "_GameBloomA");
                RenderingUtils.ReAllocateIfNeeded(ref _tempB, desc, FilterMode.Bilinear, name: "_GameBloomB");

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

                _fastBloomMaterial.SetFloat(ThresholdId, Threshold);
                _fastBloomMaterial.SetFloat(IntensityId, Intensity);
                _fastBloomMaterial.SetFloat(BlurSizeId, BlurSize);
                _fastBloomMaterial.SetColor(TintColorId, BloomTint);

                // classic FastBloom layout: 0 = threshold downsample, 1 = vertical blur,
                // 2 = horizontal blur, 3 = composite; pass names logged on first load so a
                // different layout is visible in the log.
                Blit(cmd, source, _tempA, _fastBloomMaterial, 0);
                Blit(cmd, _tempA, _tempB, _fastBloomMaterial, 1);
                Blit(cmd, _tempB, _tempA, _fastBloomMaterial, 2);

                _fastBloomMaterial.SetTexture(BloomTexId, _tempA);
                Blit(cmd, source, _tempB, _fastBloomMaterial, 3);
                Blit(cmd, _tempB, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Cleanup()
            {
                _tempA?.Release();
                _tempB?.Release();
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                // rt handles are pooled; nothing per-frame to free.
            }
        }
    }
}
