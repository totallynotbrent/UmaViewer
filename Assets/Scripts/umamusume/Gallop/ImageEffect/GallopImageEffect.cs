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
        private Tonemapping _tonemapping;

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

            if (!_runtimeProfile.TryGet(out _tonemapping))
                _tonemapping = _runtimeProfile.Add<Tonemapping>(true);

            _tonemapping.mode.overrideState = true;
            _tonemapping.mode.value = TonemappingMode.ACES;
        }

        public void ApplyBloomParameter()
        {
            if (_bloom == null || _colorAdjust == null)
                InitializeVolume();

            if (_bloom == null)
                return;

            var param = _dofDiffusionBloomOverlayParam;

            bool enabled =
                param.IsEnableBloom &&
                param.BloomIntensity > 0f;

            _bloom.active = enabled;

            _bloom.threshold.overrideState = true;
            _bloom.intensity.overrideState = true;
            _bloom.scatter.overrideState = true;

            _bloom.threshold.value = Mathf.Max(0.6f, param.BloomThreshold);

            _bloom.intensity.value = Mathf.Min(2.0f, Mathf.Max(0f, param.BloomIntensity));

            _bloom.scatter.value =
                Mathf.Clamp01(param.BloomBlurSize / 10f);

            if (_colorAdjust != null)
            {
                _colorAdjust.saturation.overrideState = true;
                _colorAdjust.contrast.overrideState = true;
                _colorAdjust.postExposure.overrideState = true;

                _colorAdjust.saturation.value =
                    Mathf.Clamp(param.DiffusionSaturation, -100f, 100f);
                _colorAdjust.contrast.value =
                    Mathf.Clamp(param.DiffusionContrast, -100f, 100f);
                _colorAdjust.postExposure.value =
                    Mathf.Clamp((param.DiffusionBright - 1f) * 0.35f, -0.35f, 0.35f);
            }

            if (param.IsEnableDiffusion)
            {
                _bloom.active = true;
                _bloom.threshold.value =
                    Mathf.Max(0.8f, param.DiffusionThreshold * 0.9f);
                _bloom.scatter.value =
                    Mathf.Clamp01(Mathf.Max(_bloom.scatter.value, Mathf.Clamp01(param.DiffusionBlurSize / 14f)));
            }
        }
    }
}