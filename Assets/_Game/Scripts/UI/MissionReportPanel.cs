using System;
using System.Text;
using FantasyGuildmaster.Core;
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
        [SerializeField] private RectTransform contentContainer;

        private Action _onContinue;
        private CanvasGroup _cg;
        private bool _pauseHeld;

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

        public bool Show(MissionReportData data, Action onContinue)
        {
            EnsureRuntimeBindings();
            _onContinue = onContinue;
            EnsureCanvasGroup();
            Debug.Log($"[ReportUI] Show called, cgPresent={(_cg != null)} button={(continueButton != null)} [TODO REMOVE]");

            gameObject.SetActive(true);
            if (root != null)
            {
                root.SetActive(true);
                root.transform.SetAsLastSibling();
                var content = root.transform.Find("Content");
                if (content != null)
                {
                    content.SetAsLastSibling();
                }
            }

            if (_cg != null)
            {
                _cg.alpha = 1f;
                _cg.interactable = true;
                _cg.blocksRaycasts = true;
            }

            if (!_pauseHeld)
            {
                GamePauseService.Push("MissionReport");
                _pauseHeld = true;
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.interactable = true;
                continueButton.onClick.AddListener(() =>
                {
                    Debug.Log("[ReportUI] Continue clicked [TODO REMOVE]");
                    onContinue?.Invoke();
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

            RefreshLayout();

            var showSucceeded = gameObject.activeInHierarchy && _cg != null && _cg.alpha > 0.001f && continueButton != null;
            if (!showSucceeded)
            {
                Debug.LogError("[ReportUI] Show failed, releasing pause + fallback [TODO REMOVE]");
                ReleasePause();
                return false;
            }

            return true;
        }

        public void Hide()
        {
            EnsureRuntimeBindings();
            _onContinue = null;
            EnsureCanvasGroup();
            if (_cg != null)
            {
                _cg.alpha = 0f;
                _cg.interactable = false;
                _cg.blocksRaycasts = false;
            }

            if (root != null)
            {
                root.SetActive(false);
            }

            ReleasePause();
        }


        private void OnDisable()
        {
            ReleasePause();
        }

        private void ReleasePause()
        {
            if (_pauseHeld)
            {
                GamePauseService.Pop("MissionReport");
                _pauseHeld = false;
            }
        }

        private void EnsureCanvasGroup()
        {
            var target = root != null ? root : gameObject;
            _cg = target.GetComponent<CanvasGroup>();
            if (_cg == null)
            {
                _cg = target.AddComponent<CanvasGroup>();
            }
        }

        private void EnsureRuntimeBindings()
        {
            if (root == null)
            {
                root = gameObject;
            }

            EnsureCanvasGroup();

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

            if (contentContainer == null && root != null)
            {
                contentContainer = root.transform.Find("Content") as RectTransform;
            }

            EnsureAutoResizeComponents();

            if (bodyText != null)
            {
                bodyText.textWrappingMode = TextWrappingModes.Normal;
            }
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
