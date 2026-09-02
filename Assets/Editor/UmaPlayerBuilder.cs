using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UmaBuildTools
{
    public static class UmaPlayerBuilder
    {
        private const string OutputDir = "Builds";

        public static void BuildLinux()
        {
            Build(BuildTarget.StandaloneLinux64, "UmaViewer");
        }

        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "UmaViewer.exe");
        }

        private static void Build(BuildTarget target, string fileName)
        {
            var scenes = new[]
            {
                "Assets/Scenes/Version2.unity",
                "Assets/Scenes/LiveScene.unity"
            };

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = $"{OutputDir}/{fileName}",
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[UmaPlayerBuilder] OK {target} {report.summary.outputPath} ({report.summary.totalSize} bytes)");
                return;
            }

            Debug.LogError($"[UmaPlayerBuilder] FAILED {target} result={report.summary.result}");
            foreach (BuildStep step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        Debug.LogError($"{msg.content}");
                }
            }
        }
    }
}