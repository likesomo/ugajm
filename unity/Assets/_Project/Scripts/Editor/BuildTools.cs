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

        // Refuse to build a player that cannot work: wrong build list, missing scene roots/references, or a missing
        // shared-asset junction (font/sfx references empty -> a build without text that still passes smoke).
        int sceneErrors = SevenSteps.EditorTools.SceneFlowValidator.ValidateAll(out string sceneReport);
        Debug.Log(sceneReport);
        if (sceneErrors > 0) throw new Exception($"[BuildTools] scene-validate found {sceneErrors} error(s); not building");

        // Build on the active target. URP collects the pipeline assets for shader stripping from the active
        // target; building WebGL while Standalone is active strips the wrong variants (UI drawn, no 3D board).
        if (EditorUserBuildSettings.activeBuildTarget != target)
        {
            Debug.Log($"[BuildTools] switching active build target {EditorUserBuildSettings.activeBuildTarget} -> {target}");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new Exception($"[BuildTools] could not switch the active build target to {target}");
        }

        // Players report Application.version as "<bundleVersion>+<commit>[-dirty]" (telemetry `build` field).
        // The commit is stamped only for the build and the project setting is put back afterwards.
        string baseVersion = PlayerSettings.bundleVersion;
        PlayerSettings.bundleVersion = BuildVersion(baseVersion);
        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            });
        }
        finally
        {
            Debug.Log($"[BuildTools] version {PlayerSettings.bundleVersion}");
            PlayerSettings.bundleVersion = baseVersion;
            AssetDatabase.SaveAssets();
        }
        var s = report.summary;
        Debug.Log($"[BuildTools] {target}: {s.result}, {s.totalErrors} errors, {s.totalSize} bytes -> {output}");
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }

    /// "0.1.0" -> "0.1.0+e6e39ca" (or "+e6e39ca-dirty" with uncommitted tracked changes, "+nogit" without git).
    public static string BuildVersion(string baseVersion)
    {
        string hash = Git("rev-parse --short=7 HEAD");
        if (string.IsNullOrEmpty(hash)) return $"{baseVersion}+nogit";
        string dirty = string.IsNullOrEmpty(Git("status --porcelain --untracked-files=no")) ? "" : "-dirty";
        return $"{baseVersion}+{hash}{dirty}";
    }

    static string Git(string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", args)
            {
                WorkingDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            string output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(5000);
            return p.ExitCode == 0 ? output : "";
        }
        catch (Exception)
        {
            return "";
        }
    }
}
#endif
