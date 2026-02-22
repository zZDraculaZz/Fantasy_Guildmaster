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
            Debug.Log($"[ReportUI] Show called, binding Continue listener. button={(continueButton != null)} [TODO REMOVE]");

            if (root != null)
            {
                root.SetActive(true);
                root.transform.SetAsLastSibling();
                var content = root.transform.Find("Content");
                if (content != null)
                {
                    content.SetAsLastSibling();
                }

                var canvasGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.alpha = 1f;
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.interactable = true;
                continueButton.onClick.AddListener(() =>
                {
                    Debug.Log("[ReportUI] Continue clicked [TODO REMOVE]");
                    var callback = _onContinue;
                    _onContinue = null;
                    callback?.Invoke();
                });

                var buttonText = continueButton.GetComponentsInChildren<TMP_Text>(true);
                for (var i = 0; i < buttonText.Length; i++)
                {
                    buttonText[i].raycastTarget = false;
                }
            }
            else
            {
                Debug.LogWarning("[ReportUI] Continue button is missing; click handler not bound. [TODO REMOVE]");
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

        private void EnsureRuntimeBindings()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (blockerImage == null)
            {
                blockerImage = root.GetComponent<Image>();
            }

            if (continueButton == null && root != null)
            {
                var continueByName = root.transform.Find("Content/ContinueButton");
                if (continueByName != null)
                {
                    continueButton = continueByName.GetComponent<Button>();
                }

                if (continueButton == null)
                {
                    continueButton = root.GetComponentInChildren<Button>(true);
                }
            }

            if (titleText == null && root != null)
            {
                var titleByName = root.transform.Find("Content/Title");
                titleText = titleByName != null ? titleByName.GetComponent<TMP_Text>() : root.GetComponentInChildren<TMP_Text>(true);
            }

            if (bodyText == null && root != null)
            {
                var bodyByName = root.transform.Find("Content/Body");
                bodyText = bodyByName != null ? bodyByName.GetComponent<TMP_Text>() : null;
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
