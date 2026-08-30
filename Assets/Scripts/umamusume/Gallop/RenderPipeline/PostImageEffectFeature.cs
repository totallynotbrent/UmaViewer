using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Optional URP entry point for scene-level post image effects.
    /// The actual overlay data is supplied by ScreenOverlayRendererFeature.
    /// </summary>
    public sealed class PostImageEffectFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent passEvent = RenderPassEvent.AfterRendering;
        }

        private sealed class Pass : ScriptableRenderPass
        {
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // Reserved for scene-level effects that do not have overlay data.
                // Intentionally no-op until a compatible shader/material is available.
            }
        }

        public Settings settings = new Settings();
        private Pass pass;

        public override void Create()
        {
            pass = new Pass
            {
                renderPassEvent = settings != null ? settings.passEvent : RenderPassEvent.AfterRendering
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Do not enqueue a no-op pass. This feature remains an explicit extension point.
        }
    }
}
