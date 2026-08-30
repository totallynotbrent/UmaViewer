using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Gallop.Live.Editor
{
    /// <summary>
    /// Logs Unity console warnings/errors to a file, deduplicating repeated messages.
    /// This makes it easy to see unique issues without scrolling through 999+ duplicates.
    /// </summary>
    public static class LiveWarningLogger
    {
        private static Dictionary<string, int> _warningCounts = new Dictionary<string, int>();
        private static StringBuilder _report = new StringBuilder();
        private static bool _isCollecting = false;

        [MenuItem("UmaViewer/Live/Start Collecting Warnings")]
        public static void StartCollecting()
        {
            _warningCounts.Clear();
            _report.Clear();
            _isCollecting = true;
            Debug.Log("[LiveWarningLogger] Started collecting warnings. Play the game, then click 'Log Deduplicated Warnings'.");
        }

        [MenuItem("UmaViewer/Live/Log Deduplicated Warnings")]
        public static void LogDeduplicatedWarnings()
        {
            if (!_isCollecting && _warningCounts.Count == 0)
            {
                Debug.Log("[LiveWarningLogger] No warnings collected. Click 'Start Collecting Warnings' first, then play the game.");
                return;
            }

            _isCollecting = false;

            var sb = new StringBuilder();
            sb.AppendLine("=== Deduplicated Unity Warnings/Errors ===");
            sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total unique messages: {_warningCounts.Count}");
            sb.AppendLine();

            // Sort by count (most frequent first)
            var sorted = new List<KeyValuePair<string, int>>(_warningCounts);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

            int totalWarnings = 0;
            foreach (var kv in sorted)
            {
                sb.AppendLine($"[{kv.Value}x] {kv.Key}");
                totalWarnings += kv.Value;
            }

            sb.AppendLine();
            sb.AppendLine($"Total messages (with duplicates): {totalWarnings}");

            string dir = Path.Combine(Application.dataPath, "..", "research");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string filename = $"warnings-dedup-{System.DateTime.Now:yyyyMMdd-HHmmss}.txt";
            string path = Path.Combine(dir, filename);

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[LiveWarningLogger] Wrote {sorted.Count} unique warnings to: {path}");
        }

        [MenuItem("UmaViewer/Live/Clear Warning Log")]
        public static void ClearWarningLog()
        {
            _warningCounts.Clear();
            _report.Clear();
            _isCollecting = false;
            Debug.Log("[LiveWarningLogger] Cleared warning log.");
        }

        // Call this from elsewhere to record a warning
        public static void RecordWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // Normalize the message - remove timestamps, instance IDs, etc.
            string normalized = NormalizeMessage(message);

            if (_warningCounts.ContainsKey(normalized))
                _warningCounts[normalized]++;
            else
                _warningCounts[normalized] = 1;
        }

        private static string NormalizeMessage(string message)
        {
            // Remove common variable parts
            string result = message;

            // Remove GUIDs
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\{fileID: \d+\}", "{fileID: X}");

            // Remove hex addresses
            result = System.Text.RegularExpressions.Regex.Replace(result, @"0x[0-9a-fA-F]+", "0xXXX");

            // Remove instance IDs
            result = System.Text.RegularExpressions.Regex.Replace(result, @"InstanceID: \d+", "InstanceID: X");

            // Remove file paths that vary
            result = System.Text.RegularExpressions.Regex.Replace(result, @"Assets/[^\s]+\.cs\(\d+,\d+\)", "Assets/XXX.cs(L,C)");

            return result.Trim();
        }
    }

    /// <summary>
    /// Editor callback to capture warnings automatically.
    /// </summary>
    [InitializeOnLoad]
    public class WarningCaptureCallback
    {
        static WarningCaptureCallback()
        {
            Application.logMessageReceived += OnLogMessageReceived;
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning || type == LogType.Error)
            {
                LiveWarningLogger.RecordWarning(condition);
            }
        }
    }
}
