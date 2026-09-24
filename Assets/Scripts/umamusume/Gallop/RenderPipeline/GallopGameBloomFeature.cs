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
            if (!GallopGameBloomPass.GameBloomEnabled || GallopGameBloomPass.ForceDisabled)
                return;
            // only the live main camera owns the bloom; mirror and multi cameras would
            // thrash the shared pyramid textures at their own resolutions.
            var cam = renderingData.cameraData.camera;
            var director = Gallop.Live.Director.instance;
            if (director == null || !director._isLiveSetup)
                return;
            if (cam == null || director.MainCameraTransform == null ||
                cam.transform != director.MainCameraTransform)
                return;
            renderer.EnqueuePass(_pass);
        }

        public class GallopGameBloomPass : ScriptableRenderPass
        {
            // set by GallopImageEffect when the game shader path is active for the frame.
            public static bool GameBloomEnabled;
            public static bool DiffusionEnabled;
            public static bool ForceDisabled;
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
            public static float DepthPower = 1f;
            public static float DepthClip = 2f;
            public static Vector4 PostFilmRollParameter = new Vector4(0f, 1f, 0f, 1f);
            public static Vector4 PostFilmScaleParameter = new Vector4(1f, 1f, 0f, 0f);
            public static float PostFilmIsAlphaMasking;
            public static float PostFilmIsWithoutDepth;

            private Material _fastBloomMaterial;
            private Material _postBloomMaterial;
            private Material _diffusionBloomMaterial;
            private RTHandle _bloomA;
            private RTHandle _bloomB;
            private RTHandle _bloomC;
            private RTHandle _composite;
            private int _lastBloomW = -1;
            private int _lastBloomH = -1;
            private float _nextProbeTime = -1f;
            private bool _envLogged;
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
            private static readonly int DepthPowerId = Shader.PropertyToID("_DepthPower");
            private static readonly int DepthClipId = Shader.PropertyToID("_DepthClip");
            private static readonly int PostFilmRollParameterId = Shader.PropertyToID("_PostFilmRollParameter");
            private static readonly int PostFilmScaleParameterId = Shader.PropertyToID("_PostFilmScaleParameter");
            private static readonly int PostFilmIsAlphaMaskingId = Shader.PropertyToID("_PostFilmIsAlphaMasking");
            private static readonly int PostFilmIsWithoutDepthId = Shader.PropertyToID("_PostFilmIsWithoutDepth");
            private static readonly int ColorBlendFactorId = Shader.PropertyToID("_colorBlendFactor");
            private static readonly int ParameterId = Shader.PropertyToID("_Parameter");
            private static readonly int BloomId = Shader.PropertyToID("_Bloom");
            private static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
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
                        DumpCompositeMaterialDefaults();
                        var diffusionShader = Gallop.ShaderManager.GetShader(Gallop.ShaderManager.ShaderKinds.PostDiffusionBloom_Rich);
                        if (diffusionShader != null && diffusionShader.isSupported)
                            _diffusionBloomMaterial = new Material(diffusionShader) { hideFlags = HideFlags.DontSave };
                        Debug.Log($"[gamebloom] pyramid=FastBloom({shader.passCount}p) composite=PostBloom_Rich({postShader.passCount}p) diffusion=PostDiffusionBloom_Rich({(diffusionShader != null ? diffusionShader.passCount : 0)}p)");
                        Gallop.Live.Director.FileLog($"[gamebloom] pyramid=FastBloom({shader.passCount}p) composite=PostBloom_Rich({postShader.passCount}p) diffusion=PostDiffusionBloom_Rich({(diffusionShader != null ? diffusionShader.passCount : 0)}p)");
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
                if (_fastBloomMaterial == null || _postBloomMaterial == null || !GameBloomEnabled || ForceDisabled)
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
                // the diffusion path feeds the screen aspect and a halved blur; the
                // plain bloom path feeds the blur texel scale in 1/512 units.
                Vector4 parameter = DiffusionEnabled
                    ? new Vector4(aspect, blur * 0.5f, Threshold, Intensity)
                    : new Vector4(
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
                // the game's pyramid: blit1 thresholds source into A with the screen
                // size and authored values in the parameter, blit2 runs pass 1 again
                // with a neutral parameter, then passes 2 and 3 blur through B and C.
                _fastBloomMaterial.SetTexture(MainTexId, source);
                Blitter.BlitCameraTexture(cmd, source, _bloomA, _fastBloomMaterial, 1);
                _fastBloomMaterial.SetTexture(MainTexId, _bloomA);
                Blitter.BlitCameraTexture(cmd, _bloomA, _bloomB, _fastBloomMaterial, 1);
                _fastBloomMaterial.SetTexture(MainTexId, _bloomB);
                Blitter.BlitCameraTexture(cmd, _bloomB, _bloomC, _fastBloomMaterial, 2);
                _fastBloomMaterial.SetTexture(MainTexId, _bloomC);
                Blitter.BlitCameraTexture(cmd, _bloomC, _bloomB, _fastBloomMaterial, 3);

                // the composite is a different shader in the game: PostBloom_Rich pass 0
                // (the pass class keeps two materials, _fastBloomMaterial at +0x130 for
                // the pyramid and _bloomMaterial at +0x120 for the composite). it reads
                // the globals _Bloom, _BloomIsScreenBlend and _bloomDofWeight, and must
                // write to a texture other than _Bloom itself or the sampled texel and
                // the written texel are the same memory.
                cmd.SetGlobalTexture(BloomId, _bloomB);
                // the composite samples _CameraDepthTexture to weight bloom by
                // distance; urp binds it only when the camera requires depth, which the
                // director now turns on for the live camera.
                cmd.SetGlobalTexture(
                    CameraDepthTextureId,
                    renderingData.cameraData.renderer.cameraDepthTargetHandle);
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
                cmd.SetGlobalFloat(DepthPowerId, DepthPower);
                cmd.SetGlobalFloat(DepthClipId, DepthClip);
                cmd.SetGlobalVector(PostFilmRollParameterId, PostFilmRollParameter);
                cmd.SetGlobalVector(PostFilmScaleParameterId, PostFilmScaleParameter);
                cmd.SetGlobalFloat(PostFilmIsAlphaMaskingId, PostFilmIsAlphaMasking);
                cmd.SetGlobalFloat(PostFilmIsWithoutDepthId, PostFilmIsWithoutDepth);
                // the diffusion composite is its own shader in the game; pick per state.
                Material compositeMaterial = DiffusionEnabled && _diffusionBloomMaterial != null
                    ? _diffusionBloomMaterial
                    : _postBloomMaterial;
                compositeMaterial.SetTexture(MainTexId, source);
                Blitter.BlitCameraTexture(cmd, source, _composite, compositeMaterial, 0);

                if (!_envLogged)
                {
                    _envLogged = true;
                    LogCompositeEnvironment();
                }
                ProbeLuminance(cmd, source, _bloomA, _bloomB, _bloomC, _composite);

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

            // the composite reads per-material values the game never sets either, so
            // log what the shader defaults them to and pin the blend factor to neutral.
            private void DumpCompositeMaterialDefaults()
            {
                var m = _postBloomMaterial;
                var sb = new System.Text.StringBuilder("[gamebloom] material defaults");
                foreach (var prop in new[] { "_colorBlendFactor", "_movieScale", "_movieOffset", "_MainTex_ST" })
                {
                    if (m.HasProperty(prop))
                    {
                        var v = m.GetVector(prop);
                        sb.Append($" {prop}=({v.x:F2},{v.y:F2},{v.z:F2},{v.w:F2})");
                    }
                    else
                    {
                        sb.Append($" {prop}=absent");
                    }
                }
                Gallop.Live.Director.FileLog(sb.ToString());
                if (m.HasProperty("_colorBlendFactor") && m.GetVector("_colorBlendFactor").x == 0f)
                {
                    // a zero blend factor multiplies the whole composite to black.
                    m.SetVector("_colorBlendFactor", new Vector4(1f, 1f, 1f, 1f));
                    Gallop.Live.Director.FileLog("[gamebloom] _colorBlendFactor defaulted to zero; pinned to one");
                }
            }

            // one-shot dump of every global the composite consumes so a bad value is
            // visible in the log instead of guessed at.
            private void LogCompositeEnvironment()
            {
                Gallop.Live.Director.FileLog(
                    $"[gamebloom] env diffusion={DiffusionEnabled} blend={BloomIsScreenBlend} dofWeight={BloomDofWeight:F2} " +
                    $"power={PostFilmPower:F3} offset=({PostFilmOffsetParam.x:F2},{PostFilmOffsetParam.y:F2},{PostFilmOffsetParam.z:F2},{PostFilmOffsetParam.w:F2}) " +
                    $"option=({PostFilmOptionParam.x:F2},{PostFilmOptionParam.y:F2},{PostFilmOptionParam.z:F2},{PostFilmOptionParam.w:F2}) " +
                    $"c0=({PostFilmColor0.r:F2},{PostFilmColor0.g:F2},{PostFilmColor0.b:F2},{PostFilmColor0.a:F2}) " +
                    $"c1=({PostFilmColor1.r:F2},{PostFilmColor1.g:F2},{PostFilmColor1.b:F2},{PostFilmColor1.a:F2}) " +
                    $"c2=({PostFilmColor2.r:F2},{PostFilmColor2.g:F2},{PostFilmColor2.b:F2},{PostFilmColor2.a:F2}) " +
                    $"c3=({PostFilmColor3.r:F2},{PostFilmColor3.g:F2},{PostFilmColor3.b:F2},{PostFilmColor3.a:F2}) " +
                    $"invVignette={PostFilmIsInverseVignette:F2}");
            }

            // async gpu readback of the four key textures so the log shows which stage
            // of the chain is black instead of inferring it from the screen.
            private void ProbeLuminance(CommandBuffer cmd, RTHandle source, RTHandle a, RTHandle b, RTHandle c, RTHandle composite)
            {
                float now = UnityEngine.Time.unscaledTime;
                if (_nextProbeTime > 0f && now < _nextProbeTime)
                    return;
                _nextProbeTime = now + 3f;
                Gallop.Live.Director.FileLog(
                    $"[gamebloom] probe t={now:F1} src={ProbeTexture(cmd, source)} A={ProbeTexture(cmd, a)} B={ProbeTexture(cmd, b)} C={ProbeTexture(cmd, c)} out={ProbeTexture(cmd, composite)}");
            }

            private string ProbeTexture(CommandBuffer cmd, RTHandle handle)
            {
                if (handle == null || handle.rt == null)
                    return "null";
                var tmp = new UnityEngine.Texture2D(4, 4, UnityEngine.TextureFormat.RGBA32, false);
                var prev = UnityEngine.RenderTexture.active;
                var src = handle.rt;
                UnityEngine.RenderTexture.active = src;
                tmp.ReadPixels(new UnityEngine.Rect(0, 0, 4, 4), 0, 0, false);
                tmp.Apply(false);
                UnityEngine.RenderTexture.active = prev;
                var px = tmp.GetPixels32();
                float sum = 0f;
                float max = 0f;
                foreach (var p in px)
                {
                    float l = (p.r + p.g + p.b) / 765f;
                    sum += l;
                    if (l > max) max = l;
                }
                float avg = sum / px.Length;
                UnityEngine.Object.Destroy(tmp);
                return $"(avg={avg:F4} max={max:F3})";
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                // rt handles are pooled; nothing per-frame to free.
            }
        }
    }
}
