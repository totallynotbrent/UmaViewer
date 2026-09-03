using Unity.Burst;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace UnityBuilderAction
{
    public class BuildSettingsPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // IL2CPP Windows-container builds crash in Burst AOT ("Burst compiler failed
            // running ... AsyncPluginsFromLinker") because Burst's managed BCL runner isn't
            // locatable inside the game-ci Windows container. Disable Burst AOT compilation
            // for the player build; plain IL2CPP still runs fine.
            BurstCompiler.Options.EnableBurstCompilation = false;
            BurstCompiler.Options.EnableBurstSafetyChecks = false;
            BurstCompiler.Options.EnableBurstDebug = false;
        }
    }
}