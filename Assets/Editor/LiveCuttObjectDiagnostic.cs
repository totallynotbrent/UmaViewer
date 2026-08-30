using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Gallop.Live.Cutt;

namespace Gallop.Live.Editor
{
    /// <summary>
    /// Diagnostic tool to enumerate Cutt object data and find microphone/prop references.
    /// </summary>
    public static class LiveCuttObjectDiagnostic
    {
        // Menu registration is provided by LiveAssetDiagnostics to avoid duplicate items.
        public static void LogCuttObjectData()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Cutt Object Data Diagnostic ===");
            sb.AppendLine($"Timestamp: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // Find LiveTimelineControl in scene
            var timelineControl = Object.FindObjectOfType<LiveTimelineControl>(true);
            if (timelineControl == null)
            {
                sb.AppendLine("ERROR: LiveTimelineControl not found in scene.");
                sb.AppendLine("Make sure a live is loaded.");
                WriteReport(sb.ToString(), "cutt-object-data");
                return;
            }

            sb.AppendLine($"LiveTimelineControl found: {timelineControl.name}");
            sb.AppendLine();

            // Access worksheet data
            var data = timelineControl.data;
            if (data == null || data.worksheetList == null || data.worksheetList.Count == 0)
            {
                sb.AppendLine("ERROR: No worksheet data available.");
                WriteReport(sb.ToString(), "cutt-object-data");
                return;
            }

            sb.AppendLine($"Worksheet count: {data.worksheetList.Count}");
            sb.AppendLine();

            // Enumerate all worksheets
            for (int wsIndex = 0; wsIndex < data.worksheetList.Count; wsIndex++)
            {
                var ws = data.worksheetList[wsIndex];
                if (ws == null) continue;

                sb.AppendLine($"--- Worksheet {wsIndex} ---");
                sb.AppendLine($"  SheetType: {ws.SheetType}");

                // Object list
                if (ws.objectList != null)
                {
                    sb.AppendLine($"  Object count: {ws.objectList.Count}");
                    for (int i = 0; i < ws.objectList.Count; i++)
                    {
                        var obj = ws.objectList[i];
                        if (obj == null) continue;
                        sb.AppendLine($"    [{i}] name={obj.name}, enablePosition={obj.enablePosition}, enableRotate={obj.enableRotate}");
                    }
                }

                // Transform list
                if (ws.transformList != null)
                {
                    sb.AppendLine($"  Transform count: {ws.transformList.Count}");
                    for (int i = 0; i < ws.transformList.Count; i++)
                    {
                        var t = ws.transformList[i];
                        if (t == null) continue;
                        sb.AppendLine($"    [{i}] name={t.name}");
                    }
                }

                // Search for prop/mic/instrument keywords
                sb.AppendLine("  --- Keyword Search ---");
                string[] keywords = { "mic", "microphone", "instrument", "hand", "attach", "item", "prop_", "guitar", "piano", "drum" };
                foreach (var keyword in keywords)
                {
                    int count = 0;
                    if (ws.objectList != null)
                    {
                        foreach (var obj in ws.objectList)
                        {
                            if (obj != null && obj.name != null &&
                                obj.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                count++;
                                sb.AppendLine($"    MATCH: {keyword} -> {obj.name}");
                            }
                        }
                    }
                    if (ws.transformList != null)
                    {
                        foreach (var t in ws.transformList)
                        {
                            if (t != null && t.name != null &&
                                t.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                count++;
                                sb.AppendLine($"    MATCH: {keyword} -> {t.name}");
                            }
                        }
                    }
                    if (count == 0)
                    {
                        sb.AppendLine($"    {keyword}: no matches");
                    }
                }
                sb.AppendLine();
            }

            // Search loaded bundles for prop/mic assets
            sb.AppendLine("--- Loaded Bundle Asset Search ---");
            string[] propKeywords = { "mic", "microphone", "instrument", "prop_", "hand_item", "attach" };
            int foundCount = 0;

            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (bundle == null) continue;

                string[] names;
                try { names = bundle.GetAllAssetNames(); }
                catch { continue; }

                if (names == null) continue;

                foreach (var name in names)
                {
                    foreach (var keyword in propKeywords)
                    {
                        if (name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            sb.AppendLine($"  BUNDLE ASSET: {name}");
                            foundCount++;
                        }
                    }
                }
            }

            if (foundCount == 0)
            {
                sb.AppendLine("  No prop/mic assets found in loaded bundles.");
            }

            sb.AppendLine();
            sb.AppendLine("=== End Diagnostic ===");

            WriteReport(sb.ToString(), "cutt-object-data");
        }

        private static void WriteReport(string content, string prefix)
        {
            string dir = Path.Combine(Application.dataPath, "..", "research");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string filename = $"{prefix}-{System.DateTime.Now:yyyyMMdd-HHmmss}.txt";
            string path = Path.Combine(dir, filename);

            File.WriteAllText(path, content, Encoding.UTF8);
            Debug.Log($"[LiveCuttObjectDiagnostic] Wrote report: {path}");
        }
    }
}
