using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Gallop.Live;

public class LiveViewerUI : MonoBehaviour
{
    public static LiveViewerUI Instance;

    public UnityEngine.UI.Slider ProgressBar;

    public RectTransform BottonUITransform;

    public Dropdown FrameRateDropDown;

    public GameObject RecordingUI;

    public Text RecordingText;

    public Text LyricsText;

    public List<UmaLyricsData> CurrentLyrics = new List<UmaLyricsData>();

    float targetHeight = 0;
    float height;
    private float _lastPointerActivityTime;
    private bool _pointerWasInsideWindow;
    private const float PointerIdleHideDelay = 2.5f;

    private void Awake()
    {
        if (BottonUITransform == null)
        {
            // Try to find the bottom UI transform dynamically
            var canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                BottonUITransform = canvas.GetComponentInChildren<RectTransform>();
            }
        }

        if (BottonUITransform == null)
        {
            Debug.LogError("[LiveViewerUI] Bottom UI transform is not assigned.");
            return;
        }

        // Find the FrameRate dropdown dynamically if not assigned
        if (FrameRateDropDown == null)
        {
            FrameRateDropDown = FindObjectOfType<Dropdown>(true);
            if (FrameRateDropDown == null)
            {
                Debug.LogWarning("[LiveViewerUI] FrameRateDropDown not found, searching by name...");
                var allDropdowns = FindObjectsOfType<Dropdown>(true);
                foreach (var dd in allDropdowns)
                {
                    if (dd != null && dd.name != null &&
                        (dd.name.IndexOf("FrameRate", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         dd.name.IndexOf("FPS", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        FrameRateDropDown = dd;
                        break;
                    }
                }
                // Fallback: use first dropdown found
                if (FrameRateDropDown == null && allDropdowns.Length > 0)
                {
                    FrameRateDropDown = allDropdowns[0];
                }
            }
        }

        if (Config.Instance == null)
        {
            new Config();
        }

        Instance = this;

        // Apply options after finding the dropdown
        if (FrameRateDropDown != null)
        {
            ApplyFrameRateOptions();
            UmaViewerMain.ApplyFrameRateLimit();
        }
        else
        {
            Debug.LogWarning("[LiveViewerUI] No dropdown found in scene.");
        }

        height = BottonUITransform.rect.height;
        targetHeight = 0;
        _lastPointerActivityTime = Time.unscaledTime;
        Invoke(nameof(HideSlider), 1.5f);
    }

    private void Update()
    {
        if (BottonUITransform == null)
            return;

        // Pointer-event callbacks can be missed while the panel is hidden or when
        // another full-screen graphic owns the raycast. Polling keeps hover recovery
        // independent of the hidden panel's hierarchy state.
        Vector3 pointer = Input.mousePosition;
        bool insideWindow = pointer.x >= 0f && pointer.x <= Screen.width &&
                            pointer.y >= 0f && pointer.y <= Screen.height;

        if (insideWindow && (!_pointerWasInsideWindow || Input.GetAxisRaw("Mouse X") != 0f || Input.GetAxisRaw("Mouse Y") != 0f))
        {
            _lastPointerActivityTime = Time.unscaledTime;
            ShowSlider();
        }
        else if (!insideWindow)
        {
            _lastPointerActivityTime = Time.unscaledTime;
        }

        _pointerWasInsideWindow = insideWindow;

        if (insideWindow && Time.unscaledTime - _lastPointerActivityTime >= PointerIdleHideDelay)
            HideSlider();
    }

    public void OnMouse(bool isEnter)
    {
        CancelInvoke(nameof(HideSlider));

        if (isEnter)
        {
            _lastPointerActivityTime = Time.unscaledTime;
            ShowSlider();
        }
        else
        {
            Invoke(nameof(HideSlider), PointerIdleHideDelay);
        }
    }

    private void ShowSlider()
    {
        if (BottonUITransform == null)
            return;

        CancelInvoke(nameof(HideSlider));
        targetHeight = 0f;
    }

    private void FixedUpdate()
    {
        if (BottonUITransform == null)
            return;

        BottonUITransform.anchoredPosition = Vector2.Lerp(BottonUITransform.anchoredPosition, new Vector2(0, targetHeight), Time.fixedDeltaTime * 5);
    }

    private void HideSlider()
    {
        if (BottonUITransform == null)
            return;

        targetHeight = -height;
    }

    public void SetFrameRate(int fps)
    {
        if (fps == 1) Config.Instance.TargetFrameRate = 30;
        else if (fps == 2) Config.Instance.TargetFrameRate = -1;
        else Config.Instance.TargetFrameRate = 60;
        Config.Instance.UpdateConfig(false);
        UmaViewerMain.ApplyFrameRateLimit();
    }

    private void ApplyFrameRateOptions()
    {
        if (FrameRateDropDown == null) return;

        FrameRateDropDown.ClearOptions();
        FrameRateDropDown.AddOptions(new List<string>
        {
            "60", "30", "Unlimited"
        });
        int frameRate = Config.Instance.GetTargetFrameRate();
        FrameRateDropDown.SetValueWithoutNotify(frameRate == 30 ? 1 : (frameRate == -1 ? 2 : 0));
        FrameRateDropDown.RefreshShownValue();
        FrameRateDropDown.onValueChanged.RemoveListener(SetFrameRate);
        FrameRateDropDown.onValueChanged.AddListener(SetFrameRate);
    }

    private string _cachedLyric;

    public void UpdateLyrics(float time)
    {
        var text = UmaUtility.GetCurrentLyrics(time, CurrentLyrics);
        if (!string.Equals(text, _cachedLyric, System.StringComparison.Ordinal))
        {
            _cachedLyric = text;
            LyricsText.text = text;
        }
    }
}
