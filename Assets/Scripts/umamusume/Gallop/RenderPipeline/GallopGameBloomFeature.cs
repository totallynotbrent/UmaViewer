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

            // the postfilm block the game's screen-overlay chain publishes every
            // frame; the postbloom composite samples these alongside the bloom.
            public static float PostFilmPower = 1f;
            public static Vector4 PostFilmOffsetParam = Vector4.one;
            public static Vector4 PostFilmOptionParam = Vector4.one;
            public static Color PostFilmColor0 = Color.white;
            public static Color PostFilmColor1 = Color.white;
            public static Color PostFilmColor2 = Color.white;
            public static Color PostFilmColor3 = Color.white;
            public static float PostFilmIsInverseVignette;

            private Material _fastBloomMaterial;
            private Material _postBloomMaterial;
            private RTHandle _bloomA;
            private RTHandle _bloomB;
            private RTHandle _bloomC;
            private RTHandle _composite;
            private int _lastBloomW = -1;
            private int _lastBloomH = -1;
            private int _lastLoggedW = -1;
            private int _lastLoggedH = -1;

            private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
            private static readonly int PostFilmPowerId = Shader.PropertyToID("_PostFilmPower");
            private static readonly int PostFilmOffsetParamId = Shader.PropertyToID("_PostFilmOffsetParam");
            private static readonly int PostFilmOptionParamId = Shader.PropertyToID("_PostFilmOptionParam");
            private static readonly int PostFilmColor0Id = Shader.PropertyToID("_PostFilmColor0");
            private static readonly int PostFilmColor1Id = Shader.PropertyToID("_PostFilmColor1");
            private static readonly int PostFilmColor2Id = Shader.PropertyToID("_PostFilmColor2");
            private static readonly int PostFilmColor3Id = Shader.PropertyToID("_PostFilmColor3");
            private static readonly int PostFilmIsInverseVignetteId = Shader.PropertyToID("_PostFilmIsInverseVignette");
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

                if (_fastBloomMaterial == null)
                {
                    var shader = Gallop.ShaderManager.GetShader(Gallop.ShaderManager.ShaderKinds.FastBloom);
                    var postShader = Gallop.ShaderManager.GetShader(Gallop.ShaderManager.ShaderKinds.PostBloom_Rich);
                    if (shader != null && shader.isSupported && postShader != null && postShader.isSupported)
                    {
                        _fastBloomMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                        _postBloomMaterial = new Material(postShader) { hideFlags = HideFlags.DontSave };
                        Debug.Log($"[gamebloom] pyramid=FastBloom({shader.passCount}p) composite=PostBloom_Rich({postShader.passCount}p)");
                        Gallop.Live.Director.FileLog($"[gamebloom] pyramid=FastBloom({shader.passCount}p) composite=PostBloom_Rich({postShader.passCount}p)");
                    }
                    else
                    {
                        Debug.LogWarning("[gamebloom] game bloom shaders unavailable (FastBloom=" + (shader != null) + " PostBloom_Rich=" + (postShader != null) + "), falling back to URP bloom");
                        Gallop.Live.Director.FileLog($"[gamebloom] shaders unavailable fastbloom={shader != null} postbloom={postShader != null}, falling back to urp bloom");
                        GameBloomEnabled = false;
                    }
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_fastBloomMaterial == null || _postBloomMaterial == null || !GameBloomEnabled)
                    return;

                var cmd = CommandBufferPool.Get("GameFastBloom");
                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

                int srcW = renderingData.cameraData.cameraTargetDescriptor.width;
                int srcH = renderingData.cameraData.cameraTargetDescriptor.height;

                // the game's CreateBloomTexture runs the whole pyramid at ONE reduced
                // resolution (source/4 normally, source/2 on its high quality path) with
                // three same-res blits through passes 1, 2 and 3.
                int bloomW = Mathf.Max(1, srcW / 4);
                int bloomH = Mathf.Max(1, srcH / 4);
                if (bloomW != _lastBloomW || bloomH != _lastBloomH)
                {
                    _lastBloomW = bloomW;
                    _lastBloomH = bloomH;
                    var bloomDesc = renderingData.cameraData.cameraTargetDescriptor;
                    bloomDesc.depthBufferBits = 0;
                    bloomDesc.msaaSamples = 1;
                    bloomDesc.width = bloomW;
                    bloomDesc.height = bloomH;
                    RenderingUtils.ReAllocateIfNeeded(ref _bloomA, bloomDesc, FilterMode.Bilinear, name: "_GameBloomA");
                    RenderingUtils.ReAllocateIfNeeded(ref _bloomB, bloomDesc, FilterMode.Bilinear, name: "_GameBloomB");
                    RenderingUtils.ReAllocateIfNeeded(ref _bloomC, bloomDesc, FilterMode.Bilinear, name: "_GameBloomC");
                }

                // the parameter vector mirrors the game exactly: x/y are the blur texel
                // scale (aspect-corrected, 1/512 units), z/w the authored threshold and
                // intensity straight from the param object.
                float blur = Mathf.Max(0.5f, BlurSize);
                float aspect = (float)srcW / Mathf.Max(1, srcH);
                Vector4 parameter = new Vector4(
                    blur / aspect * 0.00195312f,
                    blur * 0.00195312f,
                    Threshold,
                    Intensity);
                cmd.SetGlobalVector(ParameterId, parameter);
                if (_lastLoggedW != bloomW || _lastLoggedH != bloomH)
                {
                    _lastLoggedW = bloomW;
                    _lastLoggedH = bloomH;
                    Gallop.Live.Director.FileLog($"[gamebloom] pyramid res={bloomW}x{bloomH} param=({parameter.x:F5},{parameter.y:F5},{parameter.z:F3},{parameter.w:F2})");
                }

                // the game shaders sample _MainTex, so every blit binds it explicitly;
                // the urp blitter owns _BlitTexture and does not set _MainTex itself.
                _fastBloomMaterial.SetTexture(MainTexId, source);
                Blitter.BlitCameraTexture(cmd, source, _bloomA, _fastBloomMaterial, 1);
                _fastBloomMaterial.SetTexture(MainTexId, _bloomA);
                Blitter.BlitCameraTexture(cmd, _bloomA, _bloomB, _fastBloomMaterial, 2);
                _fastBloomMaterial.SetTexture(MainTexId, _bloomB);
                Blitter.BlitCameraTexture(cmd, _bloomB, _bloomC, _fastBloomMaterial, 3);

                // the composite is a different shader in the game: PostBloom_Rich pass 0
                // (the pass class keeps two materials, _fastBloomMaterial at +0x130 for
                // the pyramid and _bloomMaterial at +0x120 for the composite). it reads
                // the globals _Bloom, _BloomIsScreenBlend and _bloomDofWeight, and must
                // write to a texture other than _Bloom itself or the sampled texel and
                // the written texel are the same memory.
                cmd.SetGlobalTexture(BloomId, _bloomA);
                cmd.SetGlobalFloat(BloomIsScreenBlendId, BloomIsScreenBlend);
                cmd.SetGlobalFloat(BloomDofWeightId, BloomDofWeight);
                cmd.SetGlobalFloat(PostFilmPowerId, PostFilmPower);
                cmd.SetGlobalVector(PostFilmOffsetParamId, PostFilmOffsetParam);
                cmd.SetGlobalVector(PostFilmOptionParamId, PostFilmOptionParam);
                cmd.SetGlobalColor(PostFilmColor0Id, PostFilmColor0);
                cmd.SetGlobalColor(PostFilmColor1Id, PostFilmColor1);
                cmd.SetGlobalColor(PostFilmColor2Id, PostFilmColor2);
                cmd.SetGlobalColor(PostFilmColor3Id, PostFilmColor3);
                cmd.SetGlobalFloat(PostFilmIsInverseVignetteId, PostFilmIsInverseVignette);
                _postBloomMaterial.SetTexture(MainTexId, source);
                Blitter.BlitCameraTexture(cmd, source, _composite, _postBloomMaterial, 0);
                Blitter.BlitCameraTexture(cmd, _composite, source);

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
