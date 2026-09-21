// deterministic frame-time benchmark for the diagnostic container, not a gameplay feature.
// manual mode: arm the profiler any time; it starts sampling only once a live is
// actually playing, so the user can boot normally, pick a concert by hand, and the
// numbers still come out clean without any command-line autostart.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Gallop.Live;
using UnityEngine;
using UnityEngine.Profiling;

public class FrameTimeProfiler : MonoBehaviour
{
    private const string SummaryName = "uma_bench_summary.txt";

    private static bool _enabled;
    private static float _warmupSeconds = 12f;
    private static float _sampleSeconds = 30f;
    private static bool _manualMode;

    // manual-mode state
    private bool _liveWasPlaying;

    private bool _sampling;
    private float _clock;
    private float _lastBeat = -1f;
    private readonly List<float> _frameTimes = new List<float>(65536);
    private long _gcAllocAccumulated;
    private long _gcLastTotalMemory;
    private int _gcCollectionsBefore;

    private static void ParseArgs()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--bench") _enabled = true;
            else if (args[i] == "--bench-manual") { _enabled = true; _manualMode = true; }
            else if (args[i] == "--bench-warmup" && i + 1 < args.Length) float.TryParse(args[i + 1], out _warmupSeconds);
            else if (args[i] == "--bench-seconds" && i + 1 < args.Length) float.TryParse(args[i + 1], out _sampleSeconds);
        }
    }

    // the harness polls marker files; bench output lives beside the exe next to
    // UmaViewer.log so the user finds everything in the install folder.
    private static string OutputDirectory()
    {
        try
        {
            // Application.dataPath is <install>/UmaViewer_Data, so its parent is
            // the folder the exe lives in.
            string root = Path.GetDirectoryName(Application.dataPath.TrimEnd('/', '\\'));
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                return root;
        }
        catch
        {
            // any failure falls back to persistent data below.
        }
        return Application.persistentDataPath;
    }

    // a live counts as playing when the director finished setup and the ui is in live mode.
    private static bool IsLivePlaying()
    {
        var director = Gallop.Live.Director.instance;
        var ui = UmaViewerUI.Instance;
        return director != null && director._isLiveSetup && ui != null && ui.LiveTime;
    }

    private void Awake()
    {
        ParseArgs();
        if (!_enabled)
        {
            Destroy(this);
            return;
        }

        _gcAllocAccumulated = 0;
        _gcLastTotalMemory = GC.GetTotalMemory(false);
        _gcCollectionsBefore = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        Debug.Log($"[bench] armed: manual={_manualMode} warmup={_warmupSeconds}s sample={_sampleSeconds}s");
        StateLog($"armed manual={_manualMode} warmup={_warmupSeconds}s sample={_sampleSeconds}s");
        WrapEnginePhases();
    }

    // brackets unity's own player-loop subsystems so the bench summary attributes
    // engine work (animation, physics, render prep) the script sections never see.
    private static void WrapEnginePhases()
    {
        try
        {
            SectionProfiler.WrapUpdateGroupSubsystems();
        }
        catch
        {
            // a failed wrap only costs the section rows; never kill the bench.
        }
    }

    // logs every enabled MonoBehaviour in the scene grouped by type once per window
    // so a slow frame can be traced to the scripts that actually run every frame.
    private static void LogBehaviourCensus()
    {
        try
        {
            var all = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var b in all)
            {
                if (b == null || !b.gameObject.activeInHierarchy)
                    continue;
                string key = b.GetType().FullName;
                counts.TryGetValue(key, out int n);
                counts[key] = n + 1;
            }
            Gallop.Live.Director.FileLog($"[bench] behaviour census: types={counts.Count} instances={all.Length}");
            var rows = new System.Collections.Generic.List<string>(counts.Keys);
            rows.Sort();
            foreach (var key in rows)
                Gallop.Live.Director.FileLog($"[bench] behaviour {key} x{counts[key]}");
        }
        catch
        {
            // the census is diagnostics; never let it kill the bench.
        }
    }

    // bench state rides the runtime log so a run reports with a single file.
    private static void StateLog(string message)
    {
        try
        {
            Gallop.Live.Director.FileLog($"[bench] {message}");
        }
        catch
        {
            // state lines are diagnostics; never let one kill the bench.
        }
    }

    private void Update()
    {
        if (!_enabled)
            return;

        if (_manualMode)
        {
            // wait indefinitely for the user to start a live; a fresh marker each
            // idle minute proves the profiler is alive while waiting.
            bool playing = IsLivePlaying();
            if (!playing)
            {
                if (_sampling)
                {
                    // the live ended mid-window; keep the clean frames we have.
                    FinishSampling();
                    return;
                }

                _liveWasPlaying = false;
                return;
            }

            if (!_liveWasPlaying)
            {
                // the user just entered the live; start the window from here.
                _liveWasPlaying = true;
                _clock = 0f;
                _sampling = false;
                Debug.Log("[bench] live detected, starting window");
            }
        }

        // wall-clock only: the stage load produces multi-second frames, so scaled
        // time would burn the warm-up instantly and contaminate the sample.
        _clock += Time.unscaledDeltaTime;

        // heartbeat every 10s of wall clock: proves the loop ticks and names the
        // gate the clock is stuck at when the harness times out.
        float beat = Mathf.Floor(_clock / 10f);
        if (beat > _lastBeat)
        {
            _lastBeat = beat;
            StateLog($"beat t={_clock:F0}s sampling={_sampling}");
        }

        if (!_sampling && _clock < _warmupSeconds)
            return;

        if (!_sampling)
        {
            _sampling = true;
            StateLog($"sampling open at t={_clock:F0}s");
            LogBehaviourCensus();
        }

        if (_clock >= _warmupSeconds + _sampleSeconds)
        {
            FinishSampling();
            return;
        }

        long gcNow = GC.GetTotalMemory(false);
        long gcDelta = gcNow - _gcLastTotalMemory;
        if (gcDelta > 0)
            _gcAllocAccumulated += gcDelta;
        _gcLastTotalMemory = gcNow;

        _frameTimes.Add(Time.unscaledDeltaTime * 1000f);
        SectionProfiler.AccountFrame(Time.unscaledDeltaTime * 1000f);
        CaptureFrameTiming();
        SectionProfiler.SetEnabled(true);
    }

    // frame-timing accumulation: splits each frame into cpu main-thread work,
    // render-thread work, present wait, and gpu time, so a low-utilization
    // stall is distinguishable from real main-thread cost.
    private double _ftCpuMainThreadMs;
    private double _ftCpuRenderThreadMs;
    private double _ftPresentWaitMs;
    private double _ftGpuMs;
    private int _ftSamples;

    private void CaptureFrameTiming()
    {
        uint got = FrameTimingManager.GetLatestTimings((uint)_frameTimings.Length, _frameTimings);
        for (int i = 0; i < got; i++)
        {
            var t = _frameTimings[i];
            // unity's FrameTiming fields are already milliseconds; accumulate raw.
            _ftCpuMainThreadMs += t.cpuMainThreadFrameTime;
            _ftCpuRenderThreadMs += t.cpuRenderThreadFrameTime;
            _ftGpuMs += t.gpuFrameTime;
            // present-wait: total cpu frame time minus the main-thread slice is
            // the render-queue/present overhead on this engine version.
            _ftPresentWaitMs += t.cpuFrameTime - t.cpuMainThreadFrameTime;
            _ftSamples++;
        }
    }

    private FrameTiming[] _frameTimings = new FrameTiming[1];

    private void WriteSummary()
    {
        if (_frameTimes.Count == 0)
            return;

        List<float> sorted = _frameTimes.OrderBy(t => t).ToList();
        int n = sorted.Count;
        float P(float p) => sorted[Mathf.Clamp((int)(p * (n - 1)), 0, n - 1)];
        float sum = sorted.Sum();
        float avg = sum / n;

        // bucket histogram + stall stats so a single run also exposes the shape
        // of the distribution, not just averages. frames >100ms are stalls.
        int[] buckets = { 0, 0, 0, 0, 0, 0 };
        float[] edgesMs = { 8.33f, 16.67f, 33.33f, 50f, 100f, float.MaxValue };
        int stalls = 0;
        float stallMaxMs = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = sorted[i];
            for (int b = 0; b < buckets.Length; b++)
            {
                if (t <= edgesMs[b])
                {
                    buckets[b]++;
                    break;
                }
            }
            if (t > 100f)
            {
                stalls++;
                if (t > stallMaxMs) stallMaxMs = t;
            }
        }

        int gcColsNow = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        SectionProfiler.SetEnabled(false);
        string sectionReport = SectionProfiler.Dump(70) + "\n" + SectionProfiler.GapReport();
        SectionProfiler.Reset();
        SectionProfiler.ResetAccounting();

        // note: per-thread and gpu timings are not available here; the FrameTiming
        // manager module is not compiled into this project's player build.
        string summary = string.Join(Environment.NewLine,
            "uma bench summary",
            $"samples={n}",
            $"avg_ms={avg:F3} avg_fps={(avg > 0f ? 1000f / avg : 0f):F1}",
            $"median_ms={P(0.5f):F3}",
            $"p99_ms={P(0.99f):F3} p99_9_ms={P(0.999f):F3}",
            $"min_ms={sorted[0]:F3} max_ms={sorted[^1]:F3}",
            "hist_pct: " +
                $"<8.33={100f * buckets[0] / n:F1} <16.67={100f * buckets[1] / n:F1} " +
                $"<33.33={100f * buckets[2] / n:F1} <50={100f * buckets[3] / n:F1} " +
                $"<100={100f * buckets[4] / n:F1} >=100={100f * buckets[5] / n:F1}",
            $"stalls_gt100ms={stalls} stall_max_ms={stallMaxMs:F1}",
            $"gc_alloc_mb={_gcAllocAccumulated / 1048576.0:F2}",
            $"gc_collections={gcColsNow - _gcCollectionsBefore}",
            $"target_fps={Application.targetFrameRate} resolution={Screen.width}x{Screen.height}",
            _ftSamples > 0
                ? $"frame-timing avg: cpu_main={_ftCpuMainThreadMs / _ftSamples:F2}ms cpu_render={_ftCpuRenderThreadMs / _ftSamples:F2}ms present_wait={_ftPresentWaitMs / _ftSamples:F2}ms gpu={_ftGpuMs / _ftSamples:F2}ms samples={_ftSamples}"
                : "frame-timing: capture unavailable in this build",
            _ftSamples > 0 && avg > 0f
                ? $"load: main_thread={100f * (float)(_ftCpuMainThreadMs / _ftSamples) / avg:F1}% of frame gpu={100f * (float)(_ftGpuMs / _ftSamples) / avg:F1}% of frame cores={SystemInfo.processorCount} cpu_type={SystemInfo.processorType}"
                : "",
            sectionReport);

        string outPath = Path.Combine(OutputDirectory(), SummaryName);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, summary);
            Debug.Log($"[bench] summary written -> {outPath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[bench] could not write {outPath}: {ex.Message}");
        }

        Debug.Log("[bench] done\n" + summary);

        // mirror the numbers into UmaViewer.log next to the exe so the
        // optimization results live with the rest of the runtime log.
        try
        {
            Gallop.Live.Director.FileLog("[bench] summary\n" + summary.Replace("\n", "\n[bench] "));
        }
        catch
        {
            // the mirror is convenience only; the file write above already succeeded.
        }
    }

    private void FinishSampling()
    {
        WriteSummary();
        _enabled = false;
        _manualMode = false;
        Debug.Log("[bench] BENCH_DONE");
    }
}