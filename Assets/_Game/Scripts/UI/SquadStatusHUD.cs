using System;
using System.Collections.Generic;
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public sealed class SquadStatusHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private LayoutElement viewportLayoutElement;
        [SerializeField] private SquadStatusRow rowPrefab;
        [SerializeField] private GameState gameState;

        private const float CompactWidth = 280f;
        private const float CompactMinHeight = 140f;

        private readonly Dictionary<string, SquadStatusRow> _rowsBySquadId = new();

        private void Awake()
        {
            ApplyCompactLayout();
            UpdateGoldText(gameState != null ? gameState.Gold : 0);
        }

        private void OnEnable()
        {
            if (gameState != null)
            {
                gameState.OnGoldChanged += UpdateGoldText;
                UpdateGoldText(gameState.Gold);
            }
        }

        private void OnDisable()
        {
            if (gameState != null)
            {
                gameState.OnGoldChanged -= UpdateGoldText;
            }
        }

        public void BindGameState(GameState state)
        {
            if (gameState == state)
            {
                return;
            }

            if (gameState != null)
            {
                gameState.OnGoldChanged -= UpdateGoldText;
            }

            gameState = state;

            if (gameState != null && isActiveAndEnabled)
            {
                gameState.OnGoldChanged += UpdateGoldText;
                UpdateGoldText(gameState.Gold);
            }
        }

        public void Sync(IReadOnlyList<SquadData> squads, IReadOnlyList<TravelTask> tasks, Func<string, string> resolveRegionName, long nowUnix)
        {
            if (titleText != null)
            {
                titleText.text = "Squads";
            }

            ApplyCompactLayout();

            rowsRoot ??= scrollRect != null ? scrollRect.content : null;
            EnsureRowPrefab();

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
                BuildStatus(squad, task, out var statusTimer, out var statusColor, out var hpTextValue, nowUnix);
                row.SetData(string.IsNullOrWhiteSpace(squad.name) ? squad.id : squad.name, statusTimer, hpTextValue, statusColor);
            }

            RefreshPanelHeight();
        }

        private void UpdateGoldText(int gold)
        {
            if (goldText != null)
            {
                goldText.text = $"Gold: {gold}";
            }
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
            rootRect.localScale = Vector3.one;
            rootRect.sizeDelta = new Vector2(CompactWidth, Mathf.Max(CompactMinHeight, rootRect.sizeDelta.y));

            var layoutElement = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            layoutElement.minWidth = CompactWidth;
            layoutElement.preferredWidth = CompactWidth;

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

            rootRect.sizeDelta = new Vector2(CompactWidth, Mathf.Max(CompactMinHeight, 86f + viewportHeight));
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

        private void EnsureRowPrefab()
        {
            if (rowPrefab != null || rowsRoot == null)
            {
                return;
            }

            var rowGo = new GameObject("SquadStatusRow_Runtime", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(SquadStatusRow));
            rowGo.transform.SetParent(rowsRoot, false);
            rowGo.SetActive(false);

            var image = rowGo.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.06f);

            var rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(8, 8, 5, 5);
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = false;

            var rootLayoutElement = rowGo.GetComponent<LayoutElement>();
            rootLayoutElement.minHeight = 34f;

            var name = CreateRuntimeText("SquadName", rowGo.transform, TextAlignmentOptions.Left, 14f);
            var nameLayout = name.gameObject.AddComponent<LayoutElement>();
            nameLayout.preferredWidth = 110f;

            var statusTimer = CreateRuntimeText("StatusTimer", rowGo.transform, TextAlignmentOptions.Left, 14f);
            var statusLayout = statusTimer.gameObject.AddComponent<LayoutElement>();
            statusLayout.flexibleWidth = 1f;

            var hp = CreateRuntimeText("Hp", rowGo.transform, TextAlignmentOptions.Right, 14f);
            var hpLayout = hp.gameObject.AddComponent<LayoutElement>();
            hpLayout.preferredWidth = 72f;

            var row = rowGo.GetComponent<SquadStatusRow>();
            row.ConfigureRuntime(name, statusTimer, hp);
            rowPrefab = row;
        }

        private static TMP_Text CreateRuntimeText(string objectName, Transform parent, TextAlignmentOptions alignment, float fontSize)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.text = objectName;
            return text;
        }

        private static void BuildStatus(SquadData squad, TravelTask task, out string statusTimer, out Color statusColor, out string hpTextValue, long nowUnix)
        {
            if (squad.IsDestroyed)
            {
                statusTimer = "DESTROYED";
                statusColor = new Color(0.75f, 0.2f, 0.2f, 1f);
                hpTextValue = "--";
                return;
            }

            hpTextValue = $"{Mathf.Max(0, squad.hp)}/{Mathf.Max(1, squad.maxHp)}";

            if (task != null)
            {
                var phaseLabel = task.phase == TravelPhase.Outbound ? "OUT" : "RET";
                statusTimer = $"{phaseLabel} {FormatTimer(task.endUnix - nowUnix)}";
                statusColor = task.phase == TravelPhase.Outbound
                    ? new Color(0.96f, 0.84f, 0.28f, 1f)
                    : new Color(0.99f, 0.61f, 0.26f, 1f);
                return;
            }

            switch (squad.state)
            {
                case SquadState.ResolvingEncounter:
                    statusTimer = "WAIT";
                    statusColor = new Color(0.94f, 0.32f, 0.32f, 1f);
                    return;
                case SquadState.ReturningToHQ:
                    statusTimer = "RET";
                    statusColor = new Color(0.99f, 0.61f, 0.26f, 1f);
                    return;
                case SquadState.TravelingToRegion:
                    statusTimer = "OUT";
                    statusColor = new Color(0.96f, 0.84f, 0.28f, 1f);
                    return;
                default:
                    statusTimer = "IDLE";
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
