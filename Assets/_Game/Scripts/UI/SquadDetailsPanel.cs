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

        private MapController _map;

        public void BindMap(MapController map)
        {
            _map = map;
            Refresh();
        }

        public void Refresh()
        {
            EnsureTexts();
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
                bodyText.text = "Select a squad in Squad HUD.";
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

        private void EnsureTexts()
        {
            if (titleText == null)
            {
                titleText = transform.Find("Title")?.GetComponent<TMP_Text>();
            }

            if (bodyText == null)
            {
                bodyText = transform.Find("BodyText")?.GetComponent<TMP_Text>();
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

            EnsureAutoResizeComponents();
        }
    }
}
