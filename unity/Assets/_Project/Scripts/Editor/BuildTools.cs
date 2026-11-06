#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Batch-mode entry points for agents and CI.
/// Usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod BuildTools.BuildWebGL -logFile -
/// </summary>
public static class BuildTools
{
    static string[] EnabledScenes() =>
        EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

    /// Opening the project in batch mode already compiles all scripts; this just confirms it.
    public static void CompileCheck()
    {
        if (EditorUtility.scriptCompilationFailed)
        {
            Debug.LogError("[BuildTools] Script compilation failed.");
            EditorApplication.Exit(1);
        }
        Debug.Log("[BuildTools] Compile OK.");
        EditorApplication.Exit(0);
    }

    [MenuItem("Tools/Build/WebGL")]
    public static void BuildWebGL() => Build(BuildTarget.WebGL, "../Builds/WebGL");

    [MenuItem("Tools/Build/Windows")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, $"../Builds/Windows/{PlayerSettings.productName}.exe");

    static void Build(BuildTarget target, string output)
    {
        var scenes = EnabledScenes();
        if (scenes.Length == 0) throw new Exception("[BuildTools] No enabled scenes in Build Settings.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = target,
            options = BuildOptions.None,
        });
        var s = report.summary;
        Debug.Log($"[BuildTools] {target}: {s.result}, {s.totalErrors} errors, {s.totalSize} bytes -> {output}");
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }
}
#endif
