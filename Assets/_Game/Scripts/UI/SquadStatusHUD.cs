using System.Collections;
using System.Text;
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace FantasyGuildmaster.UI
{
    public class SquadStatusHUD : MonoBehaviour, IPointerClickHandler
    {
        [Header("Optional UI")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;

        [Header("Required UI (auto-created if missing)")]
        [SerializeField] private TMP_Text bodyText;

        [Header("Behavior")]
        [SerializeField] private float refreshSeconds = 1f;

        private MapController _map;
        private GameState _gameState;
        private Coroutine _tick;
        private bool _nullSafeLogPrinted;

        private void Awake() => EnsureBodyText();

        private void OnEnable() => StartCoroutine(DelayedBindAndStart());

        private void OnDisable()
        {
            if (_gameState != null) _gameState.OnGoldChanged -= OnGoldChanged;
            if (_tick != null) StopCoroutine(_tick);
            _tick = null;
        }

        private IEnumerator DelayedBindAndStart()
        {
            yield return null;
            BindIfNeeded();
            RefreshNow();
            if (_tick == null) _tick = StartCoroutine(TickRoutine());
        }

        private IEnumerator TickRoutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.2f, refreshSeconds));
            while (enabled && gameObject.activeInHierarchy)
            {
                RefreshNow();
                yield return wait;
            }
        }

        public void BindGameState(MapController mc)
        {
            _map = mc;
            RefreshNow();
        }

        public void BindGameState(GameState gs)
        {
            if (_gameState != null) _gameState.OnGoldChanged -= OnGoldChanged;
            _gameState = gs;
            if (_gameState != null)
            {
                _gameState.OnGoldChanged += OnGoldChanged;
                OnGoldChanged(_gameState.Gold);
            }
        }

        public void Sync(IReadOnlyList<SquadData> squads, IReadOnlyList<TravelTask> tasks, System.Func<string, string> resolveRegionName, long nowUnix)
        {
            Render(squads, tasks, resolveRegionName, nowUnix);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_map == null || bodyText == null || eventData == null) return;
            var linkIndex = TMP_TextUtilities.FindIntersectingLink(bodyText, eventData.position, eventData.pressEventCamera);
            if (linkIndex < 0 || linkIndex >= bodyText.textInfo.linkCount) return;
            var linkInfo = bodyText.textInfo.linkInfo[linkIndex];
            var squadId = linkInfo.GetLinkID();
            if (!string.IsNullOrWhiteSpace(squadId))
            {
                _map.SetSelectedSquad(squadId);
                RefreshNow();
            }
        }

        private void BindIfNeeded()
        {
            if (_map == null) _map = UnityEngine.Object.FindFirstObjectByType<MapController>();
        }

        public void RefreshNow()
        {
            EnsureBodyText();
            BindIfNeeded();
            if (titleText != null) titleText.text = "Roster";
            if (_map == null)
            {
                bodyText.text = "Roster: (MapController not found)";
                return;
            }

            Render(_map.GetSquads(), _map.GetTravelTasks(), _map.GetRegionNameById, SimulationTime.NowSeconds);
        }

        private void Render(IReadOnlyList<SquadData> squads, IReadOnlyList<TravelTask> tasks, System.Func<string, string> resolveRegionName, long nowUnix)
        {
            var squadsNull = squads == null;
            var tasksNull = tasks == null;
            var resolverNull = resolveRegionName == null;
            if ((squadsNull || tasksNull || resolverNull) && !_nullSafeLogPrinted)
            {
                _nullSafeLogPrinted = true;
                Debug.Log($"[HUD] Null-safe path used squadsNull={squadsNull} tasksNull={tasksNull} resolverNull={resolverNull} [TODO REMOVE]");
            }

            squads ??= System.Array.Empty<SquadData>();
            tasks ??= System.Array.Empty<TravelTask>();
            resolveRegionName ??= id => string.IsNullOrEmpty(id) ? "?" : id;

            var sb = new StringBuilder(1024);
            sb.AppendLine("<b>Squads</b>");

            if (squads == null || squads.Count == 0)
            {
                sb.AppendLine("- none");
            }
            else
            {
                var selectedId = _map != null ? _map.GetSelectedSquadId() : null;
                for (var i = 0; i < squads.Count; i++)
                {
                    var squad = squads[i];
                    if (squad == null) continue;
                    if (string.IsNullOrEmpty(squad.id)) continue;
                    var stateText = BuildSquadStateText(squad, tasks, resolveRegionName);
                    var membersText = $"Members {((squad.hunterIds != null && squad.hunterIds.Count > 0) ? squad.hunterIds.Count : squad.membersCount)}";
                    var readinessText = $"Readiness {ComputeReadinessPercent(squad)}%";
                    var line = $"[Squad] {squad.name} | {stateText} | {membersText} | Cohesion {squad.cohesion} | {readinessText}";
                    var isSelected = !string.IsNullOrEmpty(selectedId) && selectedId == squad.id;
                    var color = isSelected ? "#FFE08A" : "#E9F4FF";
                    sb.Append($"<link={squad.id}><color={color}>{line}</color></link>");
                    sb.Append('\n');
                }
            }

            sb.AppendLine("<b>Solo Hunters</b>");
            var solos = _map != null ? _map.GetSoloHunters() : null;
            if (solos == null || solos.Count == 0)
            {
                sb.Append("- none");
            }
            else
            {
                for (var i = 0; i < solos.Count; i++)
                {
                    var hunter = solos[i];
                    if (hunter == null) continue;
                    if (string.IsNullOrEmpty(hunter.id)) continue;
                    var state = BuildSoloStateText(hunter, resolveRegionName);
                    var lone = hunter.loneWolf ? " LoneWolf" : string.Empty;
                    var exhausted = hunter.exhaustedToday ? " Exhausted" : string.Empty;
                    sb.AppendLine($"[Solo] {hunter.name} [{hunter.rank}]{lone} | {state} | HP {hunter.hp}/{hunter.maxHp}{exhausted}");
                }
            }

            bodyText.text = sb.ToString();
        }

        private string BuildSoloStateText(HunterData hunter, System.Func<string, string> resolveRegionName)
        {
            if (_map == null || hunter == null) return "Idle";
            var task = _map.GetTravelTaskForSoloHunter(hunter.id);
            if (task == null)
            {
                return hunter.exhaustedToday ? "Exhausted" : "Idle";
            }

            if (task.phase == TravelPhase.Outbound)
            {
                var toRegion = resolveRegionName != null ? resolveRegionName(task.toRegionId) : task.toRegionId;
                return $"Traveling -> {Truncate(toRegion, 12)} (OUT)";
            }

            return "Returning (RET)";
        }

        private static string BuildSquadStateText(SquadData squad, IReadOnlyList<TravelTask> tasks, System.Func<string, string> resolveRegionName)
        {
            var squadId = squad != null ? squad.id : null;
            var task = FindTaskForSquad(tasks, squadId);
            if (task == null)
            {
                return squad != null && squad.exhausted ? "Exhausted" : "Idle";
            }

            if (task.phase == TravelPhase.Outbound)
            {
                var toRegion = resolveRegionName != null ? resolveRegionName(task.toRegionId) : task.toRegionId;
                return $"Traveling -> {Truncate(toRegion, 12)} (OUT)";
            }

            return "Returning (RET)";
        }

        private static TravelTask FindTaskForSquad(IReadOnlyList<TravelTask> tasks, string squadId)
        {
            if (tasks == null || string.IsNullOrEmpty(squadId)) return null;
            for (var i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                if (task != null && task.squadId == squadId) return task;
            }
            return null;
        }

        private static int ComputeReadinessPercent(SquadData squad)
        {
            var members = squad != null ? squad.members : null;
            if (members == null || members.Count == 0) return 100;
            float sum = 0f; var validCount = 0;
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || member.maxHp <= 0) continue;
                validCount++;
                sum += Mathf.Clamp01(member.hp / (float)member.maxHp);
            }
            if (validCount == 0) return 100;
            return Mathf.RoundToInt((sum / validCount) * 100f);
        }

        private static string Truncate(string value, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxChars) return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
            return value.Substring(0, Mathf.Max(1, maxChars - 1)) + "…";
        }

        private void EnsureBodyText()
        {
            if (bodyText == null)
            {
                var existing = transform.Find("BodyText");
                if (existing != null) bodyText = existing.GetComponent<TMP_Text>();
            }

            if (bodyText == null)
            {
                var go = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(transform, false);
                bodyText = go.GetComponent<TextMeshProUGUI>();
            }

            var rect = bodyText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            if (goldText != null && goldText.font != null) bodyText.font = goldText.font;
            else if (TMP_Settings.defaultFontAsset != null) bodyText.font = TMP_Settings.defaultFontAsset;

            bodyText.color = Color.white;
            bodyText.fontSize = 15f;
            bodyText.raycastTarget = true;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void OnGoldChanged(int value)
        {
            if (goldText != null) goldText.text = $"Gold: {value}";
        }
    }
}
