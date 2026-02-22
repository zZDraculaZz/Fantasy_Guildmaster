#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FantasyGuildmaster.Editor
{
    [InitializeOnLoad]
    internal static class MapControllerDuplicateGuard
    {
        private const string MapControllerPath = "Assets/_Game/Scripts/Map/MapController.cs";

        static MapControllerDuplicateGuard()
        {
            Validate();
        }

        [MenuItem("Tools/Validation/Check MapController CS0111 Duplicates")]
        private static void ValidateFromMenu()
        {
            Validate();
        }

        private static void Validate()
        {
            if (!File.Exists(MapControllerPath))
            {
                return;
            }

            var text = File.ReadAllText(MapControllerPath);
            Check(text, "void EnterGuildHallEvening(");
            Check(text, "void ApplyRestEveningEffect(");
            Check(text, "void OnGuildHallNextDay(");
            Check(text, "void RefreshContractsForNextDay(");
        }

        private static void Check(string text, string signatureToken)
        {
            var count = CountOccurrences(text, signatureToken);
            if (count > 1)
            {
                Debug.LogError($"[MapControllerDuplicateGuard] Duplicate signature '{signatureToken}' count={count}. delete duplicates in MapController.cs");
            }
        }

        private static int CountOccurrences(string text, string token)
        {
            var count = 0;
            var index = 0;
            while (index >= 0)
            {
                index = text.IndexOf(token, index, System.StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                count++;
                index += token.Length;
            }

            return count;
        }
    }
}
#endif
