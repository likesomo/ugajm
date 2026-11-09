using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Copies the "게임 크레딧 화면·itch.io용 문구" code block of design/art/CREDITS.md (owner: art) into
    /// Assets/_Project/UI/Credits.txt, the TextAsset the Title scene's credits panel shows. Runs before every player build,
    /// so a credit added by art reaches the game without touching code. Owner: gameplay/UI.
    /// Menu: Seven Steps > Gameplay > Export Credits. Batch: -executeMethod SevenSteps.EditorTools.CreditsExport.BatchExport
    /// </summary>
    public sealed class CreditsExport : IPreprocessBuildWithReport
    {
        public const string OutPath = "Assets/_Project/UI/Credits.txt";
        const string SourceRel = "../design/art/CREDITS.md";
        const string Heading = "## 게임 크레딧 화면";

        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Export();

        [MenuItem("Seven Steps/Gameplay/Export Credits", priority = 31)]
        public static void Export()
        {
            string src = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), SourceRel));
            string text = File.Exists(src) ? Extract(File.ReadAllText(src, Encoding.UTF8)) : null;
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning($"[SevenSteps] Credits: no code block under '{Heading}' in {src}; keeping {OutPath}");
                return;
            }
            string full = Path.Combine(Path.GetDirectoryName(Application.dataPath), OutPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            // Same text, whatever the line endings: a Windows checkout has CRLF (git autocrlf) and rewriting it as LF left a
            // diff after every build, so the build version read "-dirty" (lead 2026-11-10).
            if (File.Exists(full) && Normalize(File.ReadAllText(full, Encoding.UTF8)) == Normalize(text)) return;
            File.WriteAllText(full, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(OutPath);
            Debug.Log($"[SevenSteps] Credits exported to {OutPath}");
        }

        /// Text without line-ending or BOM differences (only content changes count).
        internal static string Normalize(string s) => s.TrimStart('﻿').Replace("\r\n", "\n");

        public static void BatchExport()
        {
            try { Export(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// The first ``` block after the heading, without the fences.
        static string Extract(string md)
        {
            int h = md.IndexOf(Heading, StringComparison.Ordinal);
            if (h < 0) return null;
            int open = md.IndexOf("```", h, StringComparison.Ordinal);
            if (open < 0) return null;
            int lineEnd = md.IndexOf('\n', open);
            int close = lineEnd < 0 ? -1 : md.IndexOf("```", lineEnd, StringComparison.Ordinal);
            if (close < 0) return null;
            return md.Substring(lineEnd + 1, close - lineEnd - 1).Replace("\r\n", "\n").Trim() + "\n";
        }
    }
}
