// development-only bootstrap that auto-starts one live in PerfTestScene and loads the real boot scene additively.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConcertTestBootstrap : MonoBehaviour
{
    [SerializeField] private int music_id = 1004;

    private static int _override_music_id = -1;

    private bool _started;

    private IEnumerator _waiter;

    private static int ResolveMusicId()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--music" && i + 1 < args.Length && int.TryParse(args[i + 1], out _override_music_id))
                break;
        }
        return _override_music_id >= 0 ? _override_music_id : music_id;
    }

    private void Start()
    {
        if (_started)
            return;
        _started = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log("[perf] bootstrap start, loading Version2 additively");
        SceneManager.LoadSceneAsync("Version2", LoadSceneMode.Additive);

        int target = ResolveMusicId();
        Debug.Log($"[perf] bootstrap waiting for UmaViewerMain instance");
        _waiter = WaitForMain(target);
        StartCoroutine(_waiter);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[perf] scene loaded {scene.name} mode={mode}");
        Debug.Log($"[perf] main instance present={UmaViewerMain.Instance != null}, ui instance present={UmaViewerUI.Instance != null}");
    }

    private IEnumerator WaitForMain(int target)
    {
        float waited = 0f;
        while (waited < 300f)
        {
            var main = UmaViewerMain.Instance;
            if (main != null && main.Lives != null && main.Lives.Count > 0)
                break;
            if (waited > 0f && Mathf.Abs(waited - Mathf.FloorToInt(waited)) < 0.01f)
                Debug.Log($"[perf] waiting for main data, elapsed={waited:F0}s");
            yield return new WaitForSeconds(0.5f);
            waited += 0.5f;
        }

        var resolved = UmaViewerMain.Instance;
        if (resolved == null || resolved.Lives == null || resolved.Lives.Count == 0)
        {
            Debug.LogError("[perf] timeout waiting for UmaViewerMain Lives");
            yield break;
        }

        LiveEntry entry = null;
        foreach (var live in resolved.Lives)
        {
            if (live.MusicId == target)
            {
                entry = live;
                break;
            }
        }
        if (entry == null)
        {
            Debug.LogWarning($"[perf] music {target} not found in Lives, falling back to first");
            entry = resolved.Lives[0];
        }

        if (UmaViewerUI.Instance == null)
        {
            Debug.LogError("[perf] UmaViewerUI instance missing, cannot start live");
            yield break;
        }

        Debug.Log($"[perf] starting live {entry.MusicId}, song={entry.SongName}");
        UmaViewerUI.Instance.AutoStartLive(entry);
        Debug.Log($"[perf] concert started music={entry.MusicId}");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}