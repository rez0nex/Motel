using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Jobs.LowLevel.Unsafe;

namespace Motel.Editor
{
    public static class MotelDemoBuild
    {
        [MenuItem("Motel/Build Windows Demo")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before building.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene edits before building.");
            if (SceneManager.GetActiveScene().path == MotelBlockoutSceneCreator.ScenePath)
                MotelLoopChecks.ValidateScene(SceneManager.GetActiveScene());
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Windows Build Support is not installed.");
            string folder = Path.GetFullPath("Builds/MotelDemo");
            Directory.CreateDirectory(folder);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MotelBlockoutSceneCreator.ScenePath },
                locationPathName = Path.Combine(folder, "Motel.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            // Scope resource limits to this build; do not change project or system settings.
            string previousProcessorCount = Environment.GetEnvironmentVariable("DOTNET_PROCESSOR_COUNT");
            string previousBuildThreads = Environment.GetEnvironmentVariable("BEE_BUILD_THREADS");
            int previousWorkerCount = JobsUtility.JobWorkerCount;
            BuildReport report;
            try
            {
                Environment.SetEnvironmentVariable("DOTNET_PROCESSOR_COUNT", "2");
                Environment.SetEnvironmentVariable("BEE_BUILD_THREADS", "2");
                JobsUtility.JobWorkerCount = Math.Min(2, previousWorkerCount);
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_PROCESSOR_COUNT", previousProcessorCount);
                Environment.SetEnvironmentVariable("BEE_BUILD_THREADS", previousBuildThreads);
                JobsUtility.JobWorkerCount = previousWorkerCount;
            }
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows demo build: " + report.summary.result);
            Debug.Log("Windows demo saved: " + options.locationPathName + "; " + report.summary.totalSize + " bytes. Launch verification is separate.");
        }
    }
}
