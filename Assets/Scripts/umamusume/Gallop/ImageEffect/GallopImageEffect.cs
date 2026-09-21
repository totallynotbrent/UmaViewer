using Gallop.ImageEffect;
using Gallop.Live;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop
{
    [DisallowMultipleComponent]
    public class GallopImageEffect : MonoBehaviour
    {
        [SerializeField]
        private Volume _volume;

        [SerializeField]
        private VolumeProfile _runtimeProfile;

        [SerializeField]
        private DofDiffusionBloomOverlayParam
            _dofDiffusionBloomOverlayParam =
                new DofDiffusionBloomOverlayParam();

        private Bloom _bloom;
        private ColorAdjustments _colorAdjust;
        private DepthOfField _dof;
        private Tonemapping _tonemapping;
        private MotionBlur _motionBlur;
        private Vignette _vignette;
        private Camera _camera;

        // runtime toggle so the stage glow (bloom + diffusion) can be switched off
        // without re-editing the timeline; defaults on.
        [SerializeField]
        private bool _bloomAndDiffusionEnabled = true;

        public bool BloomAndDiffusionEnabled
        {
            get => _bloomAndDiffusionEnabled;
            set => _bloomAndDiffusionEnabled = value;
        }

        // timeline film state set by Director from the PostFilm keys; a null
        // color means no film layer is active this frame.
        private Color? _timelineFilmColor;
        private float _timelineFilmPower;
        private bool _timelineFilmIsVignette;
        private PostFilmBlend _timelineFilmBlend = PostFilmBlend.Lerp;

        // authored blend mode of the active film layer.
        public enum PostFilmBlend
        {
            Lerp = 0,
            Add = 1,
            Mul = 2
        }

        // authored dof focus from the keys: absolute distance + aperture size,
        // with a character-lock flag for shots focused on a performer.
        private float _timelineFocusDistance = -1f;
        private float _timelineFocusSize = 1.5f;
        private float _timelineFocusSpread = 1f;
        private bool _timelineFocusOnChara;
        private bool _depthOfFieldActive;

        public bool DepthOfFieldActive
        {
            get => _depthOfFieldActive;
            set => _depthOfFieldActive = value;
        }

        // authored stage saturation from the exposure/colorCorrection tracks;
        // float.min means no track drove it this frame.
        private float _timelineStageSaturation = float.MinValue;

        private float _timelineExposure = float.MinValue;
        // sun-shaft tint from the volumeLight track; tints the bloom color so
        // the glow reads in the authored shaft hue.
        private Color? _volumeLightTint;

        public void SetVolumeLightTint(Color color)
        {
            _volumeLightTint = color;
        }

        // lens fringe from the chromatic aberration track; -1 = no track input.
        private float _timelineChromaticAberration = -1f;
        private UnityEngine.Rendering.Universal.ChromaticAberration _chromatic;

        public void SetChromaticAberration(float power)
        {
            _timelineChromaticAberration = power;
        }

        // per-frame shaft lift set by Director; folded into bloom intensity once.
        private float _volumeLightBloomLift;

        public void SetVolumeLightBloomLift(float lift)
        {
            _volumeLightBloomLift = lift;
        }

        public void SetTimelineStageSaturation(float saturation)
        {
            _timelineStageSaturation = saturation;
        }

        // authored exposure gain (stops) from the exposure track; applied in ApplyGrade.
        public void SetTimelineExposure(float gain)
        {
            _timelineExposure = gain;
        }

        public void SetTimelineFocus(float distance, float size, bool onChara)
        {
            _timelineFocusDistance = distance;
            _timelineFocusSize = size;
            _timelineFocusOnChara = onChara;
        }

        // authored dof pass-through: blurSpread straight from the keys.
        public void SetTimelineFocusSpread(float blurSpread)
        {
            _timelineFocusSpread = blurSpread;
        }

        public void ApplyTimelineFilm(Color color, float power, bool isVignette)
        {
            ApplyTimelineFilm(color, power, isVignette, PostFilmBlend.Lerp);
        }

        // blend semantics from the authored filmMode: Add layers brighten (screen),
        // Mul layers darken, Lerp replaces; Vignette* layers also push the edges.
        public void ApplyTimelineFilm(Color color, float power, bool isVignette, PostFilmBlend blend)
        {
            _timelineFilmColor = color;
            _timelineFilmPower = power;
            _timelineFilmIsVignette = isVignette;
            _timelineFilmBlend = blend;
        }

        public void ClearTimelineFilm()
        {
            _timelineFilmColor = null;
        }

        // runtime toggle so depth of field can be switched off without re-editing
        // the timeline; defaults on.
        [SerializeField]
        private bool _depthOfFieldEnabled = true;

        // f-stop for the bokeh aperture; lower = shallower focus.
        [SerializeField]
        private float _dofAperture = 4f;

        // bloom threshold floor under hdr: only emissive values above this bloom,
        // so the stage glows soft instead of clipping flat white.
        [SerializeField]
        private float _bloomThresholdFloor = 1.2f;

        // cap on the veiling radius; 1.0 is the halo band, this stays in the soft-glow band.
        [SerializeField]
        private float _bloomScatterMax = 0.45f;

        // bounds how much any single hot source feeds the bloom pyramid before the
        // tonemap; the authoritative anti-blowout knob under hdr.
        [SerializeField]
        private float _bloomClamp = 1.5f;

        // camera-only motion blur strength; 0 disables, 0.4 is a soft sweep.
        [SerializeField]
        private float _motionBlurIntensity = 0.4f;

        // timeline radial-blur keys drive the camera motion blur as the closest
        // existing blur pass; clamped by the timeline handler.
        public float MotionBlurIntensity
        {
            get => _motionBlurIntensity;
            set => _motionBlurIntensity = value;
        }

        // timeline tilt-shift / vortex keys lift the bloom scatter band for the frame;
        // 0 keeps the serialized default so the glow stays in the soft-glow band.
        [SerializeField]
        private float _bloomScatterBoost = 0f;

        public float BloomScatterBoost
        {
            get => _bloomScatterBoost;
            set => _bloomScatterBoost = value;
        }

        public bool DepthOfFieldEnabled
        {
            get => _depthOfFieldEnabled;
            set => _depthOfFieldEnabled = value;
        }

        public DofDiffusionBloomOverlayParam
            DofDiffusionBloomOverlayParam
        {
            get
            {
                return _dofDiffusionBloomOverlayParam;
            }
        }

        private void Awake()
        {
            InitializeVolume();
        }

        private void LateUpdate()
        {
            SectionProfiler.Begin("imageeffect.apply");
            ApplyBloomParameter();
            ApplyTonemapping();
            ApplyDepthOfField();
            ApplyMotionBlur();
            ApplyVignette();
            ApplyTimelineFilmLayers();
            ApplyChromaticAberration();
            SectionProfiler.End();
        }

        // lens fringe: authored strength drives the URP chromatic aberration.
        private void ApplyChromaticAberration()
        {
            if (_chromatic == null)
                InitializeVolume();
            if (_chromatic == null)
                return;

            if (_timelineChromaticAberration >= 0f)
            {
                _chromatic.active = true;
                _chromatic.intensity.overrideState = true;
                _chromatic.intensity.value = _timelineChromaticAberration;
            }
            else
            {
                _chromatic.active = false;
            }
        }

        // maps the game's PostFilm layers onto the volume: vignette-mode films
        // only color the screen edges through the vignette; non-vignette films
        // tint the full screen. the hardcoded base look stays untouched so the
        // film never stacks with it.
        private void ApplyTimelineFilmLayers()
        {
            if (_colorAdjust == null)
                InitializeVolume();
            if (_colorAdjust == null)
                return;

            if (_timelineFilmColor.HasValue)
            {
                Color film = _timelineFilmColor.Value;
                float power = Mathf.Clamp01(_timelineFilmPower);

                // colorFilter is a multiplier in urp, so every blend maps to a
                // value >= the authored color and <= a safe brightening cap:
                // add-mode lifts the frame toward white-plus-color (screen),
                // mul-mode scales toward the film color (dark gel), lerp mixes
                // from white as before.
                Color filter;
                switch (_timelineFilmBlend)
                {
                    case PostFilmBlend.Add:
                        // screen-style: 1 + c*p brightens; clamp keeps the
                        // frame from blooming past recoverable range.
                        filter = new Color(
                            Mathf.Clamp01(1f - (1f - film.r) * (1f - power * 0.35f)),
                            Mathf.Clamp01(1f - (1f - film.g) * (1f - power * 0.35f)),
                            Mathf.Clamp01(1f - (1f - film.b) * (1f - power * 0.35f)),
                            1f);
                        break;
                    case PostFilmBlend.Mul:
                        // multiply darkens toward the film color, floored at
                        // 40% brightness so the stage never crushes to black.
                        filter = Color.Lerp(
                            new Color(
                                Mathf.Max(0.4f, film.r),
                                Mathf.Max(0.4f, film.g),
                                Mathf.Max(0.4f, film.b),
                                1f),
                            Color.white,
                            1f - power * 0.7f);
                        break;
                    default:
                        filter = Color.Lerp(Color.white, film, power * 0.85f);
                        break;
                }

                _colorAdjust.colorFilter.overrideState = true;
                _colorAdjust.colorFilter.value = filter;

                if (_timelineFilmIsVignette && _vignette != null)
                {
                    // vignette modes also deepen the frame edges in the film
                    // color at a fraction of the authored power.
                    _vignette.color.overrideState = true;
                    _vignette.color.value = film;
                    _vignette.intensity.overrideState = true;
                    _vignette.intensity.value = power * 0.6f;
                    _vignette.smoothness.overrideState = true;
                    _vignette.smoothness.value = 0.45f;
                    _vignette.rounded.overrideState = true;
                    _vignette.rounded.value = true;
                }
                else
                {
                    _vignette.color.overrideState = true;
                    _vignette.color.value = Color.black;
                }
            }
            else
            {
                _colorAdjust.colorFilter.overrideState = true;
                _colorAdjust.colorFilter.value = Color.white;
            }
        }

        public void InitializeVolume()
        {
            if (_volume == null)
                _volume = GetComponent<Volume>();

            if (_volume == null)
                _volume = gameObject.AddComponent<Volume>();

            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.weight = 1f;

            if (_volume.sharedProfile != null)
                _runtimeProfile =
                    Instantiate(_volume.sharedProfile);
            else
                _runtimeProfile =
                    ScriptableObject.CreateInstance<VolumeProfile>();

            _runtimeProfile.name =
                $"{name}_RuntimePostEffectProfile";

            _volume.profile = _runtimeProfile;

            if (!_runtimeProfile.TryGet(out _bloom))
                _bloom = _runtimeProfile.Add<Bloom>(true);

            if (!_runtimeProfile.TryGet(out _colorAdjust))
                _colorAdjust = _runtimeProfile.Add<ColorAdjustments>(true);

            if (!_runtimeProfile.TryGet(out _dof))
                _dof = _runtimeProfile.Add<DepthOfField>(true);

            if (!_runtimeProfile.TryGet(out _tonemapping))
                _tonemapping = _runtimeProfile.Add<Tonemapping>(true);

            if (!_runtimeProfile.TryGet(out _motionBlur))
                _motionBlur = _runtimeProfile.Add<MotionBlur>(true);

            if (!_runtimeProfile.TryGet(out _vignette))
                _vignette = _runtimeProfile.Add<Vignette>(true);

            if (!_runtimeProfile.TryGet(out _chromatic))
                _chromatic = _runtimeProfile.Add<UnityEngine.Rendering.Universal.ChromaticAberration>(true);
        }

        public void ApplyBloomParameter()
        {
            if (_bloom == null)
                InitializeVolume();

            if (_bloom == null)
                return;

            var param = _dofDiffusionBloomOverlayParam;
            if (param == null)
                return;

            // runtime toggle: drops the stage glow so the f7 a/b tests bloom only; aces
            // stays on because it is the hdr-to-display transform, not a glow effect.
            if (!_bloomAndDiffusionEnabled)
            {
                _bloom.active = false;
                return;
            }

            if (_volume != null)
                _volume.weight = 1f;

            // the reference forks drive bloom from the authored intensity raw; this fork
            // used to fold diffusion brightness in and scale by a boost, which under hdr
            // over-drove the pyramid to flat white. keep bloom intensity raw and let
            // diffusion contribute only a tightly-bounded soft kick.
            float bloomIntensity = param.IsEnableBloom ? Mathf.Max(0f, param.BloomIntensity) : 0f;
            float diffusionHint = param.IsEnableDiffusion ? Mathf.Max(0f, param.DiffusionBright) : 0f;
            float totalIntensity = bloomIntensity + Mathf.Min(diffusionHint * 0.02f, 0.4f) + _volumeLightBloomLift;

            bool enabled = (param.IsEnableBloom || param.IsEnableDiffusion) && totalIntensity > 0f;
            _bloom.active = enabled;

            _bloom.threshold.overrideState = true;
            _bloom.intensity.overrideState = true;
            _bloom.scatter.overrideState = true;
            _bloom.clamp.overrideState = true;
            _bloom.highQualityFiltering.overrideState = true;

            _bloom.intensity.value = totalIntensity;

            float bloomBlur = param.IsEnableBloom ? Mathf.Max(0f, param.BloomBlurSize) : 0f;
            float diffusionBlur = param.IsEnableDiffusion ? Mathf.Max(0f, param.DiffusionBlurSize) : 0f;
            float maxBlurSize = Mathf.Max(bloomBlur, diffusionBlur);
            float scatter = Mathf.Min(_bloomScatterMax, Mathf.Clamp01(maxBlurSize / 10f));
            // timeline tilt-shift/vortex keys lift the scatter band for the frame; the
            // bloom diffuses wider without touching the pyramid intensity.
            _bloom.scatter.value = Mathf.Min(1f, Mathf.Max(scatter, _bloomScatterBoost));

            float threshold;
            if (param.IsEnableBloom && param.IsEnableDiffusion)
            {
                threshold = Mathf.Min(Mathf.Max(0f, param.BloomThreshold), Mathf.Max(0f, param.DiffusionThreshold));
            }
            else if (param.IsEnableDiffusion)
            {
                threshold = Mathf.Max(0f, param.DiffusionThreshold);
            }
            else
            {
                threshold = Mathf.Max(0f, param.BloomThreshold);
            }
            // hold the threshold at or above the hdr floor so only emissive sources bloom.
            _bloom.threshold.value = Mathf.Max(_bloomThresholdFloor, threshold);

            // bound the hot-source contribution before the tonemap so a single led does not
            // clamp the whole pyramid to white.
            _bloom.clamp.value = _bloomClamp;

            // sun-shaft tint: colorize the bloom toward the authored shaft hue.
            if (_volumeLightTint.HasValue)
            {
                _bloom.tint.overrideState = true;
                _bloom.tint.value = _volumeLightTint.Value;
            }
            else
            {
                _bloom.tint.overrideState = true;
                _bloom.tint.value = Color.white;
            }

            // bicubic upsample removes the sparkle on the bright reconstruction.
            _bloom.highQualityFiltering.value = true;

            // neutral exposure (stops) baked directly into the grade; the viewer no longer
            // exposes a tuning slider, and the renderer keeps its chosen value here.
            if (_colorAdjust != null)
            {
                float exp = _timelineExposure != float.MinValue ? _timelineExposure : 0f;
                _colorAdjust.postExposure.overrideState = true;
                _colorAdjust.postExposure.value = exp;
                // subtle lift to hit the dark concert grade without crushing the mids.
                _colorAdjust.contrast.overrideState = true;
                _colorAdjust.contrast.value = 5f;
                _colorAdjust.saturation.overrideState = true;
                // authored stage grade wins over the static lift when a track
                // drove saturation this frame.
                _colorAdjust.saturation.value =
                    _timelineStageSaturation != float.MinValue
                        ? _timelineStageSaturation
                        : -5f;
            }
        }

        private void ApplyVignette()
        {
            if (_vignette == null)
                InitializeVolume();

            if (_vignette == null)
                return;

            // a timeline film layer owns the vignette this frame.
            if (_timelineFilmColor.HasValue)
                return;

            _vignette.active = true;
            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = 0.35f;
            _vignette.smoothness.overrideState = true;
            _vignette.smoothness.value = 0.45f;
            _vignette.rounded.overrideState = true;
            _vignette.rounded.value = true;
        }

        public void ApplyTonemapping()
        {
            if (_tonemapping == null)
                InitializeVolume();

            if (_tonemapping == null)
                return;

            // the hdr buffer needs aces to roll off highlight values; rendered raw it reads
            // washed out. always on because aces is the hdr-to-display transform, separate
            // from the bloom toggle.
            _tonemapping.active = true;
            _tonemapping.mode.overrideState = true;
            _tonemapping.mode.value = TonemappingMode.ACES;
        }

        public void ApplyDepthOfField()
        {
            if (_dof == null)
                InitializeVolume();

            if (_dof == null)
                return;

            if (_camera == null)
                _camera = GetComponent<Camera>();

            if (_camera == null)
                return;

            // free-camera / orbit mode has no timeline focus target; disable dof so
            // the manual camera stays uniformly sharp.
            if (!_depthOfFieldEnabled || Director.instance == null || !Director.instance.isTimelineControlled)
            {
                _dof.active = false;
                return;
            }

            var control = Director.instance._liveTimelineControl;
            if (control == null)
            {
                _dof.active = false;
                return;
            }

            float focusDistance;
            if (_depthOfFieldActive && _timelineFocusDistance > 0f)
            {
                // authored dof keys win: the game stores an absolute focal
                // distance with an optional character lock; follow it directly.
                focusDistance = _timelineFocusDistance;
                var director = Gallop.Live.Director.instance;
                if (_timelineFocusOnChara && director != null &&
                    director.CharaContainerScript != null && director.CharaContainerScript.Count > 0)
                {
                    // character-locked shots track the lead performer's body
                    // so handheld close-ups stay on the performer, not the set.
                    var chara = director.CharaContainerScript[0];
                    if (chara != null)
                    {
                        float charaDist = Vector3.Distance(
                            _camera.transform.position,
                            chara.transform.position);
                        focusDistance = Mathf.Lerp(focusDistance, charaDist, 0.7f);
                    }
                }
            }
            else
            {
                Vector3 focusPoint = control.LatestCameraLookAtPosition;
                focusDistance = Vector3.Distance(_camera.transform.position, focusPoint);
            }

            _dof.active = true;
            _dof.mode.overrideState = true;
            _dof.mode.value = DepthOfFieldMode.Bokeh;
            _dof.focusDistance.overrideState = true;
            _dof.focusDistance.value = Mathf.Clamp(focusDistance, 0.5f, 50f);
            _dof.focalLength.overrideState = true;
            _dof.focalLength.value = Gallop.Math.GetFocalLength(_camera.fieldOfView);
            _dof.aperture.overrideState = true;
            // authored semantics: forcalSize is the sharp band around the focal
            // plane (0 = razor focus, 30 = deep stage) so aperture rises with it;
            // blurSpread widens the bokeh and divides back down to f-stops.
            float authoredAperture;
            if (_depthOfFieldActive && _timelineFocusSize >= 0f)
            {
                float blurSpread = Mathf.Max(0.05f, _timelineFocusSpread);
                authoredAperture = Mathf.Clamp(
                    44f / (1f + Mathf.Clamp(_timelineFocusSize, 0f, 30f) * 0.5f) / blurSpread,
                    1.1f, 32f);
            }
            else
            {
                authoredAperture = _dofAperture;
            }
            _dof.aperture.value = authoredAperture;
            _dof.bladeCount.overrideState = true;
            _dof.bladeCount.value = 6;
        }

        public void ApplyMotionBlur()
        {
            if (_motionBlur == null)
                InitializeVolume();

            if (_motionBlur == null)
                return;

            // camera-only blurs the timeline's camera sweeps and dollies; drop it in
            // free-cam so orbit and screenshot mode stay sharp.
            if (Director.instance == null || !Director.instance.isTimelineControlled)
            {
                _motionBlur.active = false;
                return;
            }

            _motionBlur.active = true;
            _motionBlur.mode.overrideState = true;
            _motionBlur.mode.value = MotionBlurMode.CameraOnly;
            _motionBlur.intensity.overrideState = true;
            _motionBlur.intensity.value = Mathf.Clamp(_motionBlurIntensity, 0f, 1f);
            _motionBlur.clamp.overrideState = true;
            _motionBlur.clamp.value = 0.05f;
            _motionBlur.quality.overrideState = true;
            _motionBlur.quality.value = MotionBlurQuality.Medium;
        }
    }
}