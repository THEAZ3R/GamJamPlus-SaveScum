// The jam build: a plain non-development Windows x64 player.
// Development flags (god mode, F1-F4 test hops) live behind
// UNITY_EDITOR || DEVELOPMENT_BUILD, so a build made with
// BuildOptions.None has all of them compiled out.
//
// Unity menu:  Build > Build Release (Windows x64, no dev flags)
//              Build > Build Development (Windows x64, G / F1-F4 live)
// command line: Unity.exe -batchmode -quit -projectPath <proj>
//               -executeMethod BuildScript.BuildRelease -logFile build.log
//               (BuildDevelopment for a test build with the cheats in it)
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    const string ReleaseDir = "Builds/Windows";
    const string ReleaseExe = "SAVE_SCUM.exe";
    const string DevDir = "Builds/WindowsDev";        // never the folder the jam gets
    const string DevExe = "SAVE_SCUM_dev.exe";

    [MenuItem("Build/Build Release (Windows x64, no dev flags)")]
    public static void BuildRelease() { Build(false); }

    // Same player, but DEVELOPMENT_BUILD is defined: the G key (god mode),
    // F1-F4 (straight to each boss) and F6 (aim debug) are compiled in.
    // TEST BUILD ONLY - do not upload this one.
    [MenuItem("Build/Build Development (Windows x64, G / F1-F4 live)")]
    public static void BuildDevelopment() { Build(true); }

    static void Build(bool dev)
    {
        var scenes = EditorBuildSettings.scenes;
        var enabled = new System.Collections.Generic.List<string>();
        foreach (var s in scenes)
            if (s.enabled) enabled.Add(s.path);
        if (enabled.Count == 0)
            enabled.Add("Assets/Scenes/SampleScene.unity");

        string dir = dev ? DevDir : ReleaseDir;
        string exe = dev ? DevExe : ReleaseExe;
        Directory.CreateDirectory(dir);

        var opts = new BuildPlayerOptions
        {
            scenes = enabled.ToArray(),
            locationPathName = Path.Combine(dir, exe),
            target = BuildTarget.StandaloneWindows64,
            options = dev ? BuildOptions.Development : BuildOptions.None        // NOT Development: no dev defines
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        bool ok = report.summary.result == BuildResult.Succeeded;

        if (ok)
            Debug.Log("[BuildScript] " + (dev ? "DEV BUILD OK -> " : "RELEASE BUILD OK -> ") +
                      Path.GetFullPath(opts.locationPathName) +
                      "  (" + report.summary.totalSize + " bytes, " + report.summary.totalErrors + " errors)");
        else
            Debug.LogError("[BuildScript] " + (dev ? "DEV" : "RELEASE") + " BUILD FAILED: " + report.summary.result);

        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }
}
