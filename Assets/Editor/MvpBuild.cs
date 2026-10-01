using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MvpBuild
{
    const string OutputDir = "Builds/OneWeapon";

    [MenuItem("Tools/MVP/Build Windows")]
    public static void BuildWindows()
    {
        PlayerSettings.productName = "One Weapon";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;

        string[] scenes = Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutputDir, "OneWeapon.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log("[MVP] Build " + summary.result + ": " + summary.outputPath + " (" + summary.totalSize / 1048576 + " MB, " + summary.totalErrors + " errors)");

        if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
