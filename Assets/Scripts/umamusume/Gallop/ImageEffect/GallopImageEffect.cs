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
            ApplyBloomParameter();
            ApplyTonemapping();
            ApplyDepthOfField();
            ApplyMotionBlur();
            ApplyVignette();
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
            float totalIntensity = bloomIntensity + Mathf.Min(diffusionHint * 0.02f, 0.4f);

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

            // bicubic upsample removes the sparkle on the bright reconstruction.
            _bloom.highQualityFiltering.value = true;

            // neutral exposure (stops) baked directly into the grade; the viewer no longer
            // exposes a tuning slider, and the renderer keeps its chosen value here.
            if (_colorAdjust != null)
            {
                float exp = 0f;
                _colorAdjust.postExposure.overrideState = true;
                _colorAdjust.postExposure.value = exp;
                // subtle lift to hit the dark concert grade without crushing the mids.
                _colorAdjust.contrast.overrideState = true;
                _colorAdjust.contrast.value = 5f;
                _colorAdjust.saturation.overrideState = true;
                _colorAdjust.saturation.value = -5f;
            }
        }

        private void ApplyVignette()
        {
            if (_vignette == null)
                InitializeVolume();

            if (_vignette == null)
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

            Vector3 focusPoint = control.LatestCameraLookAtPosition;
            float focusDistance = Vector3.Distance(_camera.transform.position, focusPoint);

            _dof.active = true;
            _dof.mode.overrideState = true;
            _dof.mode.value = DepthOfFieldMode.Bokeh;
            _dof.focusDistance.overrideState = true;
            _dof.focusDistance.value = Mathf.Clamp(focusDistance, 0.5f, 50f);
            _dof.focalLength.overrideState = true;
            _dof.focalLength.value = Gallop.Math.GetFocalLength(_camera.fieldOfView);
            _dof.aperture.overrideState = true;
            _dof.aperture.value = Mathf.Clamp(_dofAperture, 1f, 32f);
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