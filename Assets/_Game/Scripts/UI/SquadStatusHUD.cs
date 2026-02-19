using System;
using System.Collections;
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

        private MapController _mapController;
        private SquadRoster _squadRoster;
        private GameClock _gameClock;
        private bool _debugLogged;

        private void Awake()
        {
            ApplyCompactLayout();
            EnsureContentRootReady();
            EnsureRowPrefab();
            HardBind();
            UpdateGoldText(gameState != null ? gameState.Gold : 0);
            RebuildRows();
        }

        private void Start()
        {
            StartCoroutine(RebuildWhenRosterReady());
        }

        private IEnumerator RebuildWhenRosterReady()
        {
            yield return null;

            var waited = 0f;
            while ((_squadRoster == null || _squadRoster.Squads.Count == 0) && waited < 2f)
            {
                HardBind();
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            RebuildRows();
            RefreshNow();
        }

        private void OnEnable()
        {
            if (gameState != null)
            {
                gameState.OnGoldChanged += UpdateGoldText;
                UpdateGoldText(gameState.Gold);
            }

            if (_gameClock != null)
            {
                _gameClock.TickSecond += OnTickSecond;
            }

            if (_squadRoster != null)
            {
                _squadRoster.OnRosterChanged += OnRosterChanged;
            }
        }

        private void OnDisable()
        {
            if (gameState != null)
            {
                gameState.OnGoldChanged -= UpdateGoldText;
            }

            if (_gameClock != null)
            {
                _gameClock.TickSecond -= OnTickSecond;
            }

            if (_squadRoster != null)
            {
                _squadRoster.OnRosterChanged -= OnRosterChanged;
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
            EnsureContentRootReady();
            EnsureRowPrefab();

            if (squads == null || rowsRoot == null || rowPrefab == null)
            {
                return;
            }

            EnsureRowsMatchSquads(squads);
            UpdateRows(squads, tasks, nowUnix);
            RefreshPanelHeight();
        }

        public void RebuildRows()
        {
            EnsureContentRootReady();
            EnsureRowPrefab();

            if (_mapController == null)
            {
                _mapController = FindFirstObjectByType<MapController>();
            }

            if (_mapController == null)
            {
                Debug.LogWarning("[HUDDebug] RebuildRows skipped: MapController not found");
                return;
            }

            var squads = _squadRoster != null ? _squadRoster.GetSquads() : _mapController.GetSquads();
            var squadsCount = squads != null ? squads.Count : 0;
            Debug.Log($"[HUDDebug] RebuildRows squadsCount={squadsCount}");
            if (squads == null || squadsCount == 0)
            {
                return;
            }

            EnsureRowsMatchSquads(squads);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rowsRoot);
            var contentHeight = rowsRoot.rect.height;
            var firstRow = rowsRoot.childCount > 0 ? rowsRoot.GetChild(0) as RectTransform : null;
            var firstRowHeight = firstRow != null ? firstRow.rect.height : 0f;
            Debug.Log($"[HUDDebug] Rows alive={_rowsBySquadId.Count}, contentChildCount={rowsRoot.childCount}");
            Debug.Log($"[HUDDebug] Content rect height={contentHeight:0.##}, firstRowHeight={firstRowHeight:0.##}");
            RefreshPanelHeight();
        }

        public void RefreshNow()
        {
            if (_mapController == null)
            {
                _mapController = FindFirstObjectByType<MapController>();
                if (_mapController == null)
                {
                    return;
                }
            }

            var squads = _squadRoster != null ? _squadRoster.GetSquads() : _mapController.GetSquads();
            var tasks = _mapController.GetTravelTasks();
            if (squads == null)
            {
                return;
            }

            EnsureContentRootReady();
            EnsureRowPrefab();
            EnsureRowsMatchSquads(squads);
            UpdateRows(squads, tasks, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            RefreshPanelHeight();
        }

        private void HardBind()
        {
            _mapController = FindFirstObjectByType<MapController>();

            if (gameState == null)
            {
                gameState = FindFirstObjectByType<GameState>();
            }

            var foundRoster = FindFirstObjectByType<SquadRoster>();
            if (_squadRoster != foundRoster)
            {
                if (_squadRoster != null)
                {
                    _squadRoster.OnRosterChanged -= OnRosterChanged;
                }

                _squadRoster = foundRoster;

                if (_squadRoster != null && isActiveAndEnabled)
                {
                    _squadRoster.OnRosterChanged += OnRosterChanged;
                }
            }

            _gameClock = FindFirstObjectByType<GameClock>();

            if (!_debugLogged)
            {
                var squadCount = _squadRoster != null ? _squadRoster.Squads.Count : (_mapController?.GetSquads()?.Count ?? 0);
                if (squadCount > 0)
                {
                    var contentName = rowsRoot != null ? rowsRoot.name : "null";
                    var rowName = rowPrefab != null ? rowPrefab.name : "null";
                    Debug.Log($"[HUDDebug] MapController found={_mapController != null}, squadsCount={squadCount}");
                    Debug.Log($"[HUDDebug] Content={contentName}, RowPrefab={rowName}");
                    _debugLogged = true;
                }
            }
        }

        private void OnRosterChanged()
        {
            RebuildRows();
            RefreshNow();
        }

        private void OnTickSecond()
        {
            RefreshNow();
        }

        private void UpdateRows(IReadOnlyList<SquadData> squads, IReadOnlyList<TravelTask> tasks, long nowUnix)
        {
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
                rowsRoot.pivot = new Vector2(0f, 1f);
                rowsRoot.anchoredPosition = Vector2.zero;
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
            if (rowsRoot == null || squads == null)
            {
                return;
            }

            var validIds = new HashSet<string>();
            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null || string.IsNullOrEmpty(squad.id))
                {
                    continue;
                }

                validIds.Add(squad.id);

                if (!_rowsBySquadId.TryGetValue(squad.id, out var row) || row == null)
                {
                    row = Instantiate(rowPrefab);
                    _rowsBySquadId[squad.id] = row;
                }

                row.name = $"Row_{squad.id}";
                row.transform.SetParent(rowsRoot, false);
                row.transform.localScale = Vector3.one;
                row.transform.SetAsLastSibling();
                row.gameObject.SetActive(true);
                EnsureRowLayoutElement(row);
                row.SetData(string.IsNullOrWhiteSpace(squad.name) ? squad.id : squad.name, "IDLE 00:00", $"{Mathf.Max(0, squad.hp)}/{Mathf.Max(1, squad.maxHp)}", Color.white);
            }

            if (squads.Count > 0)
            {
                foreach (var pair in _rowsBySquadId)
                {
                    if (pair.Value == null)
                    {
                        continue;
                    }

                    pair.Value.gameObject.SetActive(validIds.Contains(pair.Key));
                }
            }
        }

        private void EnsureContentRootReady()
        {
            if (rowsRoot == null)
            {
                if (scrollRect != null && scrollRect.content != null)
                {
                    rowsRoot = scrollRect.content;
                }
                else
                {
                    var content = FindChildByName(transform, "Content") as RectTransform;

                    if (content == null && viewportRect != null)
                    {
                        var contentGo = new GameObject("Content", typeof(RectTransform));
                        content = contentGo.GetComponent<RectTransform>();
                        content.SetParent(viewportRect, false);
                    }

                    rowsRoot = content;
                }
            }

            if (rowsRoot == null)
            {
                return;
            }

            rowsRoot.anchorMin = new Vector2(0f, 1f);
            rowsRoot.anchorMax = new Vector2(1f, 1f);
            rowsRoot.pivot = new Vector2(0f, 1f);
            rowsRoot.anchoredPosition = Vector2.zero;

            var contentLayout = rowsRoot.GetComponent<VerticalLayoutGroup>() ?? rowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(8, 8, 8, 8);
            contentLayout.spacing = 6f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var fitter = rowsRoot.GetComponent<ContentSizeFitter>() ?? rowsRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (scrollRect != null)
            {
                scrollRect.content = rowsRoot;
            }

            if (viewportRect != null)
            {
                var viewportImage = viewportRect.GetComponent<Image>() ?? viewportRect.gameObject.AddComponent<Image>();
                viewportImage.color = new Color(0f, 0f, 0f, 0f);
                _ = viewportRect.GetComponent<Mask>() ?? viewportRect.gameObject.AddComponent<Mask>();
                _ = viewportRect.GetComponent<RectMask2D>() ?? viewportRect.gameObject.AddComponent<RectMask2D>();

                var viewportFitter = viewportRect.GetComponent<ContentSizeFitter>();
                if (viewportFitter != null)
                {
                    Destroy(viewportFitter);
                }
            }

            var rootFitter = GetComponent<ContentSizeFitter>();
            if (rootFitter != null)
            {
                Destroy(rootFitter);
            }
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindChildByName(root.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void EnsureRowLayoutElement(SquadStatusRow row)
        {
            if (row == null)
            {
                return;
            }

            var layout = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 32f;
            layout.preferredHeight = 32f;
            layout.flexibleHeight = 0f;
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
            rootLayoutElement.minHeight = 32f;
            rootLayoutElement.preferredHeight = 32f;

            var name = CreateRuntimeText("NameText", rowGo.transform, TextAlignmentOptions.Left, 14f);
            var nameLayout = name.gameObject.AddComponent<LayoutElement>();
            nameLayout.preferredWidth = 110f;

            var statusTimer = CreateRuntimeText("StatusText", rowGo.transform, TextAlignmentOptions.Left, 14f);
            var statusLayout = statusTimer.gameObject.AddComponent<LayoutElement>();
            statusLayout.flexibleWidth = 1f;

            var hp = CreateRuntimeText("HpText", rowGo.transform, TextAlignmentOptions.Right, 14f);
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
