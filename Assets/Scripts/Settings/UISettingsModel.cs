#if UNITY_STANDALONE || UNITY_EDITOR
using SFB;
#endif

using UnityEngine;
using UnityEngine.UI;

public class UISettingsModel : MonoBehaviour
{
    static UmaViewerBuilder Builder => UmaViewerBuilder.Instance;

    [SerializeField] private Toggle _lockCharacter;
    [SerializeField] private Toggle _openWithTPose;
    [SerializeField] private Toggle _enablePhysics;
    [SerializeField] private Toggle _lookAtCamera;
    [SerializeField] private Toggle _faceOverride;
    [SerializeField] private Slider _outlineWidthSlider;
    
    [Header("Physics Settings")]
    [SerializeField] private Slider _hairStiffnessSlider;
    [SerializeField] private Slider _skirtStiffnessSlider;
    [SerializeField] private Slider _tailStiffnessSlider;
    [SerializeField] private Slider _windIntensitySlider;
    [SerializeField] private Slider _collisionScaleSlider;

    public ScrollRect MaterialsList;

    private bool
        _isHeadFix,
        _isTPose,
        _dynamicBoneEnable = true,
        _enableEyeTracking = true,
        _enableFaceOverride = true;

    private float _outlineWidth;
    private float _hairStiffness = 0.75f;
    private float _skirtStiffness = 1.4f;
    private float _tailStiffness = 0.85f;
    private float _windIntensity = 0.5f;
    private float _collisionScale = 1.0f;

    public bool IsHeadFix
    {
        get { return _isHeadFix; }
        set { _lockCharacter.SetIsOnWithoutNotify(value); SetHeadFix(value); }
    }

    public bool IsTPose
    {
        get { return _isTPose; }
        set { _openWithTPose.SetIsOnWithoutNotify(value); SetTPose(value); }
    }

    public bool DynamicBoneEnable
    {
        get { return _dynamicBoneEnable; }
        set { _enablePhysics.SetIsOnWithoutNotify(value); SetDynamicBoneEnable(value); }
    }

    public bool EnableEyeTracking
    {
        get { return _enableEyeTracking; }
        set { _lookAtCamera.SetIsOnWithoutNotify(value); SetEyeTrackingEnable(value); }
    }

    public bool EnableFaceOverride
    {
        get { return _enableFaceOverride; }
        set { _faceOverride.SetIsOnWithoutNotify(value); SetFaceOverrideEnable(value); }
    }

    public float OutlineWidth
    {
        get { return _outlineWidth; }
        set { _outlineWidthSlider.value = value; }
    }

    public void SetHeadFix(bool value)
    {
        _isHeadFix = value;
    }

    public void SetTPose(bool value)
    {
        _isTPose = value;
    }

    public void SetDynamicBoneEnable(bool isOn)
    {
        _dynamicBoneEnable = isOn;
        Builder.CurrentUMAContainer?.SetDynamicBoneEnable(isOn);
    }

    public void SetEyeTrackingEnable(bool isOn)
    {
        _enableEyeTracking = isOn;
        Builder.CurrentUMAContainer?.SetEyeTracking(isOn);
    }

    public void SetFaceOverrideEnable(bool isOn)
    {
        _enableFaceOverride = isOn;
        Builder.CurrentUMAContainer?.SetFaceOverrideData(isOn);
    }

    public void ChangeOutlineWidth(float val)
    {
        _outlineWidth = val;
        Shader.SetGlobalFloat("_GlobalOutlineWidth", val);
    }
    
    // Physics settings methods
    public void SetHairStiffness(float value)
    {
        _hairStiffness = value;
        ApplyPhysicsToCurrentCharacter();
    }
    
    public void SetSkirtStiffness(float value)
    {
        _skirtStiffness = value;
        ApplyPhysicsToCurrentCharacter();
    }
    
    public void SetTailStiffness(float value)
    {
        _tailStiffness = value;
        ApplyPhysicsToCurrentCharacter();
    }
    
    public void SetWindIntensity(float value)
    {
        _windIntensity = value;
        ApplyPhysicsToCurrentCharacter();
    }
    
    public void SetCollisionScale(float value)
    {
        _collisionScale = value;
        ApplyPhysicsToCurrentCharacter();
    }
    
    private void ApplyPhysicsToCurrentCharacter()
    {
        var container = Builder?.CurrentUMAContainer;
        if (container == null) return;
        
        // Access the CySpring controller via reflection or public method
        // For now, apply via LivePhysicsConfig if it exists
        var liveConfig = Gallop.Live.LivePhysicsConfig.Instance;
        if (liveConfig != null)
        {
            liveConfig.SetHairStiffness(_hairStiffness);
            liveConfig.SetSkirtStiffness(_skirtStiffness);
            liveConfig.SetTailStiffness(_tailStiffness);
            liveConfig.SetWindIntensity(_windIntensity);
            liveConfig.SetCollisionScale(_collisionScale);
        }
    }
    
    public void ApplyPhysicsPreset(string preset)
    {
        switch (preset)
        {
            case "energetic":
                _hairStiffness = 1.2f;
                _skirtStiffness = 1.4f;
                _tailStiffness = 1.3f;
                _windIntensity = 0.7f;
                _collisionScale = 1.1f;
                break;
            case "gentle":
                _hairStiffness = 0.6f;
                _skirtStiffness = 0.7f;
                _tailStiffness = 0.65f;
                _windIntensity = 0.3f;
                _collisionScale = 0.9f;
                break;
            default:
                _hairStiffness = 0.75f;
                _skirtStiffness = 0.9f;
                _tailStiffness = 0.85f;
                _windIntensity = 0.5f;
                _collisionScale = 1.0f;
                break;
        }
        ApplyPhysicsToCurrentCharacter();
    }

    public void ExportModel()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        var container = Builder.CurrentUMAContainer;
        if (container)
        {
            var entry = container.CharaEntry;
            var path = StandaloneFileBrowser.SaveFilePanel("Save PMX File", Config.Instance.MainPath, $"{entry.Id}_{entry.GetName()}", "pmx");
            if (!string.IsNullOrEmpty(path))
            {
                ModelExporter.ExportModel(container, path);
            }
        }

        var prop_container = Builder.CurrentOtherContainer;
        if (prop_container)
        {
            var path = StandaloneFileBrowser.SaveFilePanel("Save PMX File", Config.Instance.MainPath, $"{prop_container}", "pmx");
            if (!string.IsNullOrEmpty(path))
            {
                ModelExporter.ExportModel(prop_container, path);
            }
        }

#else
        UmaViewerUI.Instance.ShowMessage("Not supported on this platform", UIMessageType.Warning);
#endif
    }
}
