using System;
using System.Collections;
using System.Text;
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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

        private void Awake()
        {
            EnsureBodyText();
        }

        private void OnEnable()
        {
            StartCoroutine(DelayedBindAndStart());
        }

        private void OnDisable()
        {
            if (_gameState != null)
            {
                _gameState.OnGoldChanged -= OnGoldChanged;
            }

            if (_tick != null) StopCoroutine(_tick);
            _tick = null;
        }

        private IEnumerator DelayedBindAndStart()
        {
            yield return null;
            BindIfNeeded();
            RefreshNow();

            if (_tick == null)
            {
                _tick = StartCoroutine(TickRoutine());
            }
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
            if (_gameState != null)
            {
                _gameState.OnGoldChanged -= OnGoldChanged;
            }

            _gameState = gs;

            if (_gameState != null)
            {
                _gameState.OnGoldChanged += OnGoldChanged;
                OnGoldChanged(_gameState.Gold);
            }
        }

        public void Sync(System.Collections.Generic.IReadOnlyList<SquadData> squads, System.Collections.Generic.IReadOnlyList<TravelTask> tasks, Func<string, string> resolveRegionName, long nowUnix)
        {
            Render(squads, tasks, resolveRegionName, nowUnix);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_map == null || bodyText == null || eventData == null)
            {
                return;
            }

            var linkIndex = TMP_TextUtilities.FindIntersectingLink(bodyText, eventData.position, eventData.pressEventCamera);
            if (linkIndex < 0 || linkIndex >= bodyText.textInfo.linkCount)
            {
                return;
            }

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
            if (_map != null)
            {
                return;
            }

            _map = UnityEngine.Object.FindFirstObjectByType<MapController>();
        }

        public void RefreshNow()
        {
            EnsureBodyText();
            BindIfNeeded();

            if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
            {
                titleText.text = "Squads";
            }

            if (_map == null)
            {
                bodyText.text = "Squads: (MapController not found)";
                return;
            }

            Render(_map.GetSquads(), _map.GetTravelTasks(), _map.GetRegionNameById, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        private void Render(System.Collections.Generic.IReadOnlyList<SquadData> squads, System.Collections.Generic.IReadOnlyList<TravelTask> tasks, Func<string, string> resolveRegionName, long nowUnix)
        {
            if (squads == null || squads.Count == 0)
            {
                bodyText.text = "No squads.";
                return;
            }

            var selectedId = _map != null ? _map.GetSelectedSquadId() : null;
            var sb = new StringBuilder(512);

            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null)
                {
                    continue;
                }

                var stateText = BuildStateText(squad.id, tasks, resolveRegionName);
                var membersText = BuildMembersText(squad);
                var readinessText = $"Readiness {ComputeReadinessPercent(squad)}%";
                var squadName = string.IsNullOrWhiteSpace(squad.name) ? squad.id : squad.name;
                var line = $"{squadName} | {stateText} | {membersText} | {readinessText}";
                var isSelected = !string.IsNullOrEmpty(selectedId) && selectedId == squad.id;
                var color = isSelected ? "#FFE08A" : "#E9F4FF";

                sb.Append($"<link={squad.id}><color={color}>{line}</color></link>");
                if (i < squads.Count - 1)
                {
                    sb.Append('\n');
                }
            }

            bodyText.text = sb.ToString();
        }

        private static string BuildStateText(string squadId, System.Collections.Generic.IReadOnlyList<TravelTask> tasks, Func<string, string> resolveRegionName)
        {
            var task = FindTaskForSquad(tasks, squadId);
            if (task == null)
            {
                return "Idle";
            }

            if (task.phase == TravelPhase.Outbound)
            {
                var toRegion = resolveRegionName != null ? resolveRegionName(task.toRegionId) : task.toRegionId;
                return $"Traveling -> {Truncate(toRegion, 12)} (OUT)";
            }

            return "Returning (RET)";
        }

        private static TravelTask FindTaskForSquad(System.Collections.Generic.IReadOnlyList<TravelTask> tasks, string squadId)
        {
            if (tasks == null)
            {
                return null;
            }

            for (var i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                if (task != null && task.squadId == squadId)
                {
                    return task;
                }
            }

            return null;
        }

        private static string BuildMembersText(SquadData squad)
        {
            var members = squad != null ? squad.members : null;
            if (members == null || members.Count == 0)
            {
                return "Members ?";
            }

            return $"Members {members.Count}";
        }

        private static int ComputeReadinessPercent(SquadData squad)
        {
            var members = squad != null ? squad.members : null;
            if (members == null || members.Count == 0)
            {
                return 100;
            }

            float sum = 0f;
            var validCount = 0;
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || member.maxHp <= 0)
                {
                    continue;
                }

                validCount++;
                sum += Mathf.Clamp01(member.hp / (float)member.maxHp);
            }

            if (validCount == 0)
            {
                return 100;
            }

            return Mathf.RoundToInt((sum / validCount) * 100f);
        }

        private static string Truncate(string value, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxChars)
            {
                return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
            }

            return value.Substring(0, Math.Max(1, maxChars - 1)) + "…";
        }

        private void EnsureBodyText()
        {
            if (bodyText == null)
            {
                var existing = transform.Find("BodyText");
                if (existing != null)
                {
                    bodyText = existing.GetComponent<TMP_Text>();
                }
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

            if (goldText != null && goldText.font != null)
            {
                bodyText.font = goldText.font;
            }
            else if (TMP_Settings.defaultFontAsset != null)
            {
                bodyText.font = TMP_Settings.defaultFontAsset;
            }

            bodyText.color = Color.white;
            bodyText.fontSize = 15f;
            bodyText.raycastTarget = true;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Ellipsis;
            bodyText.enableWordWrapping = true;
        }

        private void OnGoldChanged(int value)
        {
            if (goldText != null)
            {
                goldText.text = $"Gold: {value}";
            }
        }
    }
}
