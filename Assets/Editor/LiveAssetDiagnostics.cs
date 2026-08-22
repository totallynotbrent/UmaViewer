#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class LiveAssetDiagnostics
{
    [MenuItem("UmaViewer/Live/Log Loaded Bundle Assets")]
    private static void LogLoadedBundleAssets()
    {
        int bundles = 0;
        int assets = 0;
        var report = new StringBuilder();
        report.AppendLine("UmaViewer Live Asset Diagnostics");
        report.AppendLine($"Generated: {DateTime.Now:O}");
        report.AppendLine();
        foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (bundle == null) continue;
            bundles++;
            string[] names;
            try { names = bundle.GetAllAssetNames(); }
            catch (Exception e) { report.AppendLine($"ERROR {bundle.name}: {e.Message}"); continue; }
            foreach (var name in names)
            {
                string lower = name.ToLowerInvariant();
                if (lower.Contains("live") || lower.Contains("projector") || lower.Contains("confetti") ||
                    lower.Contains("shader") || lower.Contains("mic") || lower.Contains("props") || lower.Contains("cutt"))
                {
                    report.AppendLine($"{bundle.name}: {name}");
                    assets++;
                }
            }
        }
        report.AppendLine();
        report.AppendLine($"Summary: scanned bundles={bundles}, matching assets={assets}");
        string path = WriteReport("loaded-bundle-assets", report);
        Debug.Log($"[LiveAssetDiagnostics] Wrote report: {path}");
    }

    [MenuItem("UmaViewer/Live/Log Selected Missing Script IDs")]
    private static void LogSelectedMissingScriptIds()
    {
        var report = new StringBuilder();
        report.AppendLine("UmaViewer Missing Script Diagnostics");
        report.AppendLine($"Generated: {DateTime.Now:O}");
        report.AppendLine();
        foreach (var selected in Selection.gameObjects)
        {
            if (selected == null) continue;
            AppendMissingScriptDetails(selected.transform, report);
        }
        string path = WriteReport("missing-script-ids", report);
        Debug.Log($"[LiveAssetDiagnostics] Wrote report: {path}");
    }

    private static string WriteReport(string name, StringBuilder report)
    {
        string directory = Path.Combine(Application.dataPath, "..", "research");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"live-diagnostics-{name}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(path, report.ToString(), Encoding.UTF8);
        return path;
    }

    private static string GetHierarchyPath(Transform current)
    {
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }
        return path;
    }

    private static int CountMissingScripts(GameObject root)
    {
        int count = 0;
        Component[] components = root.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] == null) count++;

        foreach (Transform child in root.transform)
            count += CountMissingScripts(child.gameObject);
        return count;
    }

    private static void AppendMissingScriptDetails(Transform current, StringBuilder report)
    {
        int localCount = 0;
        Component[] components = current.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] == null) localCount++;

        if (localCount > 0)
            report.AppendLine($"{GetHierarchyPath(current)}: missingScripts={localCount}");

        foreach (Transform child in current)
            AppendMissingScriptDetails(child, report);
    }

    [MenuItem("UmaViewer/Live/Log Cutt Object Data")]
    private static void LogCuttObjectData()
    {
        var report = new StringBuilder();
        report.AppendLine("UmaViewer Cutt Object Data Diagnostics");
        report.AppendLine($"Generated: {DateTime.Now:O}");
        report.AppendLine();

        var director = GameObject.FindObjectOfType<Gallop.Live.Director>();
        if (director == null || director._liveTimelineControl == null)
        {
            report.AppendLine("No live timeline control found. Load a live first.");
            string path1 = WriteReport("cutt-object-data", report);
            Debug.Log($"[LiveAssetDiagnostics] Wrote report: {path1}");
            return;
        }

        var ctl = director._liveTimelineControl;
        var data = ctl.data;
        if (data == null)
        {
            report.AppendLine("Timeline data is null.");
            string path2 = WriteReport("cutt-object-data", report);
            Debug.Log($"[LiveAssetDiagnostics] Wrote report: {path2}");
            return;
        }

        report.AppendLine($"Worksheet count: {data.worksheetList?.Count ?? 0}");

        // Log props settings
        if (data.propsSettings != null && data.propsSettings.propsDataGroup != null)
        {
            report.AppendLine($"Props data groups: {data.propsSettings.propsDataGroupCount}");
            foreach (var propGroup in data.propsSettings.propsDataGroup)
            {
                if (propGroup == null) continue;
                report.AppendLine($"  Props: {propGroup.propsName ?? "<unnamed>"} isChara={propGroup.isCharaProps} joints={propGroup.attachJointNameCount}");
                if (propGroup.attachJointNames != null)
                {
                    foreach (var joint in propGroup.attachJointNames)
                    {
                        if (!string.IsNullOrEmpty(joint))
                            report.AppendLine($"    Joint: {joint}");
                    }
                }
            }
        }
        else
        {
            report.AppendLine("No props settings found.");
        }
        report.AppendLine();

        int objectIndex = 0;
        foreach (var sheet in data.worksheetList)
        {
            if (sheet == null) continue;
            report.AppendLine($"--- Worksheet {objectIndex} ---");

            // Log object data
            if (sheet.objectList != null)
            {
                foreach (var objData in sheet.objectList)
                {
                    if (objData == null) continue;
                    report.AppendLine($"  Object: {objData.name ?? "<unnamed>"}");
                    if (objData.keys != null)
                    {
                        foreach (var key in objData.keys)
                        {
                            if (key == null) continue;
                            report.AppendLine($"    Key frame={key.frame}");
                        }
                    }
                }
            }

            // Log particle data
            if (sheet.transformList != null)
            {
                foreach (var pData in sheet.transformList)
                {
                    if (pData == null) continue;
                    report.AppendLine($"  Particle: {pData.name ?? "<unnamed>"}");
                }
            }

            // Log props data
            if (sheet.objectList != null)
            {
                foreach (var objData in sheet.objectList)
                {
                    if (objData == null) continue;
                    if (objData.name != null && objData.name.IndexOf("prop", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        report.AppendLine($"  Props object: {objData.name}");
                    }
                }
            }

            objectIndex++;
        }

        // Search all loaded bundles for live-related assets
        report.AppendLine();
        report.AppendLine("=== Searching loaded bundles for live-related assets ===");

        // Define search categories
        string[] micKeywords = { "mic", "microphone", "instrument", "hand", "attach", "item", "prop_" };
        string[] shaderKeywords = { "shader", ".shader" };
        string[] particleKeywords = { "particle", "effect", "emission", "flare" };
        string[] stageKeywords = { "stage", "light", "blink", "laser", "cyalume", "confetti", "projector" };

        int micCount = 0, shaderCount = 0, particleCount = 0, stageCount = 0;

        foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (bundle == null) continue;
            string[] names;
            try { names = bundle.GetAllAssetNames(); }
            catch { continue; }
            foreach (var name in names)
            {
                string lower = name.ToLowerInvariant();

                // Check mic keywords
                foreach (var kw in micKeywords)
                {
                    if (lower.Contains(kw))
                    {
                        report.AppendLine($"  [MIC] {name}");
                        micCount++;
                        break;
                    }
                }

                // Check shader keywords
                foreach (var kw in shaderKeywords)
                {
                    if (lower.Contains(kw))
                    {
                        report.AppendLine($"  [SHADER] {name}");
                        shaderCount++;
                        break;
                    }
                }

                // Check particle keywords
                foreach (var kw in particleKeywords)
                {
                    if (lower.Contains(kw))
                    {
                        report.AppendLine($"  [PARTICLE] {name}");
                        particleCount++;
                        break;
                    }
                }

                // Check stage keywords
                foreach (var kw in stageKeywords)
                {
                    if (lower.Contains(kw))
                    {
                        report.AppendLine($"  [STAGE] {name}");
                        stageCount++;
                        break;
                    }
                }
            }
        }

        report.AppendLine();
        report.AppendLine($"Summary: mic={micCount}, shader={shaderCount}, particle={particleCount}, stage={stageCount}");

        string path = WriteReport("cutt-object-data", report);
        Debug.Log($"[LiveAssetDiagnostics] Wrote report: {path}");
    }
}
#endif
