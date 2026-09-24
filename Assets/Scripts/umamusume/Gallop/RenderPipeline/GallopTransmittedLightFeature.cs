using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    // mirrors the game's TransmittedLightPass: a FastBloom-style pyramid built from the
    // transmitted-light mask, published as the global _Bloom, composited by the post
    // bloom shader with the authored iterations/intensity blend.
    public class GallopTransmittedLightFeature : ScriptableRendererFeature
    {
        private GallopTransmittedLightPass _pass;

        public override void Create()
        {
            _pass = new GallopTransmittedLightPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // authored image effects only apply to the live camera; the menu and
            // freecam never carry the film track state.
            if (!GallopTransmittedLightPass.Enabled || !IsLiveCamera(renderingData))
                return;
            renderer.EnqueuePass(_pass);
            GallopTransmittedLightPass.Enabled = false;
        }

        // the live camera is the one the timeline drives; identify it by the director
        // reporting an active live and the camera being the live scene's main camera.
        private static bool IsLiveCamera(RenderingData renderingData)
        {
            var director = Gallop.Live.Director.instance;
            if (director == null || !director._isLiveSetup)
                return false;
            var cam = renderingData.cameraData.camera;
            return cam != null && director.MainCameraTransform != null &&
                   cam.transform == director.MainCameraTransform;
        }

        public class GallopTransmittedLightPass : ScriptableRenderPass
        {
            // fed by the director from the authored transmitted light keys each frame.
            public static bool Enabled;
            public static bool IsEnabled;
            public static int Iterations = 3;
            public static float Intensity = 1f;
            public static float Threshold = 0.6f;
            public static float BlurSpread = 1f;
            public static float BlendMode = 1f;

            private Material _fastBloomMaterial;
            private Material _compositeMaterial;
            private RTHandle _bloomA;
            private RTHandle _bloomB;
            private RTHandle _bloomC;
            private RTHandle _temp;

            private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
            private static readonly int ParameterId = Shader.PropertyToID("_Parameter");
            private static readonly int BloomId = Shader.PropertyToID("_Bloom");
            private static readonly int BloomIsScreenBlendId = Shader.PropertyToID("_BloomIsScreenBlend");

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(ref _temp, desc, FilterMode.Bilinear, name: "_TransmittedScratch");

                if (_fastBloomMaterial == null)
                {
                    var fast = ShaderManager.GetShader(ShaderManager.ShaderKinds.FastBloom);
                    var rich = ShaderManager.GetShader(ShaderManager.ShaderKinds.PostBloom_Rich);
                    if (fast != null && fast.isSupported && rich != null && rich.isSupported)
                    {
                        _fastBloomMaterial = new Material(fast) { hideFlags = HideFlags.DontSave };
                        _compositeMaterial = new Material(rich) { hideFlags = HideFlags.DontSave };
                        Gallop.Live.Director.FileLog("[transmitted] fastbloom+postbloom shaders loaded");
                    }
                    else
                    {
                        Gallop.Live.Director.FileLog("[transmitted] shaders unavailable, track inert");
                        Enabled = false;
                    }
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_fastBloomMaterial == null || _compositeMaterial == null || !Enabled || !IsEnabled)
                    return;

                var cmd = CommandBufferPool.Get("GallopTransmittedLight");
                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

                int halfW = renderingData.cameraData.cameraTargetDescriptor.width / 2;
                int halfH = renderingData.cameraData.cameraTargetDescriptor.height / 2;

                // reallocate the pyramid lazily via descriptors matching the game's
                // halving chain.
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                desc.width = Mathf.Max(2, halfW);
                desc.height = Mathf.Max(2, halfH);
                RenderingUtils.ReAllocateIfNeeded(ref _bloomA, desc, FilterMode.Bilinear, name: "_TransmittedA");
                desc.width = Mathf.Max(1, halfW / 2);
                desc.height = Mathf.Max(1, halfH / 2);
                RenderingUtils.ReAllocateIfNeeded(ref _bloomB, desc, FilterMode.Bilinear, name: "_TransmittedB");
                desc.width = Mathf.Max(1, halfW / 4);
                desc.height = Mathf.Max(1, halfH / 4);
                RenderingUtils.ReAllocateIfNeeded(ref _bloomC, desc, FilterMode.Bilinear, name: "_TransmittedC");

                // the game derives the blur offsets from the temp rt texel size with
                // threshold 0 and intensity 1 in the parameter vector.
                Vector4 parameter = new Vector4(
                    (4f / Mathf.Max(2, halfW)) * BlurSpread,
                    (4f / Mathf.Max(2, halfH)) * BlurSpread,
                    Threshold,
                    1f);
                cmd.SetGlobalVector(ParameterId, parameter);

                _fastBloomMaterial.SetTexture(MainTexId, source);
                Blitter.BlitCameraTexture(cmd, source, _bloomA, _fastBloomMaterial, 1);
                _fastBloomMaterial.SetTexture(MainTexId, _bloomA);
                Blitter.BlitCameraTexture(cmd, _bloomA, _bloomB, _fastBloomMaterial, 1);
                _fastBloomMaterial.SetTexture(MainTexId, _bloomB);
                Blitter.BlitCameraTexture(cmd, _bloomB, _bloomC, _fastBloomMaterial, 2);
                _fastBloomMaterial.SetTexture(MainTexId, _bloomC);
                Blitter.BlitCameraTexture(cmd, _bloomC, _bloomA, _fastBloomMaterial, 3);

                cmd.SetGlobalTexture(BloomId, _bloomA);
                cmd.SetGlobalFloat(BloomIsScreenBlendId, BlendMode);
                _compositeMaterial.SetTexture(MainTexId, source);
                Blitter.BlitCameraTexture(cmd, source, _temp, _compositeMaterial, 0);
                Blitter.BlitCameraTexture(cmd, _temp, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Cleanup()
            {
                _bloomA?.Release();
                _bloomB?.Release();
                _bloomC?.Release();
                _temp?.Release();
                _bloomA = _bloomB = _bloomC = _temp = null;
                if (_fastBloomMaterial != null)
                {
                    Object.Destroy(_fastBloomMaterial);
                    _fastBloomMaterial = null;
                }
                if (_compositeMaterial != null)
                {
                    Object.Destroy(_compositeMaterial);
                    _compositeMaterial = null;
                }
            }
        }
    }
}
