using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace FantasyGuildmaster.Editor
{
    [InitializeOnLoad]
    public static class SquadDetailsPanelDupGuard
    {
        private const string RelativePath = "Assets/_Game/Scripts/UI/SquadDetailsPanel.cs";

        static SquadDetailsPanelDupGuard()
        {
            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), RelativePath);
            if (!File.Exists(absolutePath))
            {
                return;
            }

            var code = File.ReadAllText(absolutePath);
            ValidateSingleField(code, "paddingLeft", "float", "[DupGuard] Duplicate padding fields detected in SquadDetailsPanel. Keep only one set.");
            ValidateSingleField(code, "paddingRight", "float", "[DupGuard] Duplicate padding fields detected in SquadDetailsPanel. Keep only one set.");
            ValidateSingleField(code, "paddingTop", "float", "[DupGuard] Duplicate padding fields detected in SquadDetailsPanel. Keep only one set.");
            ValidateSingleField(code, "paddingBottom", "float", "[DupGuard] Duplicate padding fields detected in SquadDetailsPanel. Keep only one set.");
            ValidateSingleField(code, "forceLegacyText", "bool", "[DupGuard] Duplicate forceLegacyText fields detected in SquadDetailsPanel. Keep only one set.");
        }

        private static void ValidateSingleField(string code, string fieldName, string typeName, string errorMessage)
        {
            var pattern = $@"(?:\[SerializeField\]\s*)?private\s+{typeName}\s+{fieldName}\b";
            var count = Regex.Matches(code, pattern).Count;
            if (count > 1)
            {
                Debug.LogError(errorMessage);
            }
        }
    }
}
