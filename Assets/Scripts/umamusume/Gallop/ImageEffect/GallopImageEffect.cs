using Gallop.ImageEffect;
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

        // runtime toggle so the stage glow (bloom + diffusion) can be switched off
        // without re-editing the timeline; defaults on.
        [SerializeField]
        private bool _bloomAndDiffusionEnabled = true;

        public bool BloomAndDiffusionEnabled
        {
            get => _bloomAndDiffusionEnabled;
            set => _bloomAndDiffusionEnabled = value;
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

            // runtime kill switch: drop bloom + diffusion entirely and zero the
            // volume weight so the stage reads flat.
            if (!_bloomAndDiffusionEnabled)
            {
                _bloom.active = false;
                if (_volume != null)
                    _volume.weight = 0f;
                return;
            }

            if (_volume != null)
                _volume.weight = 1f;

            // blend the diffusion glow into bloom (larger scatter), but keep the raw intensity
            // driven by bloom alone — DiffusionBright is a different, much larger scale and
            // summing it raw floods the stage.
            float bloomIntensity = param.IsEnableBloom ? Mathf.Max(0f, param.BloomIntensity) : 0f;
            float diffusionHint = param.IsEnableDiffusion ? Mathf.Max(0f, param.DiffusionBright) : 0f;
            // diffusion nudges the bloom up only slightly; its real job is the wider soft
            // scatter below, not raw brightness.
            float totalIntensity = bloomIntensity + Mathf.Min(diffusionHint * 0.05f, 1.5f);

            bool enabled = (param.IsEnableBloom || param.IsEnableDiffusion) && totalIntensity > 0f;
            _bloom.active = enabled;

            _bloom.threshold.overrideState = true;
            _bloom.intensity.overrideState = true;
            _bloom.scatter.overrideState = true;

            _bloom.intensity.value = totalIntensity;

            float bloomBlur = param.IsEnableBloom ? Mathf.Max(0f, param.BloomBlurSize) : 0f;
            float diffusionBlur = param.IsEnableDiffusion ? Mathf.Max(0f, param.DiffusionBlurSize) : 0f;
            float maxBlurSize = Mathf.Max(bloomBlur, diffusionBlur);
            _bloom.scatter.value = Mathf.Clamp01(maxBlurSize / 10f);

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
            _bloom.threshold.value = threshold;

            // exposure control from the settings dropdown, in stops (EV): 0 is neutral,
            // negative darkens the whole frame (emissive lights hold up better because they
            // are unlit), positive brightens. postExposure is already in stops so pass it
            // through directly.
            if (_colorAdjust != null)
            {
                float exp = Config.Instance != null ? Config.Instance.Exposure : 0f;
                exp = Mathf.Clamp(exp, -3f, 2f);
                _colorAdjust.postExposure.overrideState = true;
                _colorAdjust.postExposure.value = exp;
            }
        }
    }
}