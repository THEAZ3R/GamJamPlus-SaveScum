// The jam build: a plain non-development Windows x64 player.
// Development flags (god mode, F1-F4 test hops) live behind
// UNITY_EDITOR || DEVELOPMENT_BUILD, so a build made with
// BuildOptions.None has all of them compiled out.
//
// Unity menu:  Build > Build Release (Windows x64)
// command line: Unity.exe -batchmode -quit -projectPath <proj>
//               -executeMethod BuildScript.BuildRelease -logFile build.log
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    const string OutDir = "Builds/Windows";
    const string Exe = "SAVE_SCUM.exe";

    [MenuItem("Build/Build Release (Windows x64, no dev flags)")]
    public static void BuildRelease()
    {
        var scenes = EditorBuildSettings.scenes;
        var enabled = new System.Collections.Generic.List<string>();
        foreach (var s in scenes)
            if (s.enabled) enabled.Add(s.path);
        if (enabled.Count == 0)
            enabled.Add("Assets/Scenes/SampleScene.unity");

        Directory.CreateDirectory(OutDir);

        var opts = new BuildPlayerOptions
        {
            scenes = enabled.ToArray(),
            locationPathName = Path.Combine(OutDir, Exe),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None              // NOT Development: no dev defines
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        bool ok = report.summary.result == BuildResult.Succeeded;

        if (ok)
            Debug.Log("[BuildScript] RELEASE BUILD OK -> " + Path.GetFullPath(opts.locationPathName) +
                      "  (" + report.summary.totalSize + " bytes, " + report.summary.totalErrors + " errors)");
        else
            Debug.LogError("[BuildScript] RELEASE BUILD FAILED: " + report.summary.result);

        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }
}
