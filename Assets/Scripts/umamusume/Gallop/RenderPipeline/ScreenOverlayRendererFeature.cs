using Gallop.ImageEffect;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// URP bridge for the reconstructed ScreenOverlayRender implementation.
    /// It is inert until a ScreenOverlay component and compatible material are assigned.
    /// </summary>
    public sealed class ScreenOverlayRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
            public ScreenOverlay overlay;
            public Material material;
            public int defaultPass = 0;
            public int filmPass1st = 1;
            public int filmPass2nd = 2;
            public bool skipSceneView = true;
            public bool skipPreviewCamera = true;
        }

        private sealed class OverlayPass : ScriptableRenderPass
        {
            private readonly ScreenOverlayRendererFeature owner;
            private readonly ScreenOverlayRender renderer = new ScreenOverlayRender();
            private readonly ProfilingSampler overlayProfilingSampler = new ProfilingSampler("Uma Screen Overlay");

            public OverlayPass(ScreenOverlayRendererFeature owner)
            {
                this.owner = owner;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                Settings settings = owner.settings;
                CameraData cameraData = renderingData.cameraData;
                if (settings == null || settings.overlay == null || settings.material == null ||
                    !settings.overlay.IsEnable ||
                    (settings.skipSceneView && cameraData.isSceneViewCamera) ||
                    (settings.skipPreviewCamera && cameraData.isPreviewCamera))
                    return;

                RenderTargetIdentifier sourceTarget = cameraData.renderer.cameraColorTarget;
                int width = Mathf.Max(1, cameraData.camera.pixelWidth);
                int height = Mathf.Max(1, cameraData.camera.pixelHeight);
                RenderTextureHandle source = RenderTextureHandle.Make(sourceTarget, width, height);
                RenderTextureHandle destination = RenderTextureHandle.Make(
                    Shader.PropertyToID("ScreenOverlayRendererFeature_Destination"), width, height);

                CommandBuffer command = CommandBufferPool.Get();
                using (new ProfilingScope(command, overlayProfilingSampler))
                {
                    destination.GetTemporaryRT(command, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                    ScreenOverlayRender.Parameter first = ScreenOverlayRender.Parameter.Default();
                    ScreenOverlayRender.Parameter second = ScreenOverlayRender.Parameter.Default();
                    ScreenOverlayRender.Parameter third = ScreenOverlayRender.Parameter.Default();
                    first.Setup(settings.overlay.Overlay1);
                    second.Setup(settings.overlay.Overlay2);
                    third.Setup(settings.overlay.Overlay3);
                    renderer.PostFilmBlit(
                        context,
                        command,
                        source,
                        destination,
                        settings.material,
                        ref first,
                        ref second,
                        ref third,
                        settings.defaultPass,
                        settings.filmPass1st,
                        settings.filmPass2nd);
                    command.Blit(destination.RtId, sourceTarget);
                    destination.ReleaseTemporaryRT(command);
                }

                context.ExecuteCommandBuffer(command);
                CommandBufferPool.Release(command);
            }
        }

        public Settings settings = new Settings();
        private OverlayPass pass;

        public override void Create()
        {
            pass = new OverlayPass(this)
            {
                renderPassEvent = settings != null ? settings.passEvent : RenderPassEvent.AfterRenderingTransparents
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || settings.overlay == null || settings.material == null || !settings.overlay.IsEnable)
                return;

            if (pass == null)
                Create();
            pass.renderPassEvent = settings.passEvent;
            renderer.EnqueuePass(pass);
        }
    }
}
