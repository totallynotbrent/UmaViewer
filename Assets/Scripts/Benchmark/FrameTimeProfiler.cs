// deterministic frame-time benchmark for the diagnostic container, not a gameplay feature.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public class FrameTimeProfiler : MonoBehaviour
{
    private const string SummaryName = "uma_bench_summary.txt";

    private static bool _enabled;
    private static float _warmupSeconds = 12f;
    private static float _sampleSeconds = 30f;

    private bool _sampling;
    private readonly List<float> _frameTimes = new List<float>(65536);
    private long _gcBytesBefore;
    private int _gcCollectionsBefore;

    private static void ParseArgs()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--bench") _enabled = true;
            else if (args[i] == "--bench-warmup" && i + 1 < args.Length) float.TryParse(args[i + 1], out _warmupSeconds);
            else if (args[i] == "--bench-seconds" && i + 1 < args.Length) float.TryParse(args[i + 1], out _sampleSeconds);
        }
    }

    private void Awake()
    {
        ParseArgs();
        if (!_enabled)
        {
            Destroy(this);
            return;
        }

        _gcBytesBefore = GC.GetTotalMemory(false);
        _gcCollectionsBefore = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

        // the harness waits for this marker so the measurement window is measured
        // from arming, not from container start, which absorbs boot-time variance.
        try
        {
            string markPath = Path.Combine(Application.persistentDataPath, "uma_bench_armed.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(markPath));
            File.WriteAllText(markPath, DateTime.Now.ToString(CultureInfo.InvariantCulture));
        }
        catch
        {
            // a missing marker only costs a harness timeout; boot continues.
        }

        Debug.Log($"[bench] armed: warmup={_warmupSeconds}s sample={_sampleSeconds}s");

        // the viewer never exits by itself on the bench rig, so sampling is driven by
        // wall-clock duration; the summary is written once the window closes.
        Invoke(nameof(FinishSampling), _warmupSeconds + _sampleSeconds);
    }

    private void Update()
    {
        if (!_enabled)
            return;

        // the warm-up window is not measured; it only lets the concert reach steady state.
        if (!_sampling && _warmupSeconds > 0f)
        {
            _warmupSeconds -= Time.deltaTime;
            return;
        }

        _sampling = true;
        _frameTimes.Add(Time.deltaTime * 1000f);
    }

    private void WriteSummary()
    {
        if (_frameTimes.Count == 0)
            return;

        List<float> sorted = _frameTimes.OrderBy(t => t).ToList();
        int n = sorted.Count;
        float P(float p) => sorted[Mathf.Clamp((int)(p * (n - 1)), 0, n - 1)];
        float sum = sorted.Sum();
        float avg = sum / n;

        long gcNow = GC.GetTotalMemory(false);
        int gcColsNow = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

        // note: per-thread and gpu timings are not available here; the FrameTiming
        // manager module is not compiled into this project's player build.
        string summary = string.Join(Environment.NewLine,
            "uma bench summary",
            $"samples={n}",
            $"avg_ms={avg:F3} avg_fps={(avg > 0f ? 1000f / avg : 0f):F1}",
            $"median_ms={P(0.5f):F3}",
            $"p99_ms={P(0.99f):F3} p99_9_ms={P(0.999f):F3}",
            $"min_ms={sorted[0]:F3} max_ms={sorted[^1]:F3}",
            $"gc_alloc_mb={(gcNow - _gcBytesBefore) / 1048576.0:F2}",
            $"gc_collections={gcColsNow - _gcCollectionsBefore}",
            $"target_fps={Application.targetFrameRate} resolution={Screen.width}x{Screen.height}",
            "thread/gpu timings: unavailable (frame-timing module not compiled)");

        string outPath = Path.Combine(Application.persistentDataPath, SummaryName);
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
    }

    private void FinishSampling()
    {
        WriteSummary();
        _enabled = false;
        // the harness greps this marker in the player log to know the run ended.
        Debug.Log("[bench] BENCH_DONE");
    }
}