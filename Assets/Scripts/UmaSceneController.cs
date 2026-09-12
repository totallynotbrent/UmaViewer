using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UmaSceneController:MonoBehaviour
{
    public static UmaSceneController instance;
    public GameObject CavansPrefab;
    public GameObject CavansInstance;

    public GameObject LoadingProgressPanel;
    public Slider LoadingProgressSlider;
    public TextMeshProUGUI LoadingProgressText;
    private bool _isTransitioning; // ponytail: guard overlapping loads that destroy Animation

    private void Awake()
    {
        if (instance)
        {
            DestroyImmediate(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(this);
    }

    private void Start()
    {
        UmaAssetManager.OnLoadProgressChange += LoadingProgressChange;
    }

    public static void LoadScene(string name, Action OnSceneloaded = null, Action OnPrevSceneUnloaded = null)
    {
        if (instance == null) { Debug.LogWarning($"[Scene] LoadScene '{name}' dropped: instance==null"); return; }
        // ponytail: drop concurrent load to avoid destroying Animation mid-transition; upgrade path: queue it
        if (instance._isTransitioning) { Debug.LogWarning($"[Scene] LoadScene '{name}' dropped: already transitioning"); return; }
        Debug.LogWarning($"[Scene] LoadScene '{name}' starting");
        instance.StartCoroutine(instance.LoadLiveSceneAsync(name, OnSceneloaded, OnPrevSceneUnloaded));
    }

    IEnumerator LoadLiveSceneAsync(string sceneName, Action OnSceneloaded, Action OnPrevSceneUnloaded)
    {
        _isTransitioning = true;
        GameObject transitionObj = null;
        Animation animation = null;
        try
        {
        // ponytail: local instance avoids field clobber; always clean previous
        if (CavansInstance) Destroy(CavansInstance);
        if (!CavansPrefab) { Debug.LogWarning($"[Scene] LoadLiveSceneAsync '{sceneName}' aborted: no CavansPrefab"); _isTransitioning = false; yield break; }
        transitionObj = CavansInstance = Instantiate(CavansPrefab, transform);
        animation = transitionObj ? transitionObj.GetComponent<Animation>() : null;
        if (animation) animation.Play("SceneTransition_s");
        yield return new WaitUntil(() => animation == null || transitionObj == null || !animation.isPlaying);

        // Set the current Scene to be able to unload it later
        Scene currentScene = SceneManager.GetActiveScene();

        // The Application loads the Scene in the background at the same time as the current Scene.
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

        // Wait until the last operation fully loads to return anything
        yield return new WaitUntil(()=> asyncLoad.isDone);
        Debug.LogWarning($"[Scene] LoadLiveSceneAsync '{sceneName}' loaded, invoking OnSceneloaded");

        try { OnSceneloaded?.Invoke(); } catch (System.Exception e) { Debug.LogException(e); }

        // Unload the previous Scene
        AsyncOperation asyncUnLoad = SceneManager.UnloadSceneAsync(currentScene);
        yield return new WaitUntil(() => asyncUnLoad == null || asyncUnLoad.isDone);

        try { OnPrevSceneUnloaded?.Invoke(); } catch (System.Exception e) { Debug.LogException(e); }

        // ponytail: guard destroyed Animation (MissingReference from overlapping Unload)
        if (animation && transitionObj && CavansInstance == transitionObj)
        {
            animation.Play("SceneTransition_e");
            yield return new WaitUntil(() => animation == null || transitionObj == null || !animation.isPlaying);
        }
        if (transitionObj && CavansInstance == transitionObj) Destroy(transitionObj);
        if (CavansInstance == transitionObj) CavansInstance = null;
        } finally { _isTransitioning = false; }
    }

    public void LoadingProgressChange(int curren, int target, string message = null)
    {
        if(curren == -1)
        {
            LoadingProgressPanel.SetActive(false);
        }
        else if (target > 0)
        {
            LoadingProgressPanel.SetActive(true);
            LoadingProgressSlider.value = (float)curren / target;
            if (string.IsNullOrEmpty(message))
            {
                LoadingProgressText.text = $"Loading...({curren}/{target})";
            }
            else
            {
                LoadingProgressText.text = $"{message}({curren}/{target})";
            }
        }
    }
}

