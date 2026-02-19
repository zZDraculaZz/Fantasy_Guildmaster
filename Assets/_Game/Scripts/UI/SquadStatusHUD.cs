using System;
using System.Collections.Generic;
using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public sealed class SquadStatusHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private LayoutElement viewportLayoutElement;
        [SerializeField] private SquadStatusRow rowPrefab;

        private readonly Dictionary<string, SquadStatusRow> _rowsBySquadId = new();

        private void Awake()
        {
            ApplyCompactLayout();
        }

        public void Sync(IReadOnlyList<SquadData> squads, IReadOnlyList<TravelTask> tasks, Func<string, string> resolveRegionName, long nowUnix)
        {
            if (titleText != null)
            {
                titleText.text = "Squads";
            }

            ApplyCompactLayout();

            if (squads == null || rowsRoot == null || rowPrefab == null)
            {
                return;
            }

            var taskBySquad = new Dictionary<string, TravelTask>();
            if (tasks != null)
            {
                for (var i = 0; i < tasks.Count; i++)
                {
                    var task = tasks[i];
                    if (task != null && !string.IsNullOrEmpty(task.squadId))
                    {
                        taskBySquad[task.squadId] = task;
                    }
                }
            }

            EnsureRowsMatchSquads(squads);

            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null || string.IsNullOrEmpty(squad.id))
                {
                    continue;
                }

                if (!_rowsBySquadId.TryGetValue(squad.id, out var row) || row == null)
                {
                    continue;
                }

                row.gameObject.SetActive(true);
                taskBySquad.TryGetValue(squad.id, out var task);
                BuildStatus(squad, task, resolveRegionName, nowUnix, out var status, out var timer, out var statusColor);
                row.SetData(string.IsNullOrWhiteSpace(squad.name) ? squad.id : squad.name, status, timer, statusColor);
            }

            RefreshPanelHeight();
        }

        private void ApplyCompactLayout()
        {
            rootRect ??= transform as RectTransform;
            if (rootRect == null)
            {
                return;
            }

            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(16f, -16f);

            if (Mathf.Abs(rootRect.sizeDelta.x - 280f) > 0.01f)
            {
                rootRect.sizeDelta = new Vector2(280f, Mathf.Max(140f, rootRect.sizeDelta.y));
            }

            if (rowsRoot != null)
            {
                rowsRoot.anchorMin = new Vector2(0f, 1f);
                rowsRoot.anchorMax = new Vector2(1f, 1f);
                rowsRoot.pivot = new Vector2(0.5f, 1f);
            }

            if (viewportRect != null)
            {
                viewportRect.anchorMin = new Vector2(0f, 1f);
                viewportRect.anchorMax = new Vector2(1f, 1f);
                viewportRect.pivot = new Vector2(0.5f, 1f);
            }

        }

        private void RefreshPanelHeight()
        {
            if (rowsRoot == null || rootRect == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rowsRoot);
            var contentHeight = LayoutUtility.GetPreferredHeight(rowsRoot);
            var useScroll = _rowsBySquadId.Count > 5;
            var viewportHeight = useScroll ? 220f : Mathf.Min(220f, Mathf.Max(52f, contentHeight + 4f));

            if (viewportLayoutElement != null)
            {
                viewportLayoutElement.preferredHeight = viewportHeight;
                viewportLayoutElement.minHeight = viewportHeight;
                viewportLayoutElement.flexibleHeight = 0f;
            }

            if (scrollRect != null)
            {
                scrollRect.vertical = useScroll;
                scrollRect.enabled = true;
            }

            rootRect.sizeDelta = new Vector2(280f, Mathf.Max(140f, 58f + viewportHeight));
        }

        private void EnsureRowsMatchSquads(IReadOnlyList<SquadData> squads)
        {
            var validIds = new HashSet<string>();
            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null || string.IsNullOrEmpty(squad.id))
                {
                    continue;
                }

                validIds.Add(squad.id);

                if (_rowsBySquadId.ContainsKey(squad.id))
                {
                    continue;
                }

                var row = Instantiate(rowPrefab, rowsRoot);
                row.name = $"SquadStatusRow_{squad.id}";
                row.gameObject.SetActive(true);
                _rowsBySquadId[squad.id] = row;
            }

            var toRemove = new List<string>();
            foreach (var pair in _rowsBySquadId)
            {
                if (!validIds.Contains(pair.Key))
                {
                    if (pair.Value != null)
                    {
                        Destroy(pair.Value.gameObject);
                    }

                    toRemove.Add(pair.Key);
                }
            }

            for (var i = 0; i < toRemove.Count; i++)
            {
                _rowsBySquadId.Remove(toRemove[i]);
            }
        }

        private static void BuildStatus(SquadData squad, TravelTask task, Func<string, string> resolveRegionName, long nowUnix, out string status, out string timer, out Color statusColor)
        {
            timer = task != null ? FormatTimer(task.endUnix - nowUnix) : "—";

            switch (squad.state)
            {
                case SquadState.TravelingToRegion:
                {
                    var regionName = task != null ? resolveRegionName?.Invoke(task.toRegionId) : resolveRegionName?.Invoke(squad.currentRegionId);
                    status = $"Traveling to {(!string.IsNullOrWhiteSpace(regionName) ? regionName : "region")}";
                    statusColor = new Color(0.96f, 0.84f, 0.28f, 1f);
                    return;
                }
                case SquadState.ReturningToHQ:
                    status = "Returning to HQ";
                    statusColor = new Color(0.99f, 0.61f, 0.26f, 1f);
                    return;
                case SquadState.ResolvingEncounter:
                    status = "In Encounter";
                    timer = "—";
                    statusColor = new Color(0.94f, 0.32f, 0.32f, 1f);
                    return;
                default:
                    status = "Idle at HQ";
                    timer = "—";
                    statusColor = new Color(0.41f, 0.9f, 0.5f, 1f);
                    return;
            }
        }

        private static string FormatTimer(long remainingSeconds)
        {
            var clamped = Mathf.Max(0, (int)remainingSeconds);
            var minutes = clamped / 60;
            var seconds = clamped % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }
}
