using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    // the game's ToneCurvePass bakes the authored animation curve into a 256x1 ramp
    // texture, publishes it with the level globals, and blits the whole screen.
    public class GallopToneCurveFeature : ScriptableRendererFeature
    {
        private GallopToneCurvePass _pass;

        public override void Create()
        {
            _pass = new GallopToneCurvePass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!GallopToneCurvePass.Enabled)
                return;
            renderer.EnqueuePass(_pass);
        }

        public class GallopToneCurvePass : ScriptableRenderPass
        {
            // fed by the director from the authored tone curve keys each frame.
            public static bool Enabled;
            public static bool IsEnable;
            public static AnimationCurve ToneCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            public static AnimationCurve MaskToneCurve;
            public static Color MinCorrectionLevel = Color.black;
            public static Color MaxCorrectionLevel = Color.white;
            public static Color MaskMinCorrectionLevel = Color.black;
            public static Color MaskMaxCorrectionLevel = Color.white;
            public static float DepthMask;

            private Material _material;
            private RTHandle _temp;

            private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
            private static readonly int CurveTexId = Shader.PropertyToID("_CurveTex");
            private static readonly int MinLevelId = Shader.PropertyToID("_MinLevel");
            private static readonly int MaxLevelId = Shader.PropertyToID("_MaxLevel");
            private static readonly int MaskMinLevelId = Shader.PropertyToID("_MaskMinLevel");
            private static readonly int MaskMaxLevelId = Shader.PropertyToID("_MaskMaxLevel");
            private static readonly int DepthMaskId = Shader.PropertyToID("_DepthMask");

            private Texture2D _curveTex;
            private Texture2D _maskTex;
            private Color32[] _bakeBuffer = new Color32[256];

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(ref _temp, desc, FilterMode.Bilinear, name: "_ToneCurveScratch");

                if (_material == null)
                {
                    var shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.ToneCurve);
                    if (shader != null && shader.isSupported)
                    {
                        _material = new Material(shader) { hideFlags = HideFlags.DontSave };
                        Gallop.Live.Director.FileLog($"[tonecurve] shader loaded, passes={shader.passCount}");
                    }
                    else
                    {
                        Gallop.Live.Director.FileLog("[tonecurve] shader unavailable, track inert");
                        Enabled = false;
                    }
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || !Enabled || !IsEnable)
                    return;

                var cmd = CommandBufferPool.Get("GallopToneCurve");
                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

                _material.SetTexture(MainTexId, source);

                // bake the authored curve exactly like the game: 256 samples of
                // AnimationCurve.Evaluate scaled to bytes.
                if (_curveTex == null)
                {
                    _curveTex = new Texture2D(256, 1, TextureFormat.RGBA32, false, true)
                    {
                        hideFlags = HideFlags.DontSave,
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };
                }
                for (int i = 0; i < 256; i++)
                {
                    float t = i / 255f;
                    float v = ToneCurve != null && ToneCurve.length > 0 ? ToneCurve.Evaluate(t) : t;
                    float m = MaskToneCurve != null && MaskToneCurve.length > 0 ? MaskToneCurve.Evaluate(t) : t;
                    byte vb = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
                    byte mb = (byte)Mathf.Clamp(Mathf.RoundToInt(m * 255f), 0, 255);
                    _bakeBuffer[i] = new Color32(vb, vb, vb, mb);
                }
                _curveTex.SetPixels32(_bakeBuffer);
                _curveTex.Apply(false);

                _material.SetTexture(CurveTexId, _curveTex);
                _material.SetColor(MinLevelId, MinCorrectionLevel);
                _material.SetColor(MaxLevelId, MaxCorrectionLevel);
                _material.SetColor(MaskMinLevelId, MaskMinCorrectionLevel);
                _material.SetColor(MaskMaxLevelId, MaskMaxCorrectionLevel);
                _material.SetFloat(DepthMaskId, DepthMask);

                Blitter.BlitCameraTexture(cmd, source, _temp, _material, 0);
                Blitter.BlitCameraTexture(cmd, _temp, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Cleanup()
            {
                _temp?.Release();
                _temp = null;
                if (_curveTex != null)
                {
                    Object.Destroy(_curveTex);
                    _curveTex = null;
                }
                if (_material != null)
                {
                    Object.Destroy(_material);
                    _material = null;
                }
            }
        }
    }
}
