#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FantasyGuildmaster.Editor
{
    [InitializeOnLoad]
    internal static class MapControllerDupGuard
    {
        private const string MapControllerPath = "Assets/_Game/Scripts/Map/MapController.cs";

        private static readonly string[] ForbiddenMethodNames =
        {
            "BuildMissionReport",
            "TryShowNextMissionReport",
            "OnMissionReportContinue",
            "OnEndDayButtonClicked",
            "EnsureEndDayButton",
            "EnsureEndDayHeaderLayout",
            "IsEndDayBlocked",
            "IsEndDayWarning",
            "IsContractAssigned",
            "UpdateEndDayUiState",
            "EnsureEndDayConfirmPanel",
            "ConfigureEndDayConfirmButtonVisuals",
            "ShowEndDayConfirm",
            "HideEndDayConfirm",
            "OnEndDayConfirmYes",
            "TryAdvanceDayFlow",
            "CanEnterEveningNow",
            "LogDayFlow",
            "EnterGuildHallEvening",
            "ApplyRestEveningEffect",
            "OnGuildHallNextDay",
            "RefreshContractsForNextDay"
        };

        static MapControllerDupGuard()
        {
            Validate();
        }

        [MenuItem("Tools/Validation/Check MapController DayFlow Dup Guard")]
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
            for (var i = 0; i < ForbiddenMethodNames.Length; i++)
            {
                var methodName = ForbiddenMethodNames[i];
                if (HasDefinition(text, methodName))
                {
                    Debug.LogError("[DupGuard] Do not define DayFlow methods in MapController.cs; they must live in MapController.DayFlow.cs");
                    return;
                }
            }
        }

        private static bool HasDefinition(string text, string methodName)
        {
            return text.Contains($"private void {methodName}(")
                || text.Contains($"private bool {methodName}(")
                || text.Contains($"private MissionReportData {methodName}(");
        }
    }
}
#endif
