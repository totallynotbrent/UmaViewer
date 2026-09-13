using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UISettingsCamera : MonoBehaviour
{
    public TMPro.TMP_Dropdown AAModeDropdown;
    public TMPro.TMP_Dropdown CameraModeDropdown;

    private GameObject _runtimeRenderScaleRow;
    private GameObject _runtimeExposureRow;
    private GameObject _runtimeCharaRow;

    [Header("Free Camera")]
    public GameObject FreeCameraSettingsTab;
    public Slider FOVFree;
    public Slider CameraRotationFree;
    public Slider MovementSpeedFree;
    public Slider RotationSpeedSlider;

    [Header("Orbit Camera")]
    public GameObject OrbitCameraSettingsTab;
    public Slider FOVOrbit;
    public Slider CameraDistance;
    public Slider CameraHeightSlider;
    public Slider CameraRotationOrbit;
    public Slider TargetHeightSlider;
    public Slider ZoomSpeedSlider;
    public Slider MovementSpeedOrbit;

    [Space]
    public Toggle UseAnimationCamera;

    public int CameraMode
    {
        get { return CameraModeDropdown.value; }
        set { CameraModeDropdown.value = value; UpdateSettingsPanel(); }
    }

    public float FOV
    {
        get { return CameraMode == 0 ? FOVOrbit.value : FOVFree.value; }
        set { 
            if (CameraMode == 0)
            {
                FOVOrbit.value = value;
            }
            else
            {
                FOVFree.value = value;
            }
        }
    }

    public float CameraRotation
    {
        get { return CameraMode == 0 ? CameraRotationOrbit.value : CameraRotationFree.value; }
        set
        {
            if (CameraMode == 0)
            {
                CameraRotationOrbit.value = value;
            }
            else
            {
                CameraRotationFree.value = value;
            }
        }
    }

    public float ZoomSpeed
    {
        get { return ZoomSpeedSlider.value; }
        set { ZoomSpeedSlider.value = value; }
    }

    public float MovementSpeed
    {
        get { return CameraMode == 0 ? MovementSpeedOrbit.value : MovementSpeedFree.value; }
        set
        {
            if (CameraMode == 0)
            {
                MovementSpeedOrbit.value = value;
            }
            else
            {
                MovementSpeedFree.value = value;
            }
        }
    }

    public float RotationSpeed
    {
        get { return RotationSpeedSlider.value; }
        set { RotationSpeedSlider.value = value; }
    }

    public float CameraHeight
    {
        get { return CameraHeightSlider.value; }
        set { CameraHeightSlider.value = value; }
    }

    public float TargetHeight
    {
        get { return TargetHeightSlider.value; }
        set { TargetHeightSlider.value = value; }
    }

    public void UpdateSettingsPanel()
    {
        if (CameraMode == 0)
        {
            OrbitCameraSettingsTab.SetActive(true);
            FreeCameraSettingsTab.SetActive(false);
        }
        else
        {
            FreeCameraSettingsTab.SetActive(true);
            OrbitCameraSettingsTab.SetActive(false);
        }
    }

    /// <summary> Converts values 1-4 to valid AA values </summary>
    public void ChangeAntiAliasing(int value)
    {
        int[] aaValues = { 0, 2, 4, 8 };

        QualitySettings.antiAliasing = aaValues[value];

        if (Config.Instance.AntiAliasing != value)
        {
            Config.Instance.AntiAliasing = value;
            Config.Instance.UpdateConfig(false);
        }
    }

    /// <summary> Clones the Anti-Aliasing row at runtime as a Render Scale row (mirrors
    /// the FrameRate dropdown pattern in UISettingsOther) so no scene edit is needed. </summary>
    public void EnsureRenderScaleDropdown()
    {
        if (_runtimeRenderScaleRow != null || AAModeDropdown == null)
            return;

        var sourceRow = AAModeDropdown.transform;
        var parent = sourceRow.parent;
        if (parent == null)
            return;

        // Capture AA's position and the pitch to its next sibling BEFORE cloning.
        float sourceY = 0f;
        float shift = 35f;
        if (sourceRow is RectTransform srcRect)
        {
            sourceY = srcRect.anchoredPosition.y;
            Transform probe = null;
            for (int i = sourceRow.GetSiblingIndex() + 1; i < parent.childCount; i++)
            {
                Transform t = parent.GetChild(i);
                if (t != null && !t.Equals(sourceRow))
                {
                    probe = t;
                    break;
                }
            }
            if (probe is RectTransform probeRect)
            {
                float d = sourceY - probeRect.anchoredPosition.y;
                if (d >= 1f)
                    shift = d;
            }
        }

        _runtimeRenderScaleRow = Instantiate(sourceRow.gameObject, parent);
        _runtimeRenderScaleRow.name = "RenderScale";
        _runtimeRenderScaleRow.transform.SetSiblingIndex(sourceRow.GetSiblingIndex() + 1);

        // Drop the clone one pitch below AA, then push every original sibling below
        // it down one pitch so nothing overlaps.
        if (_runtimeRenderScaleRow.transform is RectTransform newRect)
            newRect.anchoredPosition = new Vector2(newRect.anchoredPosition.x, sourceY - shift);

        for (int i = _runtimeRenderScaleRow.transform.GetSiblingIndex() + 1; i < parent.childCount; i++)
        {
            Transform t = parent.GetChild(i);
            if (t != null && t is RectTransform rt)
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y - shift);
        }

        var label = _runtimeRenderScaleRow.transform.GetChild(0).GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.text = "Render Scale";

        var dd = _runtimeRenderScaleRow.GetComponentInChildren<TMP_Dropdown>(true);
        dd.name = "RenderScaleDropdown";
        dd.ClearOptions();
        dd.AddOptions(new List<string> { "0.75", "0.90", "1.00 (Default)" });
        dd.onValueChanged = new TMP_Dropdown.DropdownEvent();
        dd.onValueChanged.AddListener(ChangeRenderScale);
        dd.SetValueWithoutNotify(RenderScaleToDropdownValue(Config.Instance.RenderScale));
        dd.RefreshShownValue();
    }

    private int RenderScaleToDropdownValue(float scale)
    {
        if (scale <= 0.8f) return 0;
        if (scale <= 0.95f) return 1;
        return 2;
    }

    private float DropdownValueToRenderScale(int value)
    {
        switch (value)
        {
            case 0: return 0.75f;
            case 1: return 0.9f;
            default: return 1f;
        }
    }

    /// <summary> Applies a render-resolution multiplier (0.75 / 0.9 / 1.0) globally —
    /// the same URP asset serves both Live and character views. </summary>
    public void ChangeRenderScale(int value)
    {
        var scale = DropdownValueToRenderScale(value);
        if (!Mathf.Approximately(Config.Instance.RenderScale, scale))
        {
            Config.Instance.RenderScale = scale;
            Config.Instance.UpdateConfig(false);
        }
        UmaViewerMain.ApplyRenderScale();
    }

    /// <summary> Clones a settings row as a new dropdown row one pitch below the source. </summary>
    private GameObject CloneDropdownRow(Transform sourceRow, string name)
    {
        var parent = sourceRow.parent;
        if (parent == null)
            return null;

        float sourceY = 0f;
        float shift = 35f;
        if (sourceRow is RectTransform srcRect)
        {
            sourceY = srcRect.anchoredPosition.y;
            Transform probe = null;
            for (int i = sourceRow.GetSiblingIndex() + 1; i < parent.childCount; i++)
            {
                Transform t = parent.GetChild(i);
                if (t != null && !t.Equals(sourceRow))
                {
                    probe = t;
                    break;
                }
            }
            if (probe is RectTransform probeRect)
            {
                float d = sourceY - probeRect.anchoredPosition.y;
                if (d >= 1f)
                    shift = d;
            }
        }

        var row = Instantiate(sourceRow.gameObject, parent);
        row.name = name;
        row.transform.SetSiblingIndex(sourceRow.GetSiblingIndex() + 1);

        if (row.transform is RectTransform newRect)
            newRect.anchoredPosition = new Vector2(newRect.anchoredPosition.x, sourceY - shift);

        for (int i = row.transform.GetSiblingIndex() + 1; i < parent.childCount; i++)
        {
            Transform t = parent.GetChild(i);
            if (t != null && t is RectTransform rt)
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y - shift);
        }

        var label = row.transform.GetChild(0).GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.text = name;

        return row;
    }

    /// <summary> Clones the Render Scale row at runtime as an Exposure row so no scene
    /// edit is needed — exposure controls overall stage brightness. </summary>
    public void EnsureExposureDropdown()
    {
        if (_runtimeExposureRow != null || _runtimeRenderScaleRow == null)
            return;

        _runtimeExposureRow = CloneDropdownRow(_runtimeRenderScaleRow.transform, "Exposure");
        if (_runtimeExposureRow == null)
            return;

        var dd = _runtimeExposureRow.GetComponentInChildren<TMP_Dropdown>(true);
        dd.name = "ExposureDropdown";
        dd.ClearOptions();
        dd.AddOptions(new List<string> { "+2", "+1", "0 (Default)", "-1", "-2", "-3" });
        dd.onValueChanged = new TMP_Dropdown.DropdownEvent();
        dd.onValueChanged.AddListener(ChangeExposure);
        dd.SetValueWithoutNotify(ExposureToDropdownValue(Config.Instance.Exposure));
        dd.RefreshShownValue();
    }

    /// <summary> Character brightness row: rim/toon boost so idols pop on a dark stage. </summary>
    public void EnsureCharaDropdown()
    {
        if (_runtimeCharaRow != null || _runtimeExposureRow == null)
            return;

        _runtimeCharaRow = CloneDropdownRow(_runtimeExposureRow.transform, "Chara Brightness");
        if (_runtimeCharaRow == null)
            return;

        var dd = _runtimeCharaRow.GetComponentInChildren<TMP_Dropdown>(true);
        dd.name = "CharaBrightnessDropdown";
        dd.ClearOptions();
        dd.AddOptions(new List<string> { "0.5", "0.75", "1.0 (Default)", "1.3", "1.6", "2.0" });
        dd.onValueChanged = new TMP_Dropdown.DropdownEvent();
        dd.onValueChanged.AddListener(ChangeCharaBrightness);
        dd.SetValueWithoutNotify(BoostToDropdownValue(Config.Instance.CharaBrightness));
        dd.RefreshShownValue();
    }

    private int BoostToDropdownValue(float boost)
    {
        if (boost >= 1.8f) return 5;
        if (boost >= 1.45f) return 4;
        if (boost >= 1.15f) return 3;
        if (boost >= 0.85f) return 2;
        if (boost >= 0.65f) return 1;
        return 0;
    }

    private float DropdownValueToBoost(int value)
    {
        switch (value)
        {
            case 0: return 0.5f;
            case 1: return 0.75f;
            case 3: return 1.3f;
            case 4: return 1.6f;
            case 5: return 2f;
            default: return 1f;
        }
    }

    public void ChangeCharaBrightness(int value)
    {
        var b = DropdownValueToBoost(value);
        if (!Mathf.Approximately(Config.Instance.CharaBrightness, b))
        {
            Config.Instance.CharaBrightness = b;
            Config.Instance.UpdateConfig(false);
        }
    }

    private int ExposureToDropdownValue(float exposure)
    {
        if (exposure >= 1.5f) return 0;
        if (exposure >= 0.5f) return 1;
        if (exposure >= -0.5f) return 2;
        if (exposure >= -1.5f) return 3;
        if (exposure >= -2.5f) return 4;
        return 5;
    }

    private float DropdownValueToExposure(int value)
    {
        switch (value)
        {
            case 0: return 2f;
            case 1: return 1f;
            case 3: return -1f;
            case 4: return -2f;
            case 5: return -3f;
            default: return 0f;
        }
    }

    public void ChangeExposure(int value)
    {
        var exp = DropdownValueToExposure(value);
        if (!Mathf.Approximately(Config.Instance.Exposure, exp))
        {
            Config.Instance.Exposure = exp;
            Config.Instance.UpdateConfig(false);
        }
    }
}
