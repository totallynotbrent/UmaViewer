using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    // mirrors the game's LensDistortionPass: converts the authored fov-style intensity
    // into the shader's amount vector and blits the game's own distortion shader.
    public class GallopLensDistortionFeature : ScriptableRendererFeature
    {
        private GallopLensDistortionPass _pass;

        public override void Create()
        {
            _pass = new GallopLensDistortionPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // authored image effects only apply to the live camera; the menu and
            // freecam never carry the film track state.
            if (!GallopLensDistortionPass.Enabled || GallopLensDistortionPass.ForceDisabled || !IsLiveCamera(renderingData))
                return;
            renderer.EnqueuePass(_pass);
            GallopLensDistortionPass.Enabled = false;
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

        public class GallopLensDistortionPass : ScriptableRenderPass
        {
            // fed by the director from the authored lens distortion keys each frame.
            public static bool Enabled;
            public static bool ForceDisabled;
            public static float Intensity;
            public static float IntensityX = 0.0001f;
            public static float IntensityY = 0.0001f;
            public static float CenterX;
            public static float CenterY;
            public static float Scale = 1f;

            private Material _material;
            private RTHandle _temp;

            private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
            private static readonly int CenterScaleId = Shader.PropertyToID("_LensDistortion_CenterScale");
            private static readonly int AmountId = Shader.PropertyToID("_LensDistortion_Amount");

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(ref _temp, desc, FilterMode.Bilinear, name: "_LensDistortionScratch");

                if (_material == null)
                {
                    var shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.LensDistortion);
                    if (shader != null && shader.isSupported)
                    {
                        _material = new Material(shader) { hideFlags = HideFlags.DontSave };
                        Gallop.Live.Director.FileLog($"[lensdistortion] shader loaded, passes={shader.passCount}");
                    }
                    else
                    {
                        Gallop.Live.Director.FileLog("[lensdistortion] shader unavailable, track inert");
                        Enabled = false;
                    }
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || !Enabled || ForceDisabled)
                    return;

                var cmd = CommandBufferPool.Get("GallopLensDistortion");
                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

                // the game converts the authored degrees-like intensity into an angular
                // amount: clamp to [1,160]*1.6, to radians, half-angle tangent pair.
                float clamped = Mathf.Max(Mathf.Abs(Intensity), 1f) * 1.6f;
                clamped = Mathf.Min(clamped, 160f);
                float radians = clamped * 0.0174533f;
                float halfTan = Mathf.Tan(radians * 0.5f);
                float invRadians = 1f / radians;
                float invScale = 1f / Mathf.Max(Scale, 0.0001f);

                Vector4 amount = new Vector4(invRadians, halfTan * 2f, invScale, Intensity);
                Vector4 centerScale = new Vector4(CenterX, CenterY,
                    Mathf.Max(IntensityX, 0.0001f), Mathf.Max(IntensityY, 0.0001f));

                cmd.SetGlobalVector(CenterScaleId, centerScale);
                cmd.SetGlobalVector(AmountId, amount);
                _material.SetTexture(MainTexId, source);

                Blitter.BlitCameraTexture(cmd, source, _temp, _material, 0);
                Blitter.BlitCameraTexture(cmd, _temp, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Cleanup()
            {
                _temp?.Release();
                _temp = null;
                if (_material != null)
                {
                    Object.Destroy(_material);
                    _material = null;
                }
            }
        }
    }
}
