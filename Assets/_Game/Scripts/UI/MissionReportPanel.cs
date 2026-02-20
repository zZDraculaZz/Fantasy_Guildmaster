using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public sealed class MissionReportPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Image blockerImage;

        private Action _onContinue;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            EnsureRuntimeBindings();
            Hide();
        }

        public void ConfigureRuntimeBindings(GameObject rootObject, Image blocker, TMP_Text title, TMP_Text body, Button continueBtn)
        {
            root = rootObject;
            blockerImage = blocker;
            titleText = title;
            bodyText = body;
            continueButton = continueBtn;
            EnsureRuntimeBindings();
        }

        public void Show(MissionReportData data, Action onContinue)
        {
            EnsureRuntimeBindings();
            _onContinue = onContinue;

            if (root != null)
            {
                root.SetActive(true);
                root.transform.SetAsLastSibling();
            }

            if (titleText != null)
            {
                titleText.text = "MISSION REPORT";
            }

            if (bodyText != null)
            {
                bodyText.text = BuildBodyText(data);
            }
        }

        public void Hide()
        {
            EnsureRuntimeBindings();
            _onContinue = null;
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void HandleContinuePressed()
        {
            var callback = _onContinue;
            _onContinue = null;
            callback?.Invoke();
        }

        private void EnsureRuntimeBindings()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (blockerImage == null)
            {
                blockerImage = GetComponent<Image>();
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(HandleContinuePressed);
                continueButton.onClick.AddListener(HandleContinuePressed);
            }

            if (blockerImage != null)
            {
                blockerImage.raycastTarget = true;
                blockerImage.color = blockerImage.color.a <= 0f ? new Color(0f, 0f, 0f, 0.65f) : blockerImage.color;
            }

            if (bodyText != null)
            {
                bodyText.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        private static string BuildBodyText(MissionReportData data)
        {
            if (data == null)
            {
                return "No mission report data.";
            }

            var sb = new StringBuilder(256);
            sb.AppendLine($"Squad: {Safe(data.squadName, data.squadId)}");
            sb.AppendLine($"Region: {Safe(data.regionName, data.regionId)}");
            sb.AppendLine($"Contract: {Safe(data.contractTitle, data.contractId)}");
            sb.AppendLine($"Reward: +{data.rewardGold}g");
            sb.AppendLine($"Readiness: {data.readinessBeforePercent}% -> {data.readinessAfterPercent}%");
            sb.AppendLine($"Members: {Safe(data.membersSummary, "Members not implemented yet")}");
            sb.AppendLine();
            sb.Append(data.outcomeText);
            return sb.ToString();
        }

        private static string Safe(string primary, string fallback)
        {
            return string.IsNullOrWhiteSpace(primary) ? fallback : primary;
        }
    }
}
