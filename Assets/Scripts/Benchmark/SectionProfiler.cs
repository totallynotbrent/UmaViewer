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

        // whole-frame clock so the summary can report attributed vs total frame
        // time; the gap names the cost of everything not inside a section.
        private static double _frameTotalMs;
        private static int _frameCount;

        public static void AccountFrame(double frameMs)
        {
            _frameTotalMs += frameMs;
            _frameCount++;
        }

        public static string GapReport()
        {
            if (_frameCount == 0)
                return "";
            double attributed = 0;
            foreach (var kv in _stats)
            {
                // nested sections overlap; top-level sum only would need depth
                // tracking, so cap the gap at 0 when attribution exceeds reality.
                attributed += kv.Value.TotalMs;
            }
            double total = _frameTotalMs;
            double gap = total - attributed;
            return $"attribution: frame_total_ms={(total / _frameCount):F1} attributed_ms={(attributed / _frameCount):F1} unattributed_ms={(gap > 0 ? gap : 0) / _frameCount:F1} frames={_frameCount}";
        }

        public static void ResetAccounting()
        {
            _frameTotalMs = 0;
            _frameCount = 0;
        }

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

        // walks the live player loop and brackets every leaf subsystem by its
        // own name across all phase groups; the whole loop is written back once.
        public static void WrapUpdateGroupSubsystems()
        {
            var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                var group = loop.subSystemList[i];
                if (group.type == null)
                    continue;

                string prefix = "engine." + group.type.Name;
                if (group.subSystemList != null && group.subSystemList.Length > 0)
                {
                    group.subSystemList = WrapSubsystemList(prefix, group.subSystemList);
                }
                else if (group.updateDelegate != null)
                {
                    group.updateDelegate = WrapUpdateDelegate(prefix, group.updateDelegate);
                }
                loop.subSystemList[i] = group;
            }
            UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
        }

        private static UnityEngine.LowLevel.PlayerLoopSystem[] WrapSubsystemList(
            string prefix, UnityEngine.LowLevel.PlayerLoopSystem[] list)
        {
            if (list == null)
                return list;
            for (int i = 0; i < list.Length; i++)
            {
                var sub = list[i];
                if (sub.type == null)
                    continue;
                string section = prefix + "." + sub.type.Name;
                if (sub.subSystemList != null && sub.subSystemList.Length > 0)
                {
                    sub.subSystemList = WrapSubsystemList(section, sub.subSystemList);
                }
                else if (sub.updateDelegate != null)
                {
                    sub.updateDelegate = WrapUpdateDelegate(section, sub.updateDelegate);
                }
                list[i] = sub;
            }
            return list;
        }

        private static UnityEngine.LowLevel.PlayerLoopSystem.UpdateFunction WrapUpdateDelegate(
            string name, UnityEngine.LowLevel.PlayerLoopSystem.UpdateFunction inner)
        {
            return delegate
            {
                // the frame clock brackets whole phases too, so the gap line stays honest.
                if (!_enabled)
                {
                    inner?.Invoke();
                    return;
                }
                Begin(name);
                try { inner?.Invoke(); }
                finally { End(); }
            };
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
