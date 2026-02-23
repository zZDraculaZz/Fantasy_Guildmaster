using System.Text;
using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public class SquadDetailsPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private ScrollRect detailsScrollRect;
        [SerializeField] private float paddingLeft = 8f;
        [SerializeField] private float paddingRight = 8f;
        [SerializeField] private float paddingTop = 42f;
        [SerializeField] private float paddingBottom = 8f;

        [Header("Safe Mode")]
        [SerializeField] private bool forceLegacyText = false;
        private bool _legacyModeLogPrinted = false;

        private MapController _map;
        private bool _scrollFixLogPrinted;
        private bool _rectDebugLogPrinted;
        private bool _detailsScrollSetupDone;
        private bool _detailsScrollMissingLogged;


        private void Awake()
        {
            EnsureDetailsScrollSetup();
        }

        private void OnEnable()
        {
            EnsureDetailsScrollSetup();
        }

        public void BindMap(MapController map)
        {
            _map = map;
            Refresh();
        }

        public void Refresh()
        {
            EnsureTexts();
            if (titleText == null || bodyText == null)
            {
                return;
            }

            if (_map == null)
            {
                _map = FindFirstObjectByType<MapController>();
            }

            if (_map == null)
            {
                titleText.text = "Squad Details";
                bodyText.text = "MapController not found.";
                RefreshLayout();
                return;
            }

            var squad = _map.GetSelectedSquad();
            if (squad == null)
            {
                titleText.text = "Squad Details";
                bodyText.text = "No party selected";
                RefreshLayout();
                return;
            }

            titleText.text = string.IsNullOrWhiteSpace(squad.name) ? squad.id : squad.name;

            var sb = new StringBuilder(256);
            var task = _map.GetTravelTaskForSquad(squad.id);
            if (task == null)
            {
                sb.AppendLine("State: Idle");
            }
            else if (task.phase == TravelPhase.Outbound)
            {
                sb.AppendLine($"State: Traveling -> {_map.GetRegionNameById(task.toRegionId)} (OUT)");
            }
            else
            {
                sb.AppendLine("State: Returning (RET)");
            }

            sb.AppendLine($"Cohesion: {Mathf.Clamp(squad.cohesion, 0, 100)}");
            sb.AppendLine($"Status: {(squad.exhausted ? "Exhausted" : "Ready")}");
            if (_map != null)
            {
                sb.AppendLine($"New recruits: {_map.GetNewRecruitsCount(squad)}");
            }

            sb.AppendLine();
            sb.AppendLine("Members:");
            if (squad.members == null || squad.members.Count == 0)
            {
                sb.Append("- Members not implemented yet");
            }
            else
            {
                for (var i = 0; i < squad.members.Count; i++)
                {
                    var member = squad.members[i];
                    if (member == null)
                    {
                        continue;
                    }

                    var name = string.IsNullOrWhiteSpace(member.name) ? $"Member {i + 1}" : member.name;
                    var status = string.IsNullOrWhiteSpace(member.status) ? "Ready" : member.status;
                    var newTag = _map != null && member.joinedDay == _map.GetCurrentDayIndex() ? " NEW" : string.Empty;
                    sb.AppendLine($"- {name}: {member.hp}/{member.maxHp} ({status}) joinedDay={member.joinedDay}{newTag}");
                }
            }

            bodyText.text = sb.ToString();
            RefreshLayout();
        }

        private void EnsureAutoResizeComponents()
        {
            EnsureDetailsScroll();
            if (contentContainer == null)
            {
                return;
            }

            contentContainer.pivot = new Vector2(0.5f, 1f);

            var layout = contentContainer.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.enabled = false;
            }

            var fitter = contentContainer.GetComponent<ContentSizeFitter>() ?? contentContainer.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (bodyText != null)
            {
                bodyText.textWrappingMode = TextWrappingModes.Normal;
                bodyText.overflowMode = TextOverflowModes.Masking;
            }
        }

        private void RefreshLayout()
        {
            if (bodyText != null)
            {
                bodyText.gameObject.SetActive(true);
                bodyText.textWrappingMode = TextWrappingModes.Normal;
                bodyText.overflowMode = TextOverflowModes.Masking;
                bodyText.ForceMeshUpdate(true);
            }

            var canUseScroll = detailsScrollRect != null && detailsScrollRect.viewport != null && contentContainer != null;
            if (!canUseScroll)
            {
                ConfigureLegacyBodyTextLayout();
                bodyText.overflowMode = TextOverflowModes.Overflow;
                bodyText.raycastTarget = false;
                if (detailsScrollRect != null && detailsScrollRect.viewport != null)
                {
                    var mask = detailsScrollRect.viewport.GetComponent<RectMask2D>();
                    if (mask != null)
                    {
                        mask.enabled = false;
                    }
                }

                Canvas.ForceUpdateCanvases();
                return;
            }

            EnsureDetailsScrollSetup();
            var previousNormalized = detailsScrollRect != null ? detailsScrollRect.verticalNormalizedPosition : 1f;

            if (bodyText != null && contentContainer != null && bodyText.transform.parent != contentContainer)
            {
                bodyText.transform.SetParent(contentContainer, false);
            }

            var rect = bodyText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = new Vector2(paddingLeft, -paddingBottom);
            rect.offsetMax = new Vector2(-paddingRight, -paddingTop);

            bodyText.overflowMode = TextOverflowModes.Masking;
            bodyText.raycastTarget = false;
            bodyText.ForceMeshUpdate(true);
            var preferredHeight = Mathf.Max(1f, bodyText.preferredHeight + paddingTop + paddingBottom);
            contentContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);

            if (!_rectDebugLogPrinted && detailsScrollRect != null && detailsScrollRect.viewport != null)
            {
                _rectDebugLogPrinted = true;
                var vbar = detailsScrollRect.verticalScrollbar;
                Debug.Log($"[DetailsScrollFix] viewportH={detailsScrollRect.viewport.rect.height} contentH={contentContainer.rect.height} prefH={bodyText.preferredHeight} vbar={(vbar != null)} vis={detailsScrollRect.verticalScrollbarVisibility} [TODO REMOVE]");
            }
            if (detailsScrollRect != null)
            {
                detailsScrollRect.StopMovement();
                detailsScrollRect.verticalNormalizedPosition = Mathf.Clamp01(previousNormalized);
            }

            LogDetailsRectsOnce();
        }


        private void EnsureDetailsScroll()
        {
            if (detailsScrollRect == null)
            {
                detailsScrollRect = transform.Find("DetailsScroll")?.GetComponent<ScrollRect>()
                    ?? transform.Find("DetailsScrollView")?.GetComponent<ScrollRect>();
            }

            if (detailsScrollRect == null)
            {
                var scrollGo = new GameObject("DetailsScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
                scrollGo.transform.SetParent(transform, false);
                var scrollRect = scrollGo.GetComponent<RectTransform>();
                scrollRect.anchorMin = new Vector2(0f, 0f);
                scrollRect.anchorMax = new Vector2(1f, 1f);
                scrollRect.offsetMin = new Vector2(8f, 8f);
                scrollRect.offsetMax = new Vector2(-8f, -42f);
                scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

                detailsScrollRect = scrollGo.GetComponent<ScrollRect>();
                detailsScrollRect.horizontal = false;
                detailsScrollRect.vertical = true;
                detailsScrollRect.movementType = ScrollRect.MovementType.Clamped;

                var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                viewportGo.transform.SetParent(scrollGo.transform, false);
                var viewportRect = viewportGo.GetComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = Vector2.zero;
                viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

                var contentGo = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
                contentGo.transform.SetParent(viewportGo.transform, false);
                contentContainer = contentGo.GetComponent<RectTransform>();
                contentContainer.anchorMin = new Vector2(0f, 1f);
                contentContainer.anchorMax = new Vector2(1f, 1f);
                contentContainer.pivot = new Vector2(0.5f, 1f);
                contentContainer.anchoredPosition = Vector2.zero;
                contentContainer.sizeDelta = new Vector2(0f, Mathf.Max(contentContainer.sizeDelta.y, 1f));

                var fitter = contentGo.GetComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                detailsScrollRect.viewport = viewportRect;
                detailsScrollRect.content = contentContainer;
            }

            if (contentContainer == null && detailsScrollRect != null)
            {
                contentContainer = detailsScrollRect.content;
                if (contentContainer == null)
                {
                    var recoveredViewport = detailsScrollRect.viewport ?? detailsScrollRect.transform.Find("Viewport") as RectTransform;
                    if (detailsScrollRect.viewport == null && recoveredViewport != null)
                    {
                        detailsScrollRect.viewport = recoveredViewport;
                    }

                    if (recoveredViewport != null)
                    {
                        contentContainer = recoveredViewport.Find("Content") as RectTransform;
                        if (contentContainer != null)
                        {
                            detailsScrollRect.content = contentContainer;
                        }
                    }
                }
            }

            if (detailsScrollRect != null)
            {
                var scrollLayoutGroup = detailsScrollRect.GetComponent<VerticalLayoutGroup>();
                if (scrollLayoutGroup != null)
                {
                    scrollLayoutGroup.enabled = false;
                }

                var scrollLayoutElement = detailsScrollRect.GetComponent<LayoutElement>() ?? detailsScrollRect.gameObject.AddComponent<LayoutElement>();
                scrollLayoutElement.minHeight = Mathf.Max(scrollLayoutElement.minHeight, 180f);
                scrollLayoutElement.flexibleHeight = Mathf.Max(scrollLayoutElement.flexibleHeight, 1f);
                scrollLayoutElement.preferredHeight = -1f;
            }

            var viewport = detailsScrollRect != null ? detailsScrollRect.viewport : null;
            if (viewport != null && viewport.GetComponent<Mask>() == null && viewport.GetComponent<RectMask2D>() == null)
            {
                viewport.gameObject.AddComponent<RectMask2D>();
            }

            if (contentContainer != null)
            {
                var layout = contentContainer.GetComponent<VerticalLayoutGroup>();
                if (layout != null)
                {
                    layout.enabled = false;
                }

                var fitter = contentContainer.GetComponent<ContentSizeFitter>() ?? contentContainer.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            if (bodyText != null && contentContainer != null && bodyText.transform.parent != contentContainer)
            {
                bodyText.transform.SetParent(contentContainer, false);
                var rect = bodyText.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
            }

        }


        private void EnsureDetailsScrollSetup()
        {
            if (_detailsScrollSetupDone)
            {
                return;
            }

            EnsureDetailsScroll();
            if (detailsScrollRect == null)
            {
                if (!_detailsScrollMissingLogged)
                {
                    _detailsScrollMissingLogged = true;
                    Debug.Log("[DetailsScrollFix] Missing ScrollRect reference [TODO REMOVE]");
                }

                return;
            }

            var viewport = detailsScrollRect.viewport != null
                ? detailsScrollRect.viewport
                : detailsScrollRect.transform.Find("Viewport") as RectTransform;
            if (detailsScrollRect.viewport == null)
            {
                detailsScrollRect.viewport = viewport;
            }

            if (contentContainer == null)
            {
                contentContainer = detailsScrollRect.content != null
                    ? detailsScrollRect.content
                    : viewport != null
                        ? viewport.Find("Content") as RectTransform
                        : null;
            }

            if (bodyText == null)
            {
                bodyText = transform.Find("DetailsScroll/Viewport/Content/BodyText")?.GetComponent<TMP_Text>()
                    ?? transform.Find("BodyText")?.GetComponent<TMP_Text>()
                    ?? contentContainer?.Find("BodyText")?.GetComponent<TMP_Text>();
            }

            if (viewport == null || contentContainer == null || bodyText == null)
            {
                if (!_detailsScrollMissingLogged)
                {
                    _detailsScrollMissingLogged = true;
                    Debug.Log($"[DetailsScrollFix] Missing refs viewport={(viewport != null)} content={(contentContainer != null)} bodyText={(bodyText != null)} [TODO REMOVE]");
                }

                return;
            }

            if (viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null)
            {
                viewport.gameObject.AddComponent<RectMask2D>();
            }

            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;

            if (contentContainer.parent != viewport)
            {
                contentContainer.SetParent(viewport, false);
            }

            contentContainer.anchorMin = new Vector2(0f, 1f);
            contentContainer.anchorMax = new Vector2(1f, 1f);
            contentContainer.pivot = new Vector2(0.5f, 1f);
            contentContainer.anchoredPosition = Vector2.zero;

            var layout = contentContainer.GetComponent<VerticalLayoutGroup>();
            var drivenByLayoutGroup = layout != null && layout.enabled;
            if (layout != null)
            {
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.enabled = false;
            }

            var fitter = contentContainer.GetComponent<ContentSizeFitter>() ?? contentContainer.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (bodyText.transform.parent != contentContainer)
            {
                bodyText.transform.SetParent(contentContainer, false);
            }

            var bodyRect = bodyText.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.anchoredPosition = Vector2.zero;
            bodyRect.offsetMin = new Vector2(paddingLeft, -paddingBottom);
            bodyRect.offsetMax = new Vector2(-paddingRight, -paddingTop);

            var bodyFitter = bodyText.GetComponent<ContentSizeFitter>();
            if (bodyFitter != null)
            {
                bodyFitter.enabled = false;
            }

            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Masking;
            bodyText.raycastTarget = false;

            var vbar = detailsScrollRect.verticalScrollbar;
            if (vbar == null)
            {
                var vbarRect = detailsScrollRect.transform.Find("Scrollbar Vertical") as RectTransform;
                if (vbarRect != null)
                {
                    vbar = vbarRect.GetComponent<Scrollbar>() ?? vbarRect.gameObject.AddComponent<Scrollbar>();
                }
            }

            if (vbar != null)
            {
                var vbarRect = vbar.transform as RectTransform;
                if (vbarRect != null)
                {
                    if (vbarRect.parent != detailsScrollRect.transform)
                    {
                        vbarRect.SetParent(detailsScrollRect.transform, false);
                    }

                    vbarRect.anchorMin = new Vector2(1f, 0f);
                    vbarRect.anchorMax = new Vector2(1f, 1f);
                    vbarRect.pivot = new Vector2(1f, 1f);
                    vbarRect.sizeDelta = new Vector2(18f, vbarRect.sizeDelta.y);
                    vbarRect.anchoredPosition = Vector2.zero;
                    vbarRect.SetAsLastSibling();
                }

                detailsScrollRect.verticalScrollbar = vbar;
                detailsScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                detailsScrollRect.verticalScrollbarSpacing = 0f;
            }

            detailsScrollRect.content = contentContainer;
            detailsScrollRect.viewport = viewport;
            detailsScrollRect.horizontal = false;
            detailsScrollRect.vertical = true;
            detailsScrollRect.movementType = ScrollRect.MovementType.Clamped;
            detailsScrollRect.inertia = true;

            if (!_scrollFixLogPrinted)
            {
                _scrollFixLogPrinted = true;
                Debug.Log($"[ScrollFix] DetailsScroll wired: vis={detailsScrollRect.verticalScrollbarVisibility} contentPivot={contentContainer.pivot} drivenByLayoutGroup={drivenByLayoutGroup} [TODO REMOVE]");
            }

            _detailsScrollSetupDone = true;
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

        private float GetBodyTextHeight()
        {
            if (bodyText == null)
            {
                return 1f;
            }

            bodyText.ForceMeshUpdate(true);
            var rendered = bodyText.textBounds.size.y + 12f;
            return Mathf.Max(1f, bodyText.preferredHeight, rendered);
        }

        private void LogDetailsRectsOnce()
        {
            if (_rectDebugLogPrinted || bodyText == null || contentContainer == null)
            {
                return;
            }

            _rectDebugLogPrinted = true;
            var textRect = bodyText.rectTransform.rect;
            var contentRect = contentContainer.rect;
            Debug.Log($"[UI] DetailsTMP rect={textRect.width}x{textRect.height} preferred={bodyText.preferredWidth}x{bodyText.preferredHeight} contentRect={contentRect.width}x{contentRect.height} [TODO REMOVE]");
        }

        private void EnsureTexts()
        {
            if (titleText == null)
            {
                titleText = transform.Find("Title")?.GetComponent<TMP_Text>();
                if (titleText == null)
                {
                    var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                    titleGo.transform.SetParent(transform, false);
                    titleText = titleGo.GetComponent<TextMeshProUGUI>();
                    titleText.fontSize = 22f;
                    titleText.alignment = TextAlignmentOptions.TopLeft;
                    titleText.textWrappingMode = TextWrappingModes.NoWrap;
                    titleText.overflowMode = TextOverflowModes.Ellipsis;
                }
            }

            if (bodyText == null)
            {
                bodyText = transform.Find("BodyText")?.GetComponent<TMP_Text>();
                if (bodyText == null)
                {
                    var bodyGo = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
                    bodyGo.transform.SetParent(transform, false);
                    bodyText = bodyGo.GetComponent<TextMeshProUGUI>();
                    bodyText.fontSize = 18f;
                    bodyText.alignment = TextAlignmentOptions.TopLeft;
                }
            }

            if (contentContainer == null)
            {
                var content = transform.Find("Content");
                if (content != null)
                {
                    contentContainer = content as RectTransform;
                }

                if (contentContainer == null && bodyText != null)
                {
                    contentContainer = bodyText.transform.parent as RectTransform;
                }
            }

            EnsureDetailsScroll();
            EnsureAutoResizeComponents();
            if (forceLegacyText)
            {
                if (!_legacyModeLogPrinted)
                {
                    _legacyModeLogPrinted = true;
                    Debug.Log("[SquadDetails] forceLegacyText enabled -> showing TMP block");
                }

                ConfigureLegacyBodyTextLayout();
            }
            else
            {
                EnsureDetailsScrollSetup();
            }
        }
    }
}
