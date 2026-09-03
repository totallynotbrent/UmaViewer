using System;
using Unity.Burst;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace UnityBuilderAction
{
    public static class BuildScript
    {
        public static void BuildWindowsIL2CPP()
        {
            // IL2CPP Windows-container builds fail in Burst AOT ("Burst compiler failed
            // running ... AsyncPluginsFromLinker") because Burst's managed BCL runner
            // can't be located inside the game-ci Windows container. Disable Burst AOT
            // compilation for the player build; plain IL2CPP still runs fine.
            BurstCompiler.Options.EnableBurstCompilation = false;
            BurstCompiler.Options.EnableBurstSafetyChecks = false;
            BurstCompiler.Options.EnableBurstDebug = false;

            var options = new BuildPlayerOptions
            {
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                locationPathName = "build/StandaloneWindows64/UmaViewer.exe",
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Build failed: " + report.summary.result);
        }
    }
}