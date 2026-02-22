using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public sealed class ContractRow : MonoBehaviour
    {
        private const string ContractsIconBasePath = "Icons/Contracts/";
        private const string ContractsFallbackPath = "Icons/Contracts/contract_generic";

        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text reqText;
        [SerializeField] private TMP_Text unavailableText;

        private static readonly Color NormalBackground = new(0.18f, 0.18f, 0.18f, 0.82f);
        private static readonly Color SelectedBackground = new(0.35f, 0.32f, 0.12f, 0.92f);
        private static readonly Color AssignedBackground = new(0.12f, 0.24f, 0.14f, 0.92f);

        private Image _backgroundImage;
        private bool _isAssigned;
        private bool _reqCreatedLogPrinted;

        private ContractData _contract;

        public ContractData Contract => _contract;

        public void Bind(ContractData contract)
        {
            _contract = contract;
            _backgroundImage = GetComponent<Image>();
            _isAssigned = false;
            Refresh();
            ApplyVisualState(false);
        }

        public void SetSelected(bool selected)
        {
            ApplyVisualState(selected);
        }

        public void SetAssigned(bool assigned)
        {
            _isAssigned = assigned;
            ApplyVisualState(false);
        }



        private void EnsureReqText()
        {
            if (reqText == null)
            {
                reqText = transform.Find("ReqText")?.GetComponent<TMP_Text>();
                if (reqText == null)
                {
                    reqText = transform.Find("RequirementsText")?.GetComponent<TMP_Text>();
                }
            }

            if (reqText != null)
            {
                return;
            }

            var reqGo = new GameObject("ReqText", typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            reqGo.transform.SetParent(transform, false);
            reqText = reqGo.GetComponent<TextMeshProUGUI>();
            if (!_reqCreatedLogPrinted)
            {
                _reqCreatedLogPrinted = true;
                Debug.Log("[ContractRow] reqText missing -> created at runtime [TODO REMOVE]");
            }
            reqText.fontSize = 16f;
            reqText.alignment = TextAlignmentOptions.MidlineRight;

            var layoutElement = reqGo.GetComponent<LayoutElement>();
            layoutElement.minWidth = 110f;
            layoutElement.preferredWidth = 180f;

            var rect = reqText.rectTransform;
            rect.anchorMin = new Vector2(0.50f, 0.52f);
            rect.anchorMax = new Vector2(0.98f, 0.92f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public void SetRequirements(string requirements)
        {
            EnsureReqText();

            if (reqText == null)
            {
                return;
            }

            var layoutElement = reqText.GetComponent<LayoutElement>() ?? reqText.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = Mathf.Max(layoutElement.minWidth, 110f);
            layoutElement.preferredWidth = Mathf.Max(layoutElement.preferredWidth, 180f);

            reqText.text = string.IsNullOrWhiteSpace(requirements) ? string.Empty : requirements;
            reqText.textWrappingMode = TextWrappingModes.NoWrap;
            reqText.overflowMode = TextOverflowModes.Ellipsis;
            reqText.raycastTarget = false;
            reqText.ForceMeshUpdate(true);
        }

        public void SetUnavailableReason(string reason)
        {
            if (unavailableText == null)
            {
                unavailableText = transform.Find("UnavailableText")?.GetComponent<TMP_Text>();
            }

            if (unavailableText == null)
            {
                return;
            }

            var hasReason = !string.IsNullOrWhiteSpace(reason);
            unavailableText.gameObject.SetActive(hasReason);
            unavailableText.text = hasReason ? reason : string.Empty;
            unavailableText.textWrappingMode = TextWrappingModes.NoWrap;
            unavailableText.overflowMode = TextOverflowModes.Ellipsis;
            unavailableText.raycastTarget = false;
        }

        public void Refresh()
        {
            if (_contract == null)
            {
                return;
            }

            if (iconImage != null)
            {
                var path = string.IsNullOrWhiteSpace(_contract.iconKey) ? null : ContractsIconBasePath + _contract.iconKey;
                iconImage.sprite = SpriteLoader.TryLoadSprite(path, ContractsFallbackPath);
            }

            if (titleText != null)
            {
                titleText.text = _contract.title;
            }

            if (timerText != null)
            {
                timerText.text = _contract.RemainingText;
            }

            if (rewardText != null)
            {
                rewardText.text = $"{_contract.reward}g";
            }

            SetRequirements(ContractUiText.FormatContractReq(_contract));

        }

        private void ApplyVisualState(bool selected)
        {
            _backgroundImage ??= GetComponent<Image>();
            if (_backgroundImage == null)
            {
                return;
            }

            if (_isAssigned)
            {
                _backgroundImage.color = AssignedBackground;
                return;
            }

            _backgroundImage.color = selected ? SelectedBackground : NormalBackground;
        }
    }
}
