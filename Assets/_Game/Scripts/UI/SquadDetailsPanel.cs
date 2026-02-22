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

        private MapController _map;

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

            var layout = contentContainer.GetComponent<VerticalLayoutGroup>() ?? contentContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = contentContainer.GetComponent<ContentSizeFitter>() ?? contentContainer.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (bodyText != null)
            {
                bodyText.textWrappingMode = TextWrappingModes.Normal;
                bodyText.overflowMode = TextOverflowModes.Overflow;
            }
        }

        private void RefreshLayout()
        {
            if (bodyText != null)
            {
                bodyText.ForceMeshUpdate(true);
            }

            if (contentContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
            }
        }


        private void EnsureDetailsScroll()
        {
            if (detailsScrollRect == null)
            {
                detailsScrollRect = transform.Find("DetailsScrollView")?.GetComponent<ScrollRect>();
            }

            if (detailsScrollRect == null)
            {
                var scrollGo = new GameObject("DetailsScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
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

                var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                viewportGo.transform.SetParent(scrollGo.transform, false);
                var viewportRect = viewportGo.GetComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = Vector2.zero;
                viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

                var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                contentGo.transform.SetParent(viewportGo.transform, false);
                contentContainer = contentGo.GetComponent<RectTransform>();
                contentContainer.anchorMin = new Vector2(0f, 1f);
                contentContainer.anchorMax = new Vector2(1f, 1f);
                contentContainer.pivot = new Vector2(0.5f, 1f);
                contentContainer.anchoredPosition = Vector2.zero;
                contentContainer.sizeDelta = Vector2.zero;

                var layout = contentGo.GetComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(4, 4, 4, 4);
                layout.spacing = 6f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;

                var fitter = contentGo.GetComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                detailsScrollRect.viewport = viewportRect;
                detailsScrollRect.content = contentContainer;
            }

            if (contentContainer == null && detailsScrollRect != null)
            {
                contentContainer = detailsScrollRect.content;
            }

            var viewport = detailsScrollRect != null ? detailsScrollRect.viewport : null;
            if (viewport != null && viewport.GetComponent<Mask>() == null && viewport.GetComponent<RectMask2D>() == null)
            {
                viewport.gameObject.AddComponent<RectMask2D>();
            }

            if (contentContainer != null)
            {
                var layout = contentContainer.GetComponent<VerticalLayoutGroup>() ?? contentContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;

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
        }
    }
}
