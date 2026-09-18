using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// lightweight text profiler: brackets named sections per frame, accumulates
    /// wall time and managed allocation, and dumps a ranked report for the bench
    /// summary so optimization work has real numbers to chase.
    /// </summary>
    public static class SectionProfiler
    {
        private sealed class Stat
        {
            public double TotalMs;
            public long AllocBytes;
            public int Calls;
            public double MaxMs;
        }

        private static readonly Dictionary<string, Stat> _stats = new();
        private static readonly Stack<(string name, long startTicks, long startAlloc)> _stack = new();
        private static bool _enabled;

        // gate controlled by the bench window; sampling off costs one bool check.
        public static void SetEnabled(bool value)
        {
            _enabled = value;
        }

        public static bool IsEnabled => _enabled;

        public static void Reset()
        {
            _stats.Clear();
        }

        public static void Begin(string name)
        {
            if (!_enabled)
                return;
            _stack.Push((name, Stopwatch.GetTimestamp(), GC.GetTotalMemory(false)));
        }

        public static void End()
        {
            if (!_enabled || _stack.Count == 0)
                return;

            var (name, startTicks, startAlloc) = _stack.Pop();
            double ms = (Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency;
            long alloc = GC.GetTotalMemory(false) - startAlloc;

            if (!_stats.TryGetValue(name, out Stat stat))
            {
                stat = new Stat();
                _stats[name] = stat;
            }

            stat.TotalMs += ms;
            stat.AllocBytes += alloc > 0 ? alloc : 0;
            stat.Calls++;
            if (ms > stat.MaxMs)
                stat.MaxMs = ms;
        }

        // ranked report: sections sorted by total time, with per-call ms and
        // managed MB allocated across the window.
        public static string Dump(int maxRows)
        {
            if (_stats.Count == 0)
                return "sections: none recorded";

            var rows = new List<(string name, Stat s)>(_stats.Count);
            foreach (var kv in _stats)
                rows.Add((kv.Key, kv.Value));
            rows.Sort((a, b) => b.Item2.TotalMs.CompareTo(a.Item2.TotalMs));

            var sb = new StringBuilder();
            sb.AppendLine("sections (by total ms):");
            for (int i = 0; i < rows.Count && i < maxRows; i++)
            {
                var (name, s) = rows[i];
                sb.AppendLine(
                    $"  {name}: total_ms={s.TotalMs:F1} avg_ms={(s.Calls > 0 ? s.TotalMs / s.Calls : 0):F3} " +
                    $"max_ms={s.MaxMs:F2} calls={s.Calls} alloc_mb={s.AllocBytes / 1048576.0:F1}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
