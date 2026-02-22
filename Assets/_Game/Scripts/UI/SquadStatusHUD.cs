using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public class SquadStatusHUD : MonoBehaviour, IPointerClickHandler
    {
        [Header("Optional UI")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;

        [Header("Required UI (auto-created if missing)")]
        [SerializeField] private TMP_Text bodyText;

        [Header("Optional Scroll Rows")]
        [SerializeField] private ScrollRect rosterScrollRect;
        [SerializeField] private RectTransform rosterViewport;
        [SerializeField] private RectTransform rosterContent;
        [SerializeField] private TMP_Text rosterRowPrefab;

        [Header("Behavior")]
        public bool forceLegacyText = true;
        [SerializeField] private float refreshSeconds = 1f;
        [SerializeField] private float paddingLeft = 8f;
        [SerializeField] private float paddingRight = 8f;
        [SerializeField] private float paddingTop = 8f;
        [SerializeField] private float paddingBottom = 8f;

        private readonly List<TMP_Text> _rowPool = new();
        private MapController _map;
        private GameState _gameState;
        private Coroutine _tick;
        private bool _nullSafeLogPrinted;
        private bool _legacyMissingRefsLogged;
        private bool _scrollFixLogPrinted;

        private void Awake()
        {
            ValidateHelperDefinitionsEditorOnly();
            EnsureBodyText();
        }

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
            if (_map == null || bodyText == null || eventData == null || !bodyText.gameObject.activeInHierarchy) return;
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
            if (_map == null) _map = Object.FindFirstObjectByType<MapController>();
        }

        public void RefreshNow()
        {
            EnsureBodyText();
            BindIfNeeded();
            if (titleText != null) titleText.text = "Roster";
            if (_map == null)
            {
                RenderLegacyText("Roster: (MapController not found)");
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
                Debug.Log($"[RosterHUD] Null-safe path used squadsNull={squadsNull} tasksNull={tasksNull} resolverNull={resolverNull} [TODO REMOVE]");
            }

            squads ??= System.Array.Empty<SquadData>();
            tasks ??= System.Array.Empty<TravelTask>();
            resolveRegionName ??= id => string.IsNullOrEmpty(id) ? "?" : id;

            var canUseScroll = rosterScrollRect != null
                && rosterViewport != null
                && rosterContent != null;

            if (!canUseScroll)
            {
                if (!_legacyMissingRefsLogged)
                {
                    _legacyMissingRefsLogged = true;
                    Debug.Log("[RosterHUD] Using legacy fallback (missing scroll refs) [TODO REMOVE]");
                }

                RenderLegacyText(BuildLegacyText(squads, tasks, resolveRegionName));
                return;
            }

            RenderScrollText(BuildLegacyText(squads, tasks, resolveRegionName));
        }

        private int RenderScrollRows(IReadOnlyList<SquadData> squads, IReadOnlyList<TravelTask> tasks, System.Func<string, string> resolveRegionName)
        {
            if (rosterContent == null || rosterRowPrefab == null)
            {
                return 0;
            }

            var rowIndex = 0;
            EnsureRow(ref rowIndex, "Squads", true);

            var selectedId = _map != null ? _map.GetSelectedSquadId() : null;
            if (squads == null || squads.Count == 0)
            {
                EnsureRow(ref rowIndex, "- none", false);
            }
            else
            {
                for (var i = 0; i < squads.Count; i++)
                {
                    var squad = squads[i];
                    if (squad == null || string.IsNullOrEmpty(squad.id))
                    {
                        continue;
                    }

                    var stateText = BuildSquadStateText(squad, tasks, resolveRegionName);
                    var membersText = $"Members {((squad.hunterIds != null && squad.hunterIds.Count > 0) ? squad.hunterIds.Count : squad.membersCount)}";
                    var readinessText = $"Readiness {ComputeReadinessPercent(squad)}%";
                    var selectedTag = selectedId == squad.id ? " *" : string.Empty;
                    EnsureRow(ref rowIndex, $"[Squad] {squad.name}{selectedTag} | {stateText} | {membersText} | Cohesion {squad.cohesion} | {readinessText}", false);
                }
            }

            EnsureRow(ref rowIndex, "Solo Hunters", true);
            var solos = _map != null ? _map.GetSoloHunters() : null;
            if (solos == null || solos.Count == 0)
            {
                EnsureRow(ref rowIndex, "- none", false);
            }
            else
            {
                for (var i = 0; i < solos.Count; i++)
                {
                    var hunter = solos[i];
                    if (hunter == null || string.IsNullOrEmpty(hunter.id))
                    {
                        continue;
                    }

                    var state = BuildSoloStateText(hunter, resolveRegionName);
                    var lone = hunter.loneWolf ? " LoneWolf" : string.Empty;
                    var exhausted = hunter.exhaustedToday ? " Exhausted" : string.Empty;
                    EnsureRow(ref rowIndex, $"[Solo] {hunter.name} [{hunter.rank}]{lone} | {state} | HP {hunter.hp}/{hunter.maxHp}{exhausted}", false);
                }
            }

            for (var i = rowIndex; i < _rowPool.Count; i++)
            {
                if (_rowPool[i] != null)
                {
                    _rowPool[i].gameObject.SetActive(false);
                }
            }

            return rowIndex;
        }

        private void EnsureRow(ref int rowIndex, string text, bool isHeader)
        {
            TMP_Text row;
            if (rowIndex < _rowPool.Count)
            {
                row = _rowPool[rowIndex];
            }
            else
            {
                row = Instantiate(rosterRowPrefab, rosterContent);
                row.gameObject.SetActive(true);
                row.raycastTarget = false;
                row.textWrappingMode = TextWrappingModes.NoWrap;
                row.overflowMode = TextOverflowModes.Ellipsis;
                _rowPool.Add(row);
            }

            row.gameObject.SetActive(true);
            row.text = isHeader ? $"<b>{text}</b>" : text;
            row.textWrappingMode = TextWrappingModes.NoWrap;
            row.overflowMode = TextOverflowModes.Ellipsis;
            row.raycastTarget = false;
            row.ForceMeshUpdate(true);
            rowIndex++;
        }

        private string BuildLegacyText(IReadOnlyList<SquadData> squads, IReadOnlyList<TravelTask> tasks, System.Func<string, string> resolveRegionName)
        {
            var squadsNull = squads == null;
            var tasksNull = tasks == null;
            var resolverNull = resolveRegionName == null;
            if ((squadsNull || tasksNull || resolverNull) && !_nullSafeLogPrinted)
            {
                _nullSafeLogPrinted = true;
                Debug.Log($"[RosterHUD] Null-safe path used squadsNull={squadsNull} tasksNull={tasksNull} resolverNull={resolverNull} [TODO REMOVE]");
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
                    if (squad == null || string.IsNullOrEmpty(squad.id)) continue;
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
                    if (hunter == null || string.IsNullOrEmpty(hunter.id)) continue;
                    var state = BuildSoloStateText(hunter, resolveRegionName);
                    var lone = hunter.loneWolf ? " LoneWolf" : string.Empty;
                    var exhausted = hunter.exhaustedToday ? " Exhausted" : string.Empty;
                    sb.AppendLine($"[Solo] {hunter.name} [{hunter.rank}]{lone} | {state} | HP {hunter.hp}/{hunter.maxHp}{exhausted}");
                }
            }

            return sb.ToString();
        }

        private void RenderLegacyText(string text)
        {
            EnsureBodyText();
            if (bodyText == null)
            {
                return;
            }

            ConfigureLegacyBodyTextLayout();
            bodyText.gameObject.SetActive(true);
            bodyText.text = text;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Overflow;
            bodyText.ForceMeshUpdate(true);

            for (var i = 0; i < _rowPool.Count; i++)
            {
                if (_rowPool[i] != null)
                {
                    _rowPool[i].gameObject.SetActive(false);
                }
            }

            if (rosterContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rosterContent);
            }
        }

        private void RenderScrollText(string text)
        {
            EnsureBodyText();
            EnsureRosterScrollInfrastructure();
            EnsureScrollAnchorsAndMask();
            if (bodyText == null || rosterContent == null)
            {
                RenderLegacyText(text);
                return;
            }

            if (bodyText.transform.parent != rosterContent)
            {
                bodyText.transform.SetParent(rosterContent, false);
            }

            var rect = bodyText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            bodyText.gameObject.SetActive(true);
            bodyText.text = text;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Masking;
            bodyText.raycastTarget = false;
            bodyText.ForceMeshUpdate(true);

            for (var i = 0; i < _rowPool.Count; i++)
            {
                if (_rowPool[i] != null)
                {
                    _rowPool[i].gameObject.SetActive(false);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rosterContent);
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
            float sum = 0f;
            var validCount = 0;
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

            EnsureRosterScrollInfrastructure();
            ConfigureLegacyBodyTextLayout();

            if (goldText != null && goldText.font != null) bodyText.font = goldText.font;
            else if (TMP_Settings.defaultFontAsset != null) bodyText.font = TMP_Settings.defaultFontAsset;

            bodyText.color = Color.white;
            bodyText.fontSize = 15f;
            bodyText.raycastTarget = true;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Overflow;
        }

        private void EnsureRosterScrollInfrastructure()
        {
            if (rosterScrollRect == null)
            {
                rosterScrollRect = transform.Find("RosterScrollView")?.GetComponent<ScrollRect>();
            }

            if (rosterScrollRect == null)
            {
                var scrollGo = new GameObject("RosterScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
                scrollGo.transform.SetParent(transform, false);
                var scrollRect = scrollGo.GetComponent<RectTransform>();
                scrollRect.anchorMin = Vector2.zero;
                scrollRect.anchorMax = Vector2.one;
                scrollRect.offsetMin = new Vector2(8f, 8f);
                scrollRect.offsetMax = new Vector2(-8f, -8f);
                scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
                rosterScrollRect = scrollGo.GetComponent<ScrollRect>();
                rosterScrollRect.horizontal = false;
                rosterScrollRect.vertical = true;
                rosterScrollRect.movementType = ScrollRect.MovementType.Clamped;

                var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                viewportGo.transform.SetParent(scrollGo.transform, false);
                rosterViewport = viewportGo.GetComponent<RectTransform>();
                rosterViewport.anchorMin = Vector2.zero;
                rosterViewport.anchorMax = Vector2.one;
                rosterViewport.offsetMin = Vector2.zero;
                rosterViewport.offsetMax = Vector2.zero;
                viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

                var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                contentGo.transform.SetParent(viewportGo.transform, false);
                rosterContent = contentGo.GetComponent<RectTransform>();
                rosterContent.anchorMin = new Vector2(0f, 1f);
                rosterContent.anchorMax = new Vector2(1f, 1f);
                rosterContent.pivot = new Vector2(0.5f, 1f);
                rosterContent.anchoredPosition = Vector2.zero;
                rosterContent.sizeDelta = Vector2.zero;

                var layout = contentGo.GetComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(4, 4, 4, 4);
                layout.spacing = 4f;
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;

                var fitter = contentGo.GetComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                rosterScrollRect.viewport = rosterViewport;
                rosterScrollRect.content = rosterContent;
            }

            if (rosterViewport == null && rosterScrollRect != null)
            {
                rosterViewport = rosterScrollRect.viewport;
            }

            if (rosterContent == null && rosterScrollRect != null)
            {
                rosterContent = rosterScrollRect.content;
            }

            if (rosterViewport != null)
            {
                if (rosterViewport.GetComponent<Mask>() == null && rosterViewport.GetComponent<RectMask2D>() == null)
                {
                    rosterViewport.gameObject.AddComponent<RectMask2D>();
                }
            }

            if (rosterContent != null)
            {
                var layout = rosterContent.GetComponent<VerticalLayoutGroup>() ?? rosterContent.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;

                var fitter = rosterContent.GetComponent<ContentSizeFitter>() ?? rosterContent.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            if (!forceLegacyText && bodyText != null && rosterContent != null && bodyText.transform.parent != rosterContent)
            {
                bodyText.transform.SetParent(rosterContent, false);
            }

            EnsureScrollAnchorsAndMask();
        }

        private void ConfigureLegacyBodyTextLayout()
        {
            if (bodyText == null)
            {
                return;
            }

            if (bodyText.transform.parent != transform)
            {
                bodyText.transform.SetParent(transform, false);
            }

            var rect = bodyText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(paddingLeft, paddingBottom);
            rect.offsetMax = new Vector2(-paddingRight, -paddingTop);
        }

        private void EnsureScrollAnchorsAndMask()
        {
            if (rosterScrollRect == null)
            {
                return;
            }

            if (rosterViewport == null)
            {
                rosterViewport = rosterScrollRect.viewport;
            }

            if (rosterViewport != null)
            {
                var viewportRect = rosterViewport;
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = Vector2.zero;
                if (viewportRect.GetComponent<Mask>() == null && viewportRect.GetComponent<RectMask2D>() == null)
                {
                    viewportRect.gameObject.AddComponent<RectMask2D>();
                }
            }

            if (rosterContent == null)
            {
                rosterContent = rosterScrollRect.content;
            }

            if (rosterContent != null)
            {
                rosterContent.anchorMin = new Vector2(0f, 1f);
                rosterContent.anchorMax = new Vector2(1f, 1f);
                rosterContent.pivot = new Vector2(0.5f, 1f);
                rosterContent.anchoredPosition = Vector2.zero;
            }

            if (!_scrollFixLogPrinted)
            {
                _scrollFixLogPrinted = true;
                Debug.Log("[ScrollFix] content anchors/pivot fixed");
            }
        }

        private void OnGoldChanged(int value)
        {
            if (goldText != null) goldText.text = $"Gold: {value}";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void ValidateHelperDefinitionsEditorOnly()
        {
#if UNITY_EDITOR
            var scriptPath = Path.Combine(Application.dataPath, "_Game/Scripts/UI/SquadStatusHUD.cs");
            if (!File.Exists(scriptPath))
            {
                return;
            }

            var code = File.ReadAllText(scriptPath);
            ValidateSingleDefinition(code, "EnsureRow");
            ValidateSingleDefinition(code, "BuildLegacyText");
            ValidateSingleDefinition(code, "RenderScrollRows");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void ValidateSingleDefinition(string code, string helperName)
        {
#if UNITY_EDITOR
            var count = 0;
            var lines = code.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.IndexOf("private", System.StringComparison.Ordinal) < 0
                    || line.IndexOf(helperName + "(", System.StringComparison.Ordinal) < 0
                    || line.IndexOf("ValidateSingleDefinition", System.StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                count++;
            }

            if (count > 1)
            {
                Debug.LogError($"[RosterHUD] Duplicate helper detected for '{helperName}' count={count}. [TODO REMOVE]");
            }
#endif
        }
    }
}
