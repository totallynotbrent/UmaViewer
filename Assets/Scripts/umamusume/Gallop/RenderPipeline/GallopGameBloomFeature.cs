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
            public static bool InverseVignette;

            // the game draws up to three film layers after the bloom composite,
            // each gated by the same validity rules as ScreenOverlay.Overlay.IsValid.
            public sealed class FilmLayerState
            {
                public int mode;
                public float power;
                public float depthPower;
                public float depthClip;
                public Vector4 offsetParam;
                public Vector4 optionParam;
                public Color color0;
                public Color color1;
                public Color color2;
                public Color color3;
                public int layerMode;
                public int colorBlend;
                public float colorBlendFactor;
                public Vector4 rollParameter;
                public Vector4 scaleParameter;
                public bool inverseVignette;
                public bool isAlphaMasking;
                public bool isUVMovieNoScale;

                public bool IsValid()
                {
                    // mirrors ScreenOverlay.Overlay.IsValid: mul and vignette modes
                    // always draw, monochrome needs an opaque color, the rest need power.
                    if (mode == 0)
                        return false;
                    if (mode == 3 || mode == 4 || mode == 6)
                        return true;
                    if (mode == 7)
                        return color0.a > 0f;
                    return power > 0f;
                }
            }

            // keywords that select the subprogram variant per drawn layer; order
            // matches the game's SHADER_KEYWORD_MODE / SHADER_KEYWORD_BLEND tables.
            private static readonly string[] ShaderKeywordMode =
            {
                "MODE_NONE", "MODE_LERP", "MODE_ADD", "MODE_MUL",
                "MODE_VIGNETTE_LERP", "MODE_VIGNETTE_ADD", "MODE_VIGNETTE_MUL",
                "MODE_MONOCHROME", "MODE_SCREENBLEND", "MODE_VIGNETT_SCREENBLEND"
            };
            private static readonly string[] ShaderKeywordBlend =
            {
                "MASK_VIGNETTE", "BLEND_NONE", "BLEND_LERP", "BLEND_ADD", "BLEND_MUL"
            };
            public static readonly FilmLayerState[] FilmLayers = new FilmLayerState[3];
            public static float DepthPower = 1f;
            public static float DepthClip = 2f;
            public static Vector4 PostFilmRollParameter = new Vector4(0f, 1f, 0f, 1f);
            public static Vector4 PostFilmScaleParameter = new Vector4(1f, 1f, 0f, 0f);
            public static float PostFilmIsAlphaMasking;
            public static float PostFilmIsWithoutDepth = 1f;

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
            private static readonly int RgbTexId = Shader.PropertyToID("_RgbTex");
            private static readonly int ColorParamId = Shader.PropertyToID("_ColorParam");
            private static readonly int PostFilmIsUVMovieNoScaleId = Shader.PropertyToID("_PostFilmIsUVMovieNoScale");
            public static Vector4 ColorParam = new Vector4(1f, 1f, 1f, 1f);
            public static float PostFilmIsUVMovieNoScale;
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
                        if (shader != null && postShader != null && (!shader.isSupported || !postShader.isSupported))
                            Gallop.Live.Director.FileLog("[gamebloom] UNSUPPORTED-ON-THIS-GFX-API: the game shaders did not compile on this graphics api; this session benches the urp fallback, not the game bloom path");
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
                // the game pushes the active film layer's params before the composite
                // blit; layer 1 is the strongest track in every authored song.
                var compositeLayer = FilmLayers[0];
                if (compositeLayer != null)
                {
                    cmd.SetGlobalFloat(PostFilmPowerId, compositeLayer.power);
                    cmd.SetGlobalVector(PostFilmOffsetParamId, new Vector4(compositeLayer.offsetParam.x, compositeLayer.offsetParam.y, 0f, 0f));
                    cmd.SetGlobalVector(PostFilmOptionParamId, compositeLayer.optionParam);
                    cmd.SetGlobalColor(PostFilmColor0Id, compositeLayer.color0);
                    cmd.SetGlobalColor(PostFilmColor1Id, compositeLayer.color1);
                    cmd.SetGlobalColor(PostFilmColor2Id, compositeLayer.color2);
                    cmd.SetGlobalColor(PostFilmColor3Id, compositeLayer.color3);
                    cmd.SetGlobalFloat(PostFilmIsInverseVignetteId, compositeLayer.inverseVignette ? 1f : 0f);
                    cmd.SetGlobalFloat(DepthPowerId, compositeLayer.depthPower);
                    cmd.SetGlobalFloat(DepthClipId, compositeLayer.depthClip > 1f ? 0f : 1f - compositeLayer.depthClip);
                    Vector4 roll = compositeLayer.rollParameter;
                    if (srcH != 0)
                        roll.z = srcW / (float)srcH;
                    cmd.SetGlobalVector(PostFilmRollParameterId, roll);
                    cmd.SetGlobalVector(PostFilmScaleParameterId, compositeLayer.scaleParameter);
                    cmd.SetGlobalFloat(PostFilmIsAlphaMaskingId, compositeLayer.isAlphaMasking ? 1f : 0f);
                    cmd.SetGlobalFloat(PostFilmIsWithoutDepthId, 0f);
                    cmd.SetGlobalFloat(PostFilmIsUVMovieNoScaleId, compositeLayer.isUVMovieNoScale ? 1f : 0f);
                }
                // the game binds the plain rgb input and a color-correction vector
                // right before the composite; an unbound _RgbTex samples black.
                cmd.SetGlobalTexture(RgbTexId, source);
                cmd.SetGlobalVector(ColorParamId, ColorParam);
                // the diffusion composite is its own shader in the game; pick per state.
                Material compositeMaterial = DiffusionEnabled && _diffusionBloomMaterial != null
                    ? _diffusionBloomMaterial
                    : _postBloomMaterial;
                compositeMaterial.SetTexture(MainTexId, source);
                // the bloom composite is always pass 0; the game's screen-overlay
                // chain only shifts the FILM passes, never the composite itself.
                Blitter.BlitCameraTexture(cmd, source, _composite, compositeMaterial, 0);
                // the game draws up to three film layers after the composite, each
                // gated by its own validity and drawn through the film passes with
                // the mode/blend keywords selecting the subprogram variant.
                var filmTarget = _composite;
                for (int i = 0; i < FilmLayers.Length; i++)
                {
                    var layer = FilmLayers[i];
                    if (layer == null || !layer.IsValid())
                        continue;
                    SetFilmKeywords(compositeMaterial, layer);
                    SetFilmGlobals(cmd, layer, filmTarget);
                    // layer 1 draws the first film pass, layers 2/3 share the second;
                    // inverse-vignette keys shift to the variant pass (+1).
                    int filmPass = (i == 0 ? 1 : 3) + (layer.inverseVignette ? 1 : 0);
                    Blitter.BlitCameraTexture(cmd, filmTarget, filmTarget, compositeMaterial, filmPass);
                }

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

            // the game enables one mode keyword and one blend keyword per drawn film
            // layer; the keyword pair selects the subprogram variant.
            private static void SetFilmKeywords(Material material, FilmLayerState layer)
            {
                for (int i = 0; i < ShaderKeywordMode.Length; i++)
                {
                    if (i == layer.mode)
                        material.EnableKeyword(ShaderKeywordMode[i]);
                    else
                        material.DisableKeyword(ShaderKeywordMode[i]);
                }
                int blendId = layer.layerMode == 0
                    ? 0
                    : Mathf.Clamp(layer.layerMode + layer.colorBlend, 0, ShaderKeywordBlend.Length - 1);
                for (int i = 0; i < ShaderKeywordBlend.Length; i++)
                {
                    if (i == blendId)
                        material.EnableKeyword(ShaderKeywordBlend[i]);
                    else
                        material.DisableKeyword(ShaderKeywordBlend[i]);
                }
            }

            // per-layer film globals, packed the way Overlay.Update packs them.
            private static void SetFilmGlobals(CommandBuffer cmd, FilmLayerState layer, RTHandle mainTexture)
            {
                cmd.SetGlobalFloat(PostFilmPowerId, layer.power);
                cmd.SetGlobalFloat(DepthPowerId, layer.depthPower);
                cmd.SetGlobalFloat(DepthClipId, layer.depthClip > 1f ? 0f : 1f - layer.depthClip);
                cmd.SetGlobalVector(PostFilmOffsetParamId, new Vector4(layer.offsetParam.x, layer.offsetParam.y, 0f, 0f));
                cmd.SetGlobalVector(PostFilmOptionParamId, layer.optionParam);
                cmd.SetGlobalColor(PostFilmColor0Id, layer.color0);
                cmd.SetGlobalColor(PostFilmColor1Id, layer.color1);
                cmd.SetGlobalColor(PostFilmColor2Id, layer.color2);
                cmd.SetGlobalColor(PostFilmColor3Id, layer.color3);
                Vector4 roll = layer.rollParameter;
                if (mainTexture != null && mainTexture.rt != null && mainTexture.rt.height != 0)
                    roll.z = mainTexture.rt.width / (float)mainTexture.rt.height;
                cmd.SetGlobalVector(PostFilmRollParameterId, roll);
                cmd.SetGlobalVector(PostFilmScaleParameterId, layer.scaleParameter);
                cmd.SetGlobalFloat(PostFilmIsUVMovieNoScaleId, layer.layerMode == 2 ? 1f : 0f);
                cmd.SetGlobalFloat(PostFilmIsInverseVignetteId, layer.inverseVignette ? 1f : 0f);
                cmd.SetGlobalFloat(PostFilmIsAlphaMaskingId, layer.isAlphaMasking ? 1f : 0f);
                cmd.SetGlobalFloat(PostFilmIsWithoutDepthId, 0f);
            }

            // the composite reads per-material values the game never sets either, so
            // log what the shader defaults them to and pin the blend factor to neutral.
            private void DumpCompositeMaterialDefaults()
            {
                var m = _postBloomMaterial;
                var sb = new System.Text.StringBuilder("[gamebloom] material defaults");
                foreach (var prop in new[] { "_colorBlendFactor", "_movieScale", "_movieOffset", "_MainTex_ST", "_DimmerColor" })
                {
                    if (!m.HasProperty(prop))
                    {
                        sb.Append($" {prop}=absent");
                    }
                    else if (prop == "_DimmerColor")
                    {
                        var c = m.GetColor(prop);
                        sb.Append($" {prop}=({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2})");
                    }
                    else
                    {
                        var v = m.GetVector(prop);
                        sb.Append($" {prop}=({v.x:F2},{v.y:F2},{v.z:F2},{v.w:F2})");
                    }
                }
                Gallop.Live.Director.FileLog(sb.ToString());
                if (m.HasProperty("_colorBlendFactor") && m.GetVector("_colorBlendFactor").x == 0f)
                {
                    // a zero blend factor multiplies the whole composite to black.
                    m.SetVector("_colorBlendFactor", new Vector4(1f, 1f, 1f, 1f));
                    Gallop.Live.Director.FileLog("[gamebloom] _colorBlendFactor defaulted to zero; pinned to one");
                }
                if (m.HasProperty("_DimmerColor") && m.GetColor("_DimmerColor").r == 0f)
                {
                    // the composite multiplies its whole output by the dimmer color.
                    m.SetColor("_DimmerColor", Color.white);
                    Gallop.Live.Director.FileLog("[gamebloom] _DimmerColor defaulted to zero; pinned to white");
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
                // log which film layers pass the game's validity gate and which pass
                // they will draw, so the chain state is visible next to the pixels.
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < FilmLayers.Length; i++)
                {
                    var layer = FilmLayers[i];
                    if (layer == null) { sb.Append($" L{i}=none"); continue; }
                    if (!layer.IsValid()) { sb.Append($" L{i}=off(mode={layer.mode},p={layer.power:F2})"); continue; }
                    sb.Append($" L{i}=pass{(i == 0 ? 1 : 3) + (layer.inverseVignette ? 1 : 0)}(mode={layer.mode},p={layer.power:F2},inv={layer.inverseVignette})");
                }
                Gallop.Live.Director.FileLog(
                    $"[gamebloom] probe t={now:F1} film:{sb} src={ProbeTexture(cmd, source)} A={ProbeTexture(cmd, a)} B={ProbeTexture(cmd, b)} C={ProbeTexture(cmd, c)} out={ProbeTexture(cmd, composite)}");
            }

            private string ProbeTexture(CommandBuffer cmd, RTHandle handle)
            {
                if (handle == null || handle.rt == null)
                    return "null";
                var tmp = new UnityEngine.Texture2D(4, 4, UnityEngine.TextureFormat.RGBA32, false);
                var prev = UnityEngine.RenderTexture.active;
                var src = handle.rt;
                UnityEngine.RenderTexture.active = src;
                // read the CENTER of the texture: the bottom-left corner is always
                // dim stage floor and reads as black even when the frame is fine.
                float cx = Mathf.Max(0f, src.width * 0.5f - 2f);
                float cy = Mathf.Max(0f, src.height * 0.5f - 2f);
                tmp.ReadPixels(new UnityEngine.Rect(cx, cy, 4, 4), 0, 0, false);
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
