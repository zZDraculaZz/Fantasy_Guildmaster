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

        private const float HudWidth = 300f;
        private const float HudHeight = 260f;

        private readonly Dictionary<string, SquadStatusRow> _rowsBySquadId = new();

        private MapController _mapController;
        private SquadRoster _squadRoster;
        private GameClock _gameClock;
        private string _lastRosterSignature = string.Empty;
        private bool _loggedInitialRebuild;
        private bool _loggedParentPath;

        private void Awake()
        {
            ApplyFixedHudLayout();
            EnsureContentRootReady();
            EnsureRowPrefab();
            HardBind();
            UpdateGoldText(gameState != null ? gameState.Gold : 0);
        }

        private void Start()
        {
            StartCoroutine(WaitAndInitializeRows());
        }

        private IEnumerator WaitAndInitializeRows()
        {
            yield return null;

            var waited = 0f;
            while ((_squadRoster == null || _squadRoster.Squads.Count == 0) && waited < 2f)
            {
                HardBind();
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            RebuildRowsIfNeeded(force: true);
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
            ApplyFixedHudLayout();
            EnsureContentRootReady();
            EnsureRowPrefab();

            if (titleText != null)
            {
                titleText.text = "Squads";
            }

            if (squads == null)
            {
                return;
            }

            if (HasRosterCompositionChanged(squads))
            {
                RebuildRowsIfNeeded(force: true);
            }

            UpdateRows(squads, tasks, nowUnix);
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
            if (squads == null)
            {
                return;
            }

            var tasks = _mapController.GetTravelTasks();
            UpdateRows(squads, tasks, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            RefreshPanelHeight();
        }

        private void RebuildRowsIfNeeded(bool force = false)
        {
            EnsureContentRootReady();
            EnsureRowPrefab();

            if (_mapController == null)
            {
                _mapController = FindFirstObjectByType<MapController>();
            }

            if (_mapController == null)
            {
                return;
            }

            var squads = _squadRoster != null ? _squadRoster.GetSquads() : _mapController.GetSquads();
            if (squads == null || squads.Count == 0)
            {
                return;
            }

            var signature = BuildRosterSignature(squads);
            if (!force && signature == _lastRosterSignature)
            {
                return;
            }

            EnsureRowsMatchSquads(squads);
            _lastRosterSignature = signature;

            if (!_loggedInitialRebuild)
            {
                Debug.Log($"[HUDDebug] RebuildRows squadsCount={squads.Count}");
                _loggedInitialRebuild = true;
            }
            else
            {
                Debug.Log($"[HUDDebug] RebuildRows composition changed: {signature}");
            }
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
            LogHudParentPathOnce();
        }

        private void OnRosterChanged()
        {
            HardBind();
            RebuildRowsIfNeeded();
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

        private void ApplyFixedHudLayout()
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
            rootRect.sizeDelta = new Vector2(HudWidth, HudHeight);

            var rootLayoutElement = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            rootLayoutElement.ignoreLayout = true;
            rootLayoutElement.minWidth = HudWidth;
            rootLayoutElement.preferredWidth = HudWidth;
            rootLayoutElement.minHeight = HudHeight;
            rootLayoutElement.preferredHeight = HudHeight;

            var rootLayout = GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(8, 8, 8, 8);
            rootLayout.spacing = 6f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            var rootFitter = GetComponent<ContentSizeFitter>();
            if (rootFitter != null)
            {
                Destroy(rootFitter);
            }

            if (titleText != null)
            {
                var titleLayout = titleText.GetComponent<LayoutElement>() ?? titleText.gameObject.AddComponent<LayoutElement>();
                titleLayout.minHeight = 28f;
                titleLayout.preferredHeight = 28f;
                titleLayout.flexibleHeight = 0f;
            }

            if (goldText != null)
            {
                var goldLayout = goldText.GetComponent<LayoutElement>() ?? goldText.gameObject.AddComponent<LayoutElement>();
                goldLayout.minHeight = 22f;
                goldLayout.preferredHeight = 22f;
                goldLayout.flexibleHeight = 0f;
            }

            if (scrollRect != null)
            {
                var scrollRectTransform = scrollRect.transform as RectTransform;
                if (scrollRectTransform != null)
                {
                    scrollRectTransform.anchorMin = new Vector2(0f, 1f);
                    scrollRectTransform.anchorMax = new Vector2(1f, 1f);
                    scrollRectTransform.pivot = new Vector2(0.5f, 1f);
                    scrollRectTransform.anchoredPosition = Vector2.zero;
                    scrollRectTransform.sizeDelta = Vector2.zero;
                }

                viewportLayoutElement ??= scrollRect.gameObject.GetComponent<LayoutElement>() ?? scrollRect.gameObject.AddComponent<LayoutElement>();
                viewportLayoutElement.minHeight = 120f;
                viewportLayoutElement.preferredHeight = 120f;
                viewportLayoutElement.flexibleHeight = 1f;

                var scrollFitter = scrollRect.GetComponent<ContentSizeFitter>();
                if (scrollFitter != null)
                {
                    Destroy(scrollFitter);
                }
            }
        }

        private void RefreshPanelHeight()
        {
            if (rowsRoot == null || rootRect == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rowsRoot);

            if (scrollRect != null)
            {
                scrollRect.vertical = _rowsBySquadId.Count > 5;
                scrollRect.enabled = true;
            }

            rootRect.sizeDelta = new Vector2(HudWidth, HudHeight);
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
            }

            foreach (var pair in _rowsBySquadId)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                pair.Value.gameObject.SetActive(validIds.Contains(pair.Key));
            }
        }

        private void EnsureContentRootReady()
        {
            if (scrollRect != null)
            {
                viewportRect ??= scrollRect.viewport;
                if (viewportRect == null)
                {
                    viewportRect = FindChildByName(scrollRect.transform, "Viewport") as RectTransform;
                    if (viewportRect != null)
                    {
                        scrollRect.viewport = viewportRect;
                    }
                }
            }

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

            if (viewportRect != null && rowsRoot != null && rowsRoot.parent != viewportRect)
            {
                var contentUnderViewport = viewportRect.Find("Content") as RectTransform;
                if (contentUnderViewport == null)
                {
                    var contentGo = new GameObject("Content", typeof(RectTransform));
                    contentUnderViewport = contentGo.GetComponent<RectTransform>();
                    contentUnderViewport.SetParent(viewportRect, false);
                }

                rowsRoot = contentUnderViewport;
            }

            if (rowsRoot == null)
            {
                return;
            }

            rowsRoot.anchorMin = new Vector2(0f, 1f);
            rowsRoot.anchorMax = new Vector2(1f, 1f);
            rowsRoot.pivot = new Vector2(0.5f, 1f);
            rowsRoot.anchoredPosition = Vector2.zero;

            var contentLayout = rowsRoot.GetComponent<VerticalLayoutGroup>() ?? rowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(8, 8, 8, 8);
            contentLayout.spacing = 6f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var contentFitter = rowsRoot.GetComponent<ContentSizeFitter>() ?? rowsRoot.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (scrollRect != null)
            {
                scrollRect.content = rowsRoot;
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                viewportRect ??= scrollRect.viewport;
            }

            if (viewportRect != null)
            {
                var viewportImage = viewportRect.GetComponent<Image>() ?? viewportRect.gameObject.AddComponent<Image>();
                viewportImage.color = new Color(0f, 0f, 0f, 0f);
                _ = viewportRect.GetComponent<RectMask2D>() ?? viewportRect.gameObject.AddComponent<RectMask2D>();

                var viewportFitter = viewportRect.GetComponent<ContentSizeFitter>();
                if (viewportFitter != null)
                {
                    Destroy(viewportFitter);
                }
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

            var rowLayoutElement = rowGo.GetComponent<LayoutElement>();
            rowLayoutElement.minHeight = 32f;
            rowLayoutElement.preferredHeight = 32f;

            var name = CreateRuntimeText("NameText", rowGo.transform, TextAlignmentOptions.MidlineLeft, 14f);
            var nameLayout = name.gameObject.AddComponent<LayoutElement>();
            nameLayout.preferredWidth = 110f;

            var statusTimer = CreateRuntimeText("StatusText", rowGo.transform, TextAlignmentOptions.MidlineLeft, 14f);
            var statusLayout = statusTimer.gameObject.AddComponent<LayoutElement>();
            statusLayout.flexibleWidth = 1f;

            var hp = CreateRuntimeText("HpText", rowGo.transform, TextAlignmentOptions.MidlineRight, 14f);
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

        private void LogHudParentPathOnce()
        {
            if (_loggedParentPath)
            {
                return;
            }

            var parentPath = BuildParentPath(transform);
            Debug.Log($"[HUDDebug] HUD parent path={parentPath}, isUnderViewportMask={IsUnderViewportMask(transform)}");
            _loggedParentPath = true;
        }

        private static string BuildParentPath(Transform node)
        {
            if (node == null)
            {
                return "<null>";
            }

            var names = new List<string>();
            var cursor = node;
            while (cursor != null)
            {
                names.Add(cursor.name);
                cursor = cursor.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static bool IsUnderViewportMask(Transform node)
        {
            var cursor = node != null ? node.parent : null;
            while (cursor != null)
            {
                if (cursor.GetComponent<RectMask2D>() != null || cursor.GetComponent<Mask>() != null)
                {
                    return true;
                }

                cursor = cursor.parent;
            }

            return false;
        }

        private static string BuildRosterSignature(IReadOnlyList<SquadData> squads)
        {
            if (squads == null || squads.Count == 0)
            {
                return string.Empty;
            }

            var ids = new List<string>(squads.Count);
            for (var i = 0; i < squads.Count; i++)
            {
                var id = squads[i]?.id;
                if (!string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                }
            }

            ids.Sort(StringComparer.Ordinal);
            return string.Join("|", ids);
        }

        private bool HasRosterCompositionChanged(IReadOnlyList<SquadData> squads)
        {
            return BuildRosterSignature(squads) != _lastRosterSignature;
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
