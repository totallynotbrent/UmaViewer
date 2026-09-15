// deterministic frame-time benchmark for the diagnostic container, not a gameplay feature.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

public class FrameTimeProfiler : MonoBehaviour
{
    private const uint TimingFrameCount = 64;
    private const string CsvName = "uma_bench_frames.csv";
    private const string SummaryName = "uma_bench_summary.txt";

    private static bool _enabled;
    private static float _warmupSeconds = 12f;
    private static float _sampleSeconds = 30f;

    private bool _sampling;
    private float _sampledElapsed;
    private readonly List<float> _frameTimes = new List<float>(65536);
    private readonly FrameTiming[] _timings = new FrameTiming[TimingFrameCount];
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

        FrameTimingManager.EnableTimingStats(true);
        _gcBytesBefore = GC.GetTotalMemory(false);
        _gcCollectionsBefore = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        Debug.Log($"[bench] armed: warmup={_warmupSeconds}s sample={_sampleSeconds}s -> {SummaryPath()}");

        // the viewer never exits by itself on the bench rig, so the summary write is
        // driven by wall-clock sample duration rather than user interaction.
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
        float frameMs = Time.deltaTime * 1000f;
        _sampledElapsed += Time.deltaTime;
        _frameTimes.Add(frameMs);
    }

    private void WriteSamples()
    {
        uint written = FrameTimingManager.GetFrameTimings(_timings, TimingFrameCount);
        float mainThreadAvg = 0f, renderThreadAvg = 0f, gpuAvg = 0f;
        if (written > 0)
        {
            for (int i = 0; i < written; i++)
            {
                mainThreadAvg += _timings[i].cpuPlayerFrameTime;
                renderThreadAvg += _timings[i].cpuRenderThreadFrameTime;
                gpuAvg += _timings[i].gpuFrameTime;
            }

            float inv = 1f / written;
            mainThreadAvg *= inv;
            renderThreadAvg *= inv;
            gpuAvg *= inv;
        }

        string dir = Path.GetDirectoryName(SummaryPath()) ?? ".";
        Directory.CreateDirectory(dir);

        File.WriteAllLines(Path.Combine(dir, CsvName), _frameTimes
            .Select((t, i) => string.Format(CultureInfo.InvariantCulture, "{0},{1:F3}", i, t)));

        List<float> sorted = _frameTimes.OrderBy(t => t).ToList();
        int n = sorted.Count;
        float P(float p) => sorted[Mathf.Clamp((int)(p * (n - 1)), 0, n - 1)];
        float sum = sorted.Sum();
        float avg = n > 0 ? sum / n : 0f;

        long gcNow = GC.GetTotalMemory(false);
        int gcNowCollections = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

        string summary = string.Join(Environment.NewLine,
            $"uma bench summary",
            $"samples={n}",
            $"avg_ms={avg:F3} avg_fps={(avg > 0f ? 1000f / avg : 0f):F1}",
            $"median_ms={P(0.5f):F3}",
            $"p99_ms={P(0.99f):F3} p99.9_ms={P(0.999f):F3}",
            $"min_ms={sorted[0]:F3} max_ms={sorted[^1]:F3}",
            $"main_thread_avg_ms={mainThreadAvg:F3}",
            $"render_thread_avg_ms={renderThreadAvg:F3}",
            $"gpu_avg_ms={gpuAvg:F3}",
            $"gc_alloc_mb={(gcNow - _gcBytesBefore) / 1048576.0:F2}",
            $"gc_collections={gcNowCollections - _gcCollectionsBefore}",
            $"target_fps={Application.targetFrameRate} resolution={Screen.width}x{Screen.height}");
        File.WriteAllText(SummaryPath(), summary);

        Debug.Log("[bench] done\n" + summary);
    }

    private void FinishSampling()
    {
        WriteSamples();
        _enabled = false;
        // signal the harness we are done so it can collect results and free the cpus.
        Debug.Log("[bench] BENCH_DONE");
    }

    private static string OutputDir() => Path.Combine(Application.persistentDataPath, "bench");

    private static string SummaryPath() => Path.Combine(OutputDir(), SummaryName);
}