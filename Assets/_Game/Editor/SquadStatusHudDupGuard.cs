#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SquadStatusHudDupGuard
{
    private const string TargetFile = "Assets/_Game/Scripts/UI/SquadStatusHUD.cs";

    static SquadStatusHudDupGuard()
    {
        if (!File.Exists(TargetFile))
        {
            return;
        }

        var source = File.ReadAllText(TargetFile);
        var ensureRowCount = Regex.Matches(source, @"^[\t ]*(public|private|protected|internal)[^\n]*\bEnsureRow\s*\(", RegexOptions.Multiline).Count;
        var buildLegacyTextCount = Regex.Matches(source, @"^[\t ]*(public|private|protected|internal)[^\n]*\bBuildLegacyText\s*\(", RegexOptions.Multiline).Count;

        if (ensureRowCount > 1 || buildLegacyTextCount > 1)
        {
            Debug.LogError("[DupGuard] SquadStatusHUD contains duplicate helper methods; keep only one EnsureRow and one BuildLegacyText.");
        }
    }
}
#endif
