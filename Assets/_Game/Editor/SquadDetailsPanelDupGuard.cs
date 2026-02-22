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
            ValidateSingleSerializedPaddingField(code, "paddingLeft");
            ValidateSingleSerializedPaddingField(code, "paddingRight");
            ValidateSingleSerializedPaddingField(code, "paddingTop");
            ValidateSingleSerializedPaddingField(code, "paddingBottom");
        }

        private static void ValidateSingleSerializedPaddingField(string code, string fieldName)
        {
            var pattern = $@"(?:\[SerializeField\]\s*)?private\s+float\s+{fieldName}\b";
            var count = Regex.Matches(code, pattern).Count;
            if (count > 1)
            {
                Debug.LogError("[DupGuard] SquadDetailsPanel has duplicated padding fields. Keep only one set.");
            }
        }
    }
}
